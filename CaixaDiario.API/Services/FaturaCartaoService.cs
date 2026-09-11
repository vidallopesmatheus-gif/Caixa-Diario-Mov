using System.Text.Json;
using CaixaDiario.API.Data;
using CaixaDiario.API.DTOs.FaturasCartao;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Services;

/// <summary>
/// Fatura de cartão não é uma entidade própria no banco — é calculada na hora, agrupando as
/// compras (Saidas) da conta CartaoCredito pela competência em que caem (ver ObterCompetencia,
/// que usa ContaBancaria.DiaFechamento). O pagamento, esse sim, é persistido (PagamentoFatura),
/// porque precisa sobreviver a reclassificação/estorno e alimentar o saldo devedor.
///
/// v1 não reconhece juros/multa quando o valor pago é menor que o total da fatura — a diferença
/// simplesmente continua como saldo devedor, rolando pra próxima competência sem gerar despesa
/// nova (decisão registrada no PR que introduziu cartão de crédito).
/// </summary>
public class FaturaCartaoService : IFaturaCartaoService
{
    private const decimal ToleranciaQuitacao = 0.005m;

    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IPagamentoFaturaRepository _pagamentoRepo;
    private readonly IAuditService _auditService;
    private readonly AppDbContext _context;

    public FaturaCartaoService(
        IContaBancariaRepository contaRepo,
        IRegistroRepository registroRepo,
        IPagamentoFaturaRepository pagamentoRepo,
        IAuditService auditService,
        AppDbContext context)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _pagamentoRepo = pagamentoRepo;
        _auditService = auditService;
        _context = context;
    }

    public async Task<List<FaturaCartaoDto>> ListarFaturasAsync(Guid contaCartaoId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await ObterCartaoOuFalharAsync(contaCartaoId);
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        return await ListarFaturasInternoAsync(conta);
    }

    public async Task<FaturaCartaoDto?> SugerirFaturaAsync(
        Guid contaCartaoId, decimal valor, DateOnly data, Guid usuarioLogadoId, string perfil)
    {
        var conta = await ObterCartaoOuFalharAsync(contaCartaoId);
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var faturas = (await ListarFaturasInternoAsync(conta)).Where(f => f.Status != "Paga").ToList();
        if (faturas.Count == 0) return null;

        // Confirmação de qual fatura é sempre do usuário — isto só pré-seleciona a opção mais
        // provável, priorizando valor batendo (o critério mais forte) e desempatando por vencimento
        // mais próximo da data do pagamento.
        return faturas
            .OrderBy(f => Math.Abs(f.SaldoDevedor - valor))
            .ThenBy(f => Math.Abs(f.DataVencimento.DayNumber - data.DayNumber))
            .First();
    }

    public async Task<PagamentoFaturaDto> VincularPagamentoAsync(
        VincularPagamentoFaturaDto dto, Guid usuarioLogadoId, string perfil)
    {
        var contaOrigem = await _contaRepo.ObterPorIdAsync(dto.ContaOrigemId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(contaOrigem.ClienteId, usuarioLogadoId, perfil);

        var contaCartao = await ObterCartaoOuFalharAsync(dto.ContaCartaoId);
        if (contaCartao.ClienteId != contaOrigem.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        if (!contaCartao.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "O cartão deve estar ativo.");

        var registro = await _registroRepo.ObterPorContaEDataAsync(dto.ContaOrigemId, dto.Data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");
        var item = registro.Saidas.FirstOrDefault(s => s.Id == dto.LancamentoId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");
        if (item.TransferenciaId.HasValue || item.PagamentoFaturaId.HasValue)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Este lançamento já foi classificado.");

        var pagamentoId = Guid.NewGuid();
        var valorPago = item.Valor;

        // Transacional pelo mesmo motivo de ConverterLancamentoAsync: a ponta original, a
        // contrapartida no cartão e o registro de PagamentoFatura têm que ser gravados juntos —
        // sem isso, um retry sem essas marcas duplicaria o abatimento na dívida do cartão.
        await using var transacao = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        item.TipoCusto = LancamentoFiltro.TipoPagamentoFatura;
        item.Categoria = "Pagamento de fatura";
        item.PagamentoFaturaId = pagamentoId;
        item.PendenteCategorizacao = false;
        registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas); // força o EF a detectar a mudança (jsonb)
        registro.SalvoEm = DateTime.UtcNow;
        await _registroRepo.AtualizarAsync(registro);

        var (regCartao, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contaCartao, dto.Data);
        regCartao.Entradas = new List<ItemFinanceiro>(regCartao.Entradas)
        {
            new()
            {
                Id = Guid.NewGuid(),
                Descricao = $"Pagamento fatura {dto.Competencia}",
                Valor = valorPago,
                Categoria = "Pagamento de fatura",
                TipoCusto = LancamentoFiltro.TipoPagamentoFatura,
                PagamentoFaturaId = pagamentoId,
            },
        };
        // Saldo do cartão é negativo (dívida) — somar o valor pago reduz a dívida em direção a zero.
        regCartao.SaldoFinal += valorPago;
        regCartao.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regCartao, novo);

        var pagamento = new PagamentoFatura
        {
            Id = pagamentoId,
            ClienteId = contaOrigem.ClienteId,
            ContaCartaoId = dto.ContaCartaoId,
            ContaOrigemId = dto.ContaOrigemId,
            Competencia = dto.Competencia,
            ValorPago = valorPago,
            Data = dto.Data,
            CriadoEm = DateTime.UtcNow,
        };
        var criado = await _pagamentoRepo.AdicionarAsync(pagamento);
        if (transacao != null) await transacao.CommitAsync();

        var resultado = MapToDto(criado, contaCartao.Nome, contaOrigem.Nome);

        await _auditService.LogAsync(contaOrigem.ClienteId, usuarioLogadoId, "PagamentoFatura", "Vincular",
            pagamentoId.ToString(), null, JsonSerializer.Serialize(resultado));

        return resultado;
    }

    public async Task DesvincularPagamentoAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var pagamento = await _pagamentoRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.PAGAMENTO_FATURA_NAO_ENCONTRADO, "Pagamento não encontrado.");
        VerificarAcesso(pagamento.ClienteId, usuarioLogadoId, perfil);

        await using var transacao = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        var regOrigem = await _registroRepo.ObterPorContaEDataAsync(pagamento.ContaOrigemId, pagamento.Data);
        var itemOrigem = regOrigem?.Saidas.FirstOrDefault(s => s.PagamentoFaturaId == pagamento.Id);
        if (regOrigem != null && itemOrigem != null)
        {
            itemOrigem.Categoria = string.Empty;
            itemOrigem.TipoCusto = null;
            itemOrigem.PagamentoFaturaId = null;
            itemOrigem.PendenteCategorizacao = true;
            regOrigem.Saidas = new List<ItemFinanceiroSaida>(regOrigem.Saidas);
            regOrigem.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regOrigem);
        }

        // Diferente da contrapartida de transferência (que pode ter sido vinculada a um lançamento
        // já real do extrato), a entrada no cartão só existe porque este pagamento foi vinculado —
        // não é uma transação independente. Por isso ela é removida, não só desmarcada.
        var regCartao = await _registroRepo.ObterPorContaEDataAsync(pagamento.ContaCartaoId, pagamento.Data);
        var itemCartao = regCartao?.Entradas.FirstOrDefault(e => e.PagamentoFaturaId == pagamento.Id);
        if (regCartao != null && itemCartao != null)
        {
            regCartao.Entradas = regCartao.Entradas.Where(e => e.Id != itemCartao.Id).ToList();
            regCartao.SaldoFinal -= itemCartao.Valor;
            regCartao.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regCartao);
        }

        await _pagamentoRepo.RemoverAsync(pagamento);
        if (transacao != null) await transacao.CommitAsync();

        await _auditService.LogAsync(pagamento.ClienteId, usuarioLogadoId, "PagamentoFatura", "Desvincular",
            pagamento.Id.ToString(), JsonSerializer.Serialize(pagamento), null);
    }

    private async Task<List<FaturaCartaoDto>> ListarFaturasInternoAsync(ContaBancaria contaCartao)
    {
        var registros = (await _registroRepo.ListarPorContaAsync(contaCartao.Id)).Where(r => !r.Excluido);
        var pagamentos = await _pagamentoRepo.ListarPorContaCartaoAsync(contaCartao.Id);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var comprasPorCompetencia = registros
            .SelectMany(r => r.Saidas
                .Where(s => s.TipoCusto != LancamentoFiltro.TipoPagamentoFatura)
                .Select(s => (Competencia: ObterCompetencia(r.Data, contaCartao.DiaFechamento), s.Valor)))
            .GroupBy(x => x.Competencia)
            .ToDictionary(g => g.Key, g => (Total: g.Sum(x => x.Valor), Qtd: g.Count()));

        var pagoPorCompetencia = pagamentos
            .GroupBy(p => p.Competencia)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.ValorPago));

        var competencias = comprasPorCompetencia.Keys.Union(pagoPorCompetencia.Keys).Distinct();

        return competencias.Select(competencia =>
        {
            var (total, qtd) = comprasPorCompetencia.GetValueOrDefault(competencia, (Total: 0m, Qtd: 0));
            var pago = pagoPorCompetencia.GetValueOrDefault(competencia, 0m);
            var saldoDevedor = Math.Max(0m, total - pago);
            var (fechamento, vencimento) = ObterDatasCiclo(competencia, contaCartao.DiaFechamento, contaCartao.DiaVencimento);
            var status = hoje < fechamento ? "Aberta" : (saldoDevedor <= ToleranciaQuitacao ? "Paga" : "Fechada");

            return new FaturaCartaoDto
            {
                Competencia = competencia,
                DataFechamento = fechamento,
                DataVencimento = vencimento,
                ValorTotal = total,
                ValorPago = pago,
                SaldoDevedor = saldoDevedor,
                QuantidadeCompras = qtd,
                Status = status,
            };
        })
        .OrderByDescending(f => f.Competencia)
        .ToList();
    }

    /// <summary>
    /// Em qual fatura (competência "yyyy-MM", identificada pelo mês em que ela FECHA) uma compra
    /// cai, dado o dia de fechamento do cartão. Sem DiaFechamento configurado, cai no mês
    /// calendário da própria compra — aproximação razoável até o usuário preencher o campo.
    /// </summary>
    public static string ObterCompetencia(DateOnly data, int? diaFechamento)
    {
        if (!diaFechamento.HasValue) return $"{data.Year:D4}-{data.Month:D2}";

        var fechamento = Math.Clamp(diaFechamento.Value, 1, 28);
        var mesCompetencia = data.Day <= fechamento
            ? new DateOnly(data.Year, data.Month, 1)
            : new DateOnly(data.Year, data.Month, 1).AddMonths(1);
        return $"{mesCompetencia.Year:D4}-{mesCompetencia.Month:D2}";
    }

    // Vencimento, na prática, cai no mês seguinte ao fechamento — mesmo sem DiaVencimento
    // configurado, usa o próprio dia de fechamento como aproximação.
    private static (DateOnly Fechamento, DateOnly Vencimento) ObterDatasCiclo(
        string competencia, int? diaFechamento, int? diaVencimento)
    {
        var partes = competencia.Split('-');
        var ano = int.Parse(partes[0]);
        var mes = int.Parse(partes[1]);
        var ultimoDiaMes = new DateOnly(ano, mes, 1).AddMonths(1).AddDays(-1);

        var diaFech = Math.Clamp(diaFechamento ?? ultimoDiaMes.Day, 1, ultimoDiaMes.Day);
        var fechamento = new DateOnly(ano, mes, diaFech);

        var mesVencimento = fechamento.AddMonths(1);
        var ultimoDiaMesVencimento = new DateOnly(mesVencimento.Year, mesVencimento.Month, 1).AddMonths(1).AddDays(-1);
        var diaVenc = Math.Clamp(diaVencimento ?? diaFech, 1, ultimoDiaMesVencimento.Day);
        var vencimento = new DateOnly(mesVencimento.Year, mesVencimento.Month, diaVenc);

        return (fechamento, vencimento);
    }

    private async Task<ContaBancaria> ObterCartaoOuFalharAsync(Guid contaCartaoId)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaCartaoId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (conta.Tipo != "CartaoCredito")
            throw new ApiException(400, CodigoRetorno.CONTA_NAO_E_CARTAO, "Esta conta não é um cartão de crédito.");
        return conta;
    }

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }

    private static PagamentoFaturaDto MapToDto(PagamentoFatura p, string contaCartaoNome, string contaOrigemNome) => new()
    {
        Id = p.Id,
        ContaCartaoId = p.ContaCartaoId,
        ContaCartaoNome = contaCartaoNome,
        ContaOrigemId = p.ContaOrigemId,
        ContaOrigemNome = contaOrigemNome,
        Competencia = p.Competencia,
        ValorPago = p.ValorPago,
        Data = p.Data,
        CriadoEm = p.CriadoEm,
    };
}
