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
    private readonly ICategoriaRepository _categoriaRepo;
    private readonly IAuditService _auditService;
    private readonly AppDbContext _context;
    private readonly IFaturaRepository _faturaRepo;

    public FaturaCartaoService(
        IContaBancariaRepository contaRepo,
        IRegistroRepository registroRepo,
        IPagamentoFaturaRepository pagamentoRepo,
        ICategoriaRepository categoriaRepo,
        IAuditService auditService,
        AppDbContext context,
        IFaturaRepository faturaRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _pagamentoRepo = pagamentoRepo;
        _categoriaRepo = categoriaRepo;
        _auditService = auditService;
        _context = context;
        _faturaRepo = faturaRepo;
    }

    public async Task<List<string>> ListarBancosAsync(Guid contaCartaoId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await ObterCartaoOuFalharAsync(contaCartaoId);
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        return await _faturaRepo.ListarBancosDistintosPorClienteAsync(conta.ClienteId);
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
        // provável. Prioriza valor batendo exatamente (duas faturas de bancos diferentes raramente
        // têm o mesmo saldo devedor, então valor exato já costuma apontar o banco certo também);
        // sem match exato, cai pro "mais parecido" como fallback. Desempate por vencimento mais
        // próximo da data do pagamento.
        var exatas = faturas.Where(f => Math.Abs(f.SaldoDevedor - valor) <= ToleranciaQuitacao).ToList();
        var candidatas = exatas.Count > 0 ? exatas : faturas;

        return candidatas
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
            FaturaId = dto.FaturaId,
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

    // Item F4 do bundle D6JWEAJa: paga uma fatura direto da tela de Faturas — diferente de
    // VincularPagamentoAsync, não precisa de um lançamento já existente pra reclassificar; cria a
    // saída na conta de origem e a entrada no cartão do zero (mesma ideia de TransferenciaService.
    // CriarAsync pra transferências "do zero"). Por ser dinheiro novo saindo da origem (não uma
    // reclassificação de algo que já tinha efeito no saldo), cascata o saldo da origem — diferente
    // de VincularPagamentoAsync, que não cascata porque o lançamento já existia antes.
    public async Task<PagamentoFaturaDto> PagarFaturaAsync(
        Guid contaCartaoId, PagarFaturaDto dto, Guid usuarioLogadoId, string perfil)
    {
        var contaCartao = await ObterCartaoOuFalharAsync(contaCartaoId);
        VerificarAcesso(contaCartao.ClienteId, usuarioLogadoId, perfil);
        if (!contaCartao.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "O cartão deve estar ativo.");

        var contaOrigem = await _contaRepo.ObterPorIdAsync(dto.ContaOrigemId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (contaOrigem.ClienteId != contaCartao.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Conta bancária não pertence a este cliente.");
        if (contaOrigem.Tipo == "CartaoCredito")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Escolha uma conta que não seja de cartão para pagar a fatura.", "contaOrigemId");
        if (!contaOrigem.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "A conta de origem deve estar ativa.");

        var faturasDaConta = await ListarFaturasInternoAsync(contaCartao);
        var fatura = (dto.FaturaId.HasValue
                ? faturasDaConta.FirstOrDefault(f => f.Id == dto.FaturaId)
                : faturasDaConta.FirstOrDefault(f => f.Id == null && f.Competencia == dto.Competencia))
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Fatura não encontrada.");
        if (fatura.Status == "Paga")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Esta fatura já está paga.");

        var valorPago = dto.Valor ?? fatura.SaldoDevedor;
        if (valorPago <= 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe um valor maior que zero.", "valor");

        var pagamentoId = Guid.NewGuid();

        await using var transacao = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        var (regOrigem, novoOrigem) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contaOrigem, dto.Data);
        regOrigem.Saidas = new List<ItemFinanceiroSaida>(regOrigem.Saidas)
        {
            new()
            {
                Id = Guid.NewGuid(), Descricao = $"Pagamento fatura {dto.Competencia} — {contaCartao.Nome}",
                Valor = valorPago, Categoria = "Pagamento de fatura", TipoCusto = LancamentoFiltro.TipoPagamentoFatura,
                PagamentoFaturaId = pagamentoId,
            },
        };
        regOrigem.SaldoFinal -= valorPago;
        regOrigem.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regOrigem, novoOrigem);

        var registrosDaOrigem = await _registroRepo.ListarPorContaAsync(contaOrigem.Id);
        await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, contaOrigem.Id, dto.Data, regOrigem.SaldoFinal, registrosDaOrigem);

        var (regCartao, novoCartao) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contaCartao, dto.Data);
        regCartao.Entradas = new List<ItemFinanceiro>(regCartao.Entradas)
        {
            new()
            {
                Id = Guid.NewGuid(), Descricao = $"Pagamento fatura {dto.Competencia}", Valor = valorPago,
                Categoria = "Pagamento de fatura", TipoCusto = LancamentoFiltro.TipoPagamentoFatura,
                PagamentoFaturaId = pagamentoId,
            },
        };
        // Saldo do cartão é negativo (dívida) — somar o valor pago reduz a dívida em direção a zero.
        regCartao.SaldoFinal += valorPago;
        regCartao.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regCartao, novoCartao);

        var pagamento = new PagamentoFatura
        {
            Id = pagamentoId, ClienteId = contaOrigem.ClienteId, ContaCartaoId = contaCartaoId,
            ContaOrigemId = dto.ContaOrigemId, Competencia = dto.Competencia, FaturaId = fatura.Id,
            ValorPago = valorPago, Data = dto.Data, CriadoEm = DateTime.UtcNow,
        };
        var criado = await _pagamentoRepo.AdicionarAsync(pagamento);
        if (transacao != null) await transacao.CommitAsync();

        var resultado = MapToDto(criado, contaCartao.Nome, contaOrigem.Nome);

        await _auditService.LogAsync(contaOrigem.ClienteId, usuarioLogadoId, "PagamentoFatura", "Pagar",
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

    // Item 9: compra parcelada lançada direto no cartão — cada parcela vira sua própria Saida,
    // numa data um mês depois da anterior (cada uma cai numa competência/fatura futura diferente,
    // via ObterCompetencia). Nunca cria um lançamento só "informativo": o impacto real no fluxo de
    // caixa dos próximos meses tem que aparecer em cada fatura, não só na data da compra.
    public async Task LancarComprasParceladaAsync(
        Guid contaCartaoId, Guid usuarioLogadoId, string perfil, LancarCompraParceladaDto dto, Guid? origemMetaId = null)
    {
        var conta = await ObterCartaoOuFalharAsync(contaCartaoId);
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        if (!conta.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "O cartão deve estar ativo.");
        if (dto.QuantidadeParcelas < 2)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe ao menos 2 parcelas.", "quantidadeParcelas");
        if (dto.ValorTotal <= 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe um valor total maior que zero.", "valorTotal");
        if (string.IsNullOrWhiteSpace(dto.Categoria))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a categoria da compra.", "categoria");

        var tipoCusto = (await _categoriaRepo.ListarTodasPorClienteAsync(conta.ClienteId))
            .ToDictionary(c => c.Nome, c => c.Tipo)
            .GetValueOrDefault(dto.Categoria);

        var parcelamentoId = Guid.NewGuid();
        var valorParcela = Math.Round(dto.ValorTotal / dto.QuantidadeParcelas, 2);
        var somaParcelasAnteriores = 0m;

        for (var i = 0; i < dto.QuantidadeParcelas; i++)
        {
            // Última parcela absorve o resto do arredondamento — a soma das parcelas bate exatamente
            // com o valor total da compra, nunca sobra/falta centavo em nenhuma fatura.
            var ultimaParcela = i == dto.QuantidadeParcelas - 1;
            var valor = ultimaParcela ? dto.ValorTotal - somaParcelasAnteriores : valorParcela;
            somaParcelasAnteriores += valor;
            var dataParcela = dto.Data.AddMonths(i);

            var (registro, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, conta, dataParcela);
            registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas)
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Descricao = $"{dto.Descricao} ({i + 1}/{dto.QuantidadeParcelas})",
                    Valor = valor,
                    Categoria = dto.Categoria,
                    TipoCusto = tipoCusto,
                    ParcelamentoId = parcelamentoId,
                    NumeroParcela = i + 1,
                    TotalParcelas = dto.QuantidadeParcelas,
                    OrigemMetaId = origemMetaId,
                },
            };
            registro.SaldoFinal -= valor;
            registro.SalvoEm = DateTime.UtcNow;
            await RegistroDiaHelper.PersistirAsync(_registroRepo, registro, novo);

            // Mesma garantia do Caixa manual (item 2): se já havia algum registro futuro nesta
            // conta depois desta parcela, o Inicio/SaldoFinal dele precisa refletir o novo saldo.
            var registrosDaConta = await _registroRepo.ListarPorContaAsync(contaCartaoId);
            await RegistroService.RecalcularDiasSeguintesAsync(
                _registroRepo, contaCartaoId, dataParcela, registro.SaldoFinal, registrosDaConta);
        }

        await _auditService.LogAsync(conta.ClienteId, usuarioLogadoId, "ItemFinanceiroSaida", "LancarParcelado",
            parcelamentoId.ToString(), null,
            JsonSerializer.Serialize(new { dto.Descricao, dto.ValorTotal, dto.QuantidadeParcelas, dto.Data, dto.Categoria }));
    }

    private async Task<List<FaturaCartaoDto>> ListarFaturasInternoAsync(ContaBancaria contaCartao)
    {
        var registros = (await _registroRepo.ListarPorContaAsync(contaCartao.Id)).Where(r => !r.Excluido).ToList();
        var pagamentos = await _pagamentoRepo.ListarPorContaCartaoAsync(contaCartao.Id);
        var faturas = await _faturaRepo.ListarPorContaAsync(contaCartao.Id);
        return CalcularFaturas(contaCartao, registros, pagamentos, faturas, DataLocalHelper.Hoje());
    }

    // Extraído de ListarFaturasInternoAsync pra reuso fora deste service (item 4 da rodada "Visão
    // de Futuro": a Projeção precisa das mesmas faturas, sem repetir o cálculo de competência/ciclo
    // nem depender deste service inteiro, que faz checagem de acesso por requisição HTTP).
    //
    // Cartão (Bloco 3/4, Money Vision): agrupa primeiro pela Fatura REAL (ItemFinanceiroSaida.
    // FaturaId) — cada arquivo importado é uma fatura só, nunca mais partida pela competência
    // inferida. Compras sem FaturaId (parcelas lançadas direto no cartão, ainda sem documento real)
    // caem num grupo "virtual" por competência inferida, como antes — é só projeção até a fatura
    // real do banco chegar.
    public static List<FaturaCartaoDto> CalcularFaturas(
        ContaBancaria contaCartao, List<RegistroDiario> registrosDaConta, List<PagamentoFatura> pagamentosDaConta,
        List<Fatura> faturasReaisDaConta, DateOnly hoje)
    {
        var comprasPorFaturaId = new Dictionary<Guid, (decimal Total, int Qtd)>();
        var comprasVirtuaisPorCompetencia = new Dictionary<string, (decimal Total, int Qtd)>();

        foreach (var r in registrosDaConta)
        {
            foreach (var s in r.Saidas.Where(s => s.TipoCusto != LancamentoFiltro.TipoPagamentoFatura))
            {
                if (s.FaturaId.HasValue)
                {
                    var atual = comprasPorFaturaId.GetValueOrDefault(s.FaturaId.Value, (Total: 0m, Qtd: 0));
                    comprasPorFaturaId[s.FaturaId.Value] = (atual.Total + s.Valor, atual.Qtd + 1);
                }
                else
                {
                    var competencia = ObterCompetencia(r.Data, contaCartao.DiaFechamento);
                    var atual = comprasVirtuaisPorCompetencia.GetValueOrDefault(competencia, (Total: 0m, Qtd: 0));
                    comprasVirtuaisPorCompetencia[competencia] = (atual.Total + s.Valor, atual.Qtd + 1);
                }
            }
        }

        var pagoPorFaturaId = pagamentosDaConta.Where(p => p.FaturaId.HasValue)
            .GroupBy(p => p.FaturaId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.ValorPago));
        var pagoVirtualPorCompetencia = pagamentosDaConta.Where(p => !p.FaturaId.HasValue)
            .GroupBy(p => p.Competencia)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.ValorPago));

        var resultado = new List<FaturaCartaoDto>();

        var idsComMovimento = comprasPorFaturaId.Keys.Union(pagoPorFaturaId.Keys).Distinct();
        foreach (var faturaId in idsComMovimento)
        {
            var fatura = faturasReaisDaConta.FirstOrDefault(f => f.Id == faturaId);
            if (fatura == null) continue; // pagamento órfão (fatura real removida) — não deveria acontecer, mas não quebra a listagem.

            var (total, qtd) = comprasPorFaturaId.GetValueOrDefault(faturaId, (0m, 0));
            var pago = pagoPorFaturaId.GetValueOrDefault(faturaId, 0m);
            // Sem DataFechamento informado, aproxima pelo mês anterior ao vencimento (mesma relação
            // de 1 mês usada em ObterDatasCiclo pro grupo virtual).
            var fechamento = fatura.DataFechamento ?? fatura.DataVencimento.AddMonths(-1);
            resultado.Add(new FaturaCartaoDto
            {
                Id = fatura.Id,
                Banco = fatura.Banco,
                Competencia = fatura.Competencia,
                DataFechamento = fechamento,
                DataVencimento = fatura.DataVencimento,
                ValorTotal = total,
                ValorPago = pago,
                SaldoDevedor = Math.Max(0m, total - pago),
                QuantidadeCompras = qtd,
                Status = CalcularStatus(hoje, fechamento, total, pago),
            });
        }

        var competenciasVirtuais = comprasVirtuaisPorCompetencia.Keys.Union(pagoVirtualPorCompetencia.Keys).Distinct();
        foreach (var competencia in competenciasVirtuais)
        {
            var (total, qtd) = comprasVirtuaisPorCompetencia.GetValueOrDefault(competencia, (0m, 0));
            var pago = pagoVirtualPorCompetencia.GetValueOrDefault(competencia, 0m);
            var (fechamento, vencimento) = ObterDatasCiclo(competencia, contaCartao.DiaFechamento, contaCartao.DiaVencimento);
            resultado.Add(new FaturaCartaoDto
            {
                Id = null,
                Banco = null,
                Competencia = competencia,
                DataFechamento = fechamento,
                DataVencimento = vencimento,
                ValorTotal = total,
                ValorPago = pago,
                SaldoDevedor = Math.Max(0m, total - pago),
                QuantidadeCompras = qtd,
                Status = CalcularStatus(hoje, fechamento, total, pago),
            });
        }

        return resultado.OrderByDescending(f => f.DataVencimento).ToList();
    }

    private static string CalcularStatus(DateOnly hoje, DateOnly fechamento, decimal total, decimal pago)
    {
        if (hoje < fechamento) return "Aberta";
        var saldoDevedor = total - pago;
        if (saldoDevedor <= ToleranciaQuitacao) return "Paga";
        return pago > ToleranciaQuitacao ? "Parcial" : "Fechada";
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
        FaturaId = p.FaturaId,
        ValorPago = p.ValorPago,
        Data = p.Data,
        CriadoEm = p.CriadoEm,
    };
}
