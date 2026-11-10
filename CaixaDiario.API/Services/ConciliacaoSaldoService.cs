using CaixaDiario.API.DTOs.ConciliacaoSaldo;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public interface IConciliacaoSaldoService
{
    Task<CoberturaExtratoDto> ObterCoberturaAsync(Guid contaId, Guid usuarioLogadoId, string perfil);
    Task<DiagnosticoDiferencaDto> DiagnosticarAsync(Guid contaId, DateOnly data, decimal? saldoBanco, Guid usuarioLogadoId, string perfil);
    // "Saldo no banco em 07/11" digitado pelo usuário (prévia da importação ou tela da conta).
    Task<PontoConciliacaoDto> RegistrarSaldoBancoAsync(Guid contaId, RegistrarSaldoBancoDto dto, Guid usuarioLogadoId, string perfil);
    // Uso interno (importação/ajuste) — sem checagem de acesso, quem chama já checou.
    Task<PontoConciliacaoDto> RegistrarPontoAsync(ContaBancaria conta, DateOnly data, decimal saldoBanco, string origem);
    Task<Dictionary<Guid, StatusConciliacaoDto>> ObterStatusPorContaAsync(List<ContaBancaria> contas, List<RegistroDiario> registros);
    // "Conferir saldo": ajustes da conta pra confirmar (fica) ou desfazer (exclusão comum do lançamento).
    Task<List<AjusteSaldoDto>> ListarAjustesAsync(Guid contaId, Guid usuarioLogadoId, string perfil);
    Task ConfirmarAjusteAsync(Guid contaId, Guid lancamentoId, Guid usuarioLogadoId, string perfil);
}

// Money Vision ("até onde está lançado" + conciliação sem ajuste toda hora). Toda a matemática
// fica em ConciliacaoSaldoCalculo; aqui só se carrega os dados de cada conta.
public class ConciliacaoSaldoService : IConciliacaoSaldoService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly ITransacaoImportadaRepository _importRepo;
    private readonly IPontoConciliacaoRepository _pontoRepo;
    private readonly IPeriodoExtratoRepository _periodoRepo;
    private readonly TimeProvider _timeProvider;
    private readonly IDuplicataDispensadaRepository? _dispensadaRepo;
    private readonly ILogger<ConciliacaoSaldoService>? _logger;

    public ConciliacaoSaldoService(
        IContaBancariaRepository contaRepo, IRegistroRepository registroRepo, ITransacaoImportadaRepository importRepo,
        IPontoConciliacaoRepository pontoRepo, IPeriodoExtratoRepository periodoRepo, TimeProvider? timeProvider = null,
        IDuplicataDispensadaRepository? dispensadaRepo = null, ILogger<ConciliacaoSaldoService>? logger = null)
    {
        _dispensadaRepo = dispensadaRepo;
        _logger = logger;
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _importRepo = importRepo;
        _pontoRepo = pontoRepo;
        _periodoRepo = periodoRepo;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private DateOnly Hoje() => DataLocalHelper.Hoje(_timeProvider.GetUtcNow().UtcDateTime);

    public async Task<CoberturaExtratoDto> ObterCoberturaAsync(Guid contaId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await ObterContaComAcessoAsync(contaId, usuarioLogadoId, perfil);
        var registros = (await _registroRepo.ListarPorContaAsync(contaId)).Where(r => !r.Excluido).ToList();
        var pontos = await _pontoRepo.ListarPorContaAsync(contaId);
        var dias = await DiasComExtratoAsync(contaId, registros);

        return new CoberturaExtratoDto
        {
            DiasComExtrato = dias.ToList(),
            Pontos = pontos.Select(p => ConciliacaoSaldoCalculo.AvaliarPonto(conta, registros, p)).ToList(),
            SaldosPorDia = registros.OrderBy(r => r.Data).Select(r => new SaldoDoDiaDto { Data = r.Data, SaldoFinal = r.SaldoFinal }).ToList(),
            Status = ConciliacaoSaldoCalculo.CalcularStatus(conta, registros, pontos, dias, Hoje()),
        };
    }

    public async Task<DiagnosticoDiferencaDto> DiagnosticarAsync(
        Guid contaId, DateOnly data, decimal? saldoBanco, Guid usuarioLogadoId, string perfil)
    {
        var conta = await ObterContaComAcessoAsync(contaId, usuarioLogadoId, perfil);
        var pontos = await _pontoRepo.ListarPorContaAsync(contaId);
        var saldo = saldoBanco ?? pontos.FirstOrDefault(p => p.Data == data)?.SaldoBanco;

        var registrosCliente = await _registroRepo.ListarPorClienteAsync(conta.ClienteId);
        var registrosDaConta = registrosCliente.Where(r => r.ContaBancariaId == contaId && !r.Excluido).ToList();
        var importadas = await _importRepo.ListarPorContaAsync(contaId);
        var chaves = importadas.Where(t => t.FitId == null)
            // A auditoria grava o valor sem sinal (o sentido vai em Tipo).
            .Select(t => ConciliacaoSaldoCalculo.ChaveImportada(t.Data, t.Tipo == "Saida" ? -Math.Abs(t.Valor) : Math.Abs(t.Valor), t.Descricao))
            .ToHashSet();
        var dias = ConciliacaoSaldoCalculo.DiasComExtrato(await _periodoRepo.ListarPorContaAsync(contaId), importadas, registrosDaConta);

        var paresMantidos = _dispensadaRepo == null ? new HashSet<(Guid, Guid)>()
            : (await _dispensadaRepo.ListarPorContaAsync(contaId)).Select(d => (d.LancamentoMenorId, d.LancamentoMaiorId)).ToHashSet();

        return ConciliacaoSaldoCalculo.Diagnosticar(conta, registrosCliente, pontos, dias, chaves, data, saldo, paresMantidos);
    }

    public async Task<PontoConciliacaoDto> RegistrarSaldoBancoAsync(Guid contaId, RegistrarSaldoBancoDto dto, Guid usuarioLogadoId, string perfil)
    {
        var conta = await ObterContaComAcessoAsync(contaId, usuarioLogadoId, perfil);
        if (dto.Data > Hoje())
            throw new ApiException(400, CodigoRetorno.DATA_FUTURA, "Não é possível informar saldo de data futura.", "data");
        return await RegistrarPontoAsync(conta, dto.Data, dto.SaldoBanco, "Informado");
    }

    public async Task<PontoConciliacaoDto> RegistrarPontoAsync(ContaBancaria conta, DateOnly data, decimal saldoBanco, string origem)
    {
        var registros = (await _registroRepo.ListarPorContaAsync(conta.Id)).Where(r => !r.Excluido).ToList();
        var ponto = await _pontoRepo.SalvarAsync(new PontoConciliacao
        {
            ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Data = data, SaldoBanco = saldoBanco,
            SaldoApp = SaldoEncadeamento.SaldoNoFimDe(conta, registros, data), Origem = origem,
        });
        return ConciliacaoSaldoCalculo.AvaliarPonto(conta, registros, ponto);
    }

    public async Task<Dictionary<Guid, StatusConciliacaoDto>> ObterStatusPorContaAsync(List<ContaBancaria> contas, List<RegistroDiario> registros)
    {
        var resultado = new Dictionary<Guid, StatusConciliacaoDto>();
        if (contas.Count == 0) return resultado;

        // A listagem de contas nunca pode cair por causa da conciliação (deploy de 09/11: tabela
        // nova sem migration → 500 em tudo). Sem os pontos, cada conta sai só sem esse bloco.
        List<PontoConciliacao> pontosDoCliente;
        try
        {
            pontosDoCliente = await _pontoRepo.ListarPorClienteAsync(contas[0].ClienteId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Conciliação indisponível para o cliente {ClienteId} — contas listadas sem status.", contas[0].ClienteId);
            return resultado;
        }

        foreach (var conta in contas)
        {
            try
            {
                var daConta = registros.Where(r => r.ContaBancariaId == conta.Id && !r.Excluido).ToList();
                var dias = await DiasComExtratoAsync(conta.Id, daConta);
                resultado[conta.Id] = ConciliacaoSaldoCalculo.CalcularStatus(
                    conta, daConta, pontosDoCliente.Where(p => p.ContaBancariaId == conta.Id).ToList(), dias, Hoje());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Falha ao calcular a conciliação da conta {ContaId} — conta listada sem status.", conta.Id);
            }
        }
        return resultado;
    }

    public async Task<List<AjusteSaldoDto>> ListarAjustesAsync(Guid contaId, Guid usuarioLogadoId, string perfil)
    {
        await ObterContaComAcessoAsync(contaId, usuarioLogadoId, perfil);
        var registros = (await _registroRepo.ListarPorContaAsync(contaId)).Where(r => !r.Excluido).OrderBy(r => r.Data);

        return registros.SelectMany(r =>
                r.Entradas.Where(e => LancamentoFiltro.PareceAjusteConciliacao(e.TipoCusto, e.Categoria, e.Descricao))
                    .Select(e => Ajuste(r, e.Id, e.Descricao, e.Valor, e.AjusteConfirmado))
                .Concat(r.Saidas.Where(s => LancamentoFiltro.PareceAjusteConciliacao(s.TipoCusto, s.Categoria, s.Descricao))
                    .Select(s => Ajuste(r, s.Id, s.Descricao, -s.Valor, s.AjusteConfirmado))))
            .ToList();

        static AjusteSaldoDto Ajuste(RegistroDiario r, Guid id, string descricao, decimal valor, bool confirmado) => new()
        {
            Id = id, Data = r.Data, Descricao = descricao, Valor = valor,
            SaldoDoDia = r.SaldoFinal, SaldoDoDiaSemAjuste = r.SaldoFinal - valor, Confirmado = confirmado,
        };
    }

    public async Task ConfirmarAjusteAsync(Guid contaId, Guid lancamentoId, Guid usuarioLogadoId, string perfil)
    {
        await ObterContaComAcessoAsync(contaId, usuarioLogadoId, perfil);
        foreach (var r in (await _registroRepo.ListarPorContaAsync(contaId)).Where(r => !r.Excluido))
        {
            var entrada = r.Entradas.FirstOrDefault(e => e.Id == lancamentoId && LancamentoFiltro.PareceAjusteConciliacao(e.TipoCusto, e.Categoria, e.Descricao));
            var saida = r.Saidas.FirstOrDefault(s => s.Id == lancamentoId && LancamentoFiltro.PareceAjusteConciliacao(s.TipoCusto, s.Categoria, s.Descricao));
            if (entrada == null && saida == null) continue;

            if (entrada != null) entrada.AjusteConfirmado = true;
            if (saida != null) saida.AjusteConfirmado = true;
            r.Entradas = new List<ItemFinanceiro>(r.Entradas); // força o EF a detectar a mudança (jsonb)
            r.Saidas = new List<ItemFinanceiroSaida>(r.Saidas);
            r.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(r);
            return;
        }
        throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Ajuste de saldo não encontrado nesta conta.");
    }

    private async Task<SortedSet<DateOnly>> DiasComExtratoAsync(Guid contaId, List<RegistroDiario> registrosDaConta) =>
        ConciliacaoSaldoCalculo.DiasComExtrato(
            await _periodoRepo.ListarPorContaAsync(contaId), await _importRepo.ListarPorContaAsync(contaId), registrosDaConta);

    private async Task<ContaBancaria> ObterContaComAcessoAsync(Guid contaId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (perfil == "cliente" && usuarioLogadoId != conta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return conta;
    }
}
