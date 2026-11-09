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

    public ConciliacaoSaldoService(
        IContaBancariaRepository contaRepo, IRegistroRepository registroRepo, ITransacaoImportadaRepository importRepo,
        IPontoConciliacaoRepository pontoRepo, IPeriodoExtratoRepository periodoRepo, TimeProvider? timeProvider = null)
    {
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
        var saldo = saldoBanco ?? pontos.FirstOrDefault(p => p.Data == data)?.SaldoBanco
            ?? throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe o saldo do banco nesta data.", "saldoBanco");

        var registrosCliente = await _registroRepo.ListarPorClienteAsync(conta.ClienteId);
        var registrosDaConta = registrosCliente.Where(r => r.ContaBancariaId == contaId && !r.Excluido).ToList();
        var importadas = await _importRepo.ListarPorContaAsync(contaId);
        var chaves = importadas.Where(t => t.FitId == null)
            // A auditoria grava o valor sem sinal (o sentido vai em Tipo).
            .Select(t => ConciliacaoSaldoCalculo.ChaveImportada(t.Data, t.Tipo == "Saida" ? -Math.Abs(t.Valor) : Math.Abs(t.Valor), t.Descricao))
            .ToHashSet();
        var dias = ConciliacaoSaldoCalculo.DiasComExtrato(await _periodoRepo.ListarPorContaAsync(contaId), importadas, registrosDaConta);

        return ConciliacaoSaldoCalculo.Diagnosticar(conta, registrosCliente, pontos, dias, chaves, data, saldo);
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

        var pontosDoCliente = await _pontoRepo.ListarPorClienteAsync(contas[0].ClienteId);
        foreach (var conta in contas)
        {
            var daConta = registros.Where(r => r.ContaBancariaId == conta.Id && !r.Excluido).ToList();
            var dias = await DiasComExtratoAsync(conta.Id, daConta);
            resultado[conta.Id] = ConciliacaoSaldoCalculo.CalcularStatus(
                conta, daConta, pontosDoCliente.Where(p => p.ContaBancariaId == conta.Id).ToList(), dias, Hoje());
        }
        return resultado;
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
