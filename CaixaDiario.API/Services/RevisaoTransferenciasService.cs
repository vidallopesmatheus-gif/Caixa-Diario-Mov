using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public interface IRevisaoTransferenciasService
{
    Task<RevisaoTransferenciasDto> ListarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    // Cliente: corrige as contrapartidas do próprio cliente. Admin (clienteId nulo): todos.
    Task<CorrecaoTransferenciasResultDto> CorrigirAsync(Guid? clienteId, bool confirmar);
    Task<CorrecaoTransferenciasResultDto> CorrigirDoClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    // "Foi para essa conta mesmo" — a contrapartida sem extrato sai da revisão e do diagnóstico.
    Task ConfirmarContrapartidaAsync(Guid transferenciaId, Guid usuarioLogadoId, string perfil);
    // "Não foi para essa conta" — apaga a contrapartida inventada e devolve o lançamento real pra
    // categorizar (ou classificar como transferência pra outra conta).
    Task DescartarContrapartidaAsync(Guid transferenciaId, Guid usuarioLogadoId, string perfil);
    // Uso interno (diagnóstico de saldo) — sem checagem de acesso, quem chama já checou.
    Task<List<AchadoContrapartida>> AnalisarAsync(Guid clienteId);
}

// Money Vision ("transferência cria contrapartida falsa"): repara as contrapartidas que o app
// inventou antes de procurar o par real (13 pares Nubank ↔ C6 do cliente teste). Ver
// RevisaoContrapartidas.Analisar pra regra de cada caso.
public class RevisaoTransferenciasService : IRevisaoTransferenciasService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly ITransferenciaRepository _transferenciaRepo;
    private readonly ITransacaoImportadaRepository _importRepo;
    private readonly IPeriodoExtratoRepository _periodoRepo;
    private readonly ITransferenciaService? _transferenciaService;

    public RevisaoTransferenciasService(
        IContaBancariaRepository contaRepo, IRegistroRepository registroRepo, ITransferenciaRepository transferenciaRepo,
        ITransacaoImportadaRepository importRepo, IPeriodoExtratoRepository periodoRepo, ITransferenciaService? transferenciaService = null)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _transferenciaRepo = transferenciaRepo;
        _importRepo = importRepo;
        _periodoRepo = periodoRepo;
        _transferenciaService = transferenciaService;
    }

    private sealed record DadosCliente(
        List<ContaBancaria> Contas, List<RegistroDiario> Registros, List<Transferencia> Transferencias, List<AchadoContrapartida> Achados);

    private async Task<DadosCliente> CarregarAsync(Guid clienteId)
    {
        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var registros = (await _registroRepo.ListarPorClienteAsync(clienteId)).Where(r => !r.Excluido).ToList();
        var transferencias = await _transferenciaRepo.ListarPorClienteAsync(clienteId);
        var dias = new Dictionary<Guid, SortedSet<DateOnly>>();
        var chaves = new Dictionary<Guid, HashSet<(DateOnly, decimal, string)>>();
        foreach (var conta in contas)
        {
            var importadas = await _importRepo.ListarPorContaAsync(conta.Id);
            var periodos = await _periodoRepo.ListarPorContaAsync(conta.Id);
            dias[conta.Id] = ConciliacaoSaldoCalculo.DiasComExtrato(periodos, importadas, registros.Where(r => r.ContaBancariaId == conta.Id));
            chaves[conta.Id] = ChavesImportadas(importadas);
        }
        return new DadosCliente(contas, registros, transferencias, RevisaoContrapartidas.Analisar(registros, transferencias, dias, chaves));
    }

    // A auditoria grava o valor sem sinal (o sentido vai em Tipo).
    public static HashSet<(DateOnly, decimal, string)> ChavesImportadas(IEnumerable<TransacaoImportada> importadas) =>
        importadas.Where(t => t.Status != "Ignorada")
            .Select(t => ConciliacaoSaldoCalculo.ChaveImportada(t.Data, t.Tipo == "Saida" ? -Math.Abs(t.Valor) : Math.Abs(t.Valor), t.Descricao))
            .ToHashSet();

    public async Task<List<AchadoContrapartida>> AnalisarAsync(Guid clienteId) => (await CarregarAsync(clienteId)).Achados;

    public async Task<RevisaoTransferenciasDto> ListarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var dados = await CarregarAsync(clienteId);
        var nomes = dados.Contas.ToDictionary(c => c.Id, c => c.Nome);

        return new RevisaoTransferenciasDto
        {
            Corrigiveis = dados.Achados.Where(PrecisaCorrigir).Select(a => Item(clienteId, a, nomes)).ToList(),
            ParaDecidir = dados.Achados.Where(a => a.Tipo == TipoAchadoContrapartida.SemLancamentoNoExtrato)
                .Select(a => new TransferenciaParaRevisarDto
                {
                    TransferenciaId = a.Transferencia.Id, Data = a.Criada.Data, Valor = a.Criada.ValorComSinal,
                    ContaCriadaId = a.Criada.ContaId, ContaCriadaNome = nomes.GetValueOrDefault(a.Criada.ContaId, "—"),
                    ContaOutraNome = a.OutraPonta != null ? nomes.GetValueOrDefault(a.OutraPonta.ContaId, "—") : "—",
                    DescricaoOriginal = a.OutraPonta?.Descricao ?? a.Transferencia.Descricao ?? string.Empty,
                })
                .ToList(),
        };
    }

    // Sem lançamento no extrato só entra na correção pra virar provisória com descrição própria
    // (aí "não foi para essa conta" apaga ela de verdade) — a decisão continua com o usuário.
    private static bool PrecisaCorrigir(AchadoContrapartida a) =>
        a.Tipo is TipoAchadoContrapartida.DuplicadaComReal or TipoAchadoContrapartida.ParDuplicado
        || !a.Criada.Provisoria || !a.Criada.Descricao.StartsWith(ContrapartidaAnalise.PrefixoDescricao, StringComparison.Ordinal);

    public Task<CorrecaoTransferenciasResultDto> CorrigirDoClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        return CorrigirAsync(clienteId, confirmar: true);
    }

    public async Task<CorrecaoTransferenciasResultDto> CorrigirAsync(Guid? clienteId, bool confirmar)
    {
        var resultado = new CorrecaoTransferenciasResultDto { Confirmado = confirmar };
        var clientes = clienteId.HasValue
            ? new List<Guid> { clienteId.Value }
            : (await _contaRepo.ListarTodasAsync()).Select(c => c.ClienteId).Distinct().ToList();

        foreach (var cliente in clientes)
        {
            var dados = await CarregarAsync(cliente);
            var achados = dados.Achados.Where(PrecisaCorrigir).ToList();
            if (achados.Count == 0) continue;
            var nomes = dados.Contas.ToDictionary(c => c.Id, c => c.Nome);
            resultado.Itens.AddRange(achados.Select(a => Item(cliente, a, nomes)));

            var saldosAntes = dados.Contas.ToDictionary(c => c.Id, c => SaldoEncadeamento.SaldoAtual(c, dados.Registros.Where(r => r.ContaBancariaId == c.Id)));
            // Efeito no saldo de cada conta = tirar as pontas inventadas que viram duplicata.
            var removidas = achados.SelectMany(a => a.Tipo switch
            {
                TipoAchadoContrapartida.DuplicadaComReal => new[] { a.Criada },
                TipoAchadoContrapartida.ParDuplicado => new[] { a.Criada, a.OutraCriada! },
                _ => Array.Empty<PontaLancamento>(),
            }).ToList();

            if (confirmar)
            {
                foreach (var a in achados) Aplicar(a, nomes);
                foreach (var t in achados.Where(a => a.Tipo == TipoAchadoContrapartida.ParDuplicado).Select(a => a.OutraTransferencia!).DistinctBy(t => t.Id))
                    await _transferenciaRepo.RemoverAsync(t);
            }

            var contasAfetadas = achados.SelectMany(a => new[] { a.Criada.ContaId, a.OutraCriada?.ContaId ?? a.Criada.ContaId }).Distinct();
            foreach (var conta in dados.Contas.Where(c => contasAfetadas.Contains(c.Id)))
            {
                var daConta = dados.Registros.Where(r => r.ContaBancariaId == conta.Id).OrderBy(r => r.Data).ToList();
                if (confirmar)
                    foreach (var registro in SaldoEncadeamento.Encadear(conta.SaldoInicial, daConta))
                    {
                        registro.Entradas = new List<ItemFinanceiro>(registro.Entradas); // força o EF a detectar a mudança (jsonb)
                        registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
                        await _registroRepo.AtualizarAsync(registro);
                    }
                resultado.Saldos.Add(new SaldoContaCorrecaoDto
                {
                    ContaBancariaId = conta.Id, ContaBancariaNome = conta.Nome, SaldoAtualAntes = saldosAntes[conta.Id],
                    SaldoAtualDepois = saldosAntes[conta.Id] - removidas.Where(p => p.ContaId == conta.Id).Sum(p => p.ValorComSinal),
                });
            }
        }
        return resultado;
    }

    public async Task ConfirmarContrapartidaAsync(Guid transferenciaId, Guid usuarioLogadoId, string perfil)
    {
        var achado = await AchadoParaDecidirAsync(transferenciaId, usuarioLogadoId, perfil);
        var registro = achado.Criada.Registro;
        foreach (var e in registro.Entradas.Where(e => e.Id == achado.Criada.ItemId)) e.ContrapartidaConfirmada = true;
        foreach (var s in registro.Saidas.Where(s => s.Id == achado.Criada.ItemId)) s.ContrapartidaConfirmada = true;
        registro.Entradas = new List<ItemFinanceiro>(registro.Entradas);
        registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
        registro.SalvoEm = DateTime.UtcNow;
        await _registroRepo.AtualizarAsync(registro);
    }

    public async Task DescartarContrapartidaAsync(Guid transferenciaId, Guid usuarioLogadoId, string perfil)
    {
        var achado = await AchadoParaDecidirAsync(transferenciaId, usuarioLogadoId, perfil);
        if (_transferenciaService == null) throw new InvalidOperationException("ITransferenciaService não registrado.");

        // Provisória: o desfazer apaga a ponta inventada em vez de deixá-la "sem categoria" (contrapartidas
        // antigas não tinham a marca) e só tira o rótulo da ponta real.
        var registro = achado.Criada.Registro;
        foreach (var e in registro.Entradas.Where(e => e.Id == achado.Criada.ItemId)) e.Provisoria = true;
        foreach (var s in registro.Saidas.Where(s => s.Id == achado.Criada.ItemId)) s.Provisoria = true;
        registro.Entradas = new List<ItemFinanceiro>(registro.Entradas);
        registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
        await _registroRepo.AtualizarAsync(registro);
        await _transferenciaService.DesfazerClassificacaoAsync(transferenciaId, usuarioLogadoId, perfil);
    }

    private async Task<AchadoContrapartida> AchadoParaDecidirAsync(Guid transferenciaId, Guid usuarioLogadoId, string perfil)
    {
        var transferencia = await _transferenciaRepo.ObterPorIdAsync(transferenciaId)
            ?? throw new ApiException(404, CodigoRetorno.TRANSFERENCIA_NAO_ENCONTRADA, "Transferência não encontrada.");
        VerificarAcesso(transferencia.ClienteId, usuarioLogadoId, perfil);
        var dados = await CarregarAsync(transferencia.ClienteId);
        return dados.Achados.FirstOrDefault(a => a.Transferencia.Id == transferenciaId && a.Tipo == TipoAchadoContrapartida.SemLancamentoNoExtrato)
            ?? throw new ApiException(404, CodigoRetorno.TRANSFERENCIA_NAO_ENCONTRADA, "Esta transferência não está na revisão.");
    }

    private static void Aplicar(AchadoContrapartida a, Dictionary<Guid, string> nomes)
    {
        switch (a.Tipo)
        {
            case TipoAchadoContrapartida.DuplicadaComReal:
                // O lançamento real do extrato assume o vínculo; a cópia inventada sai.
                MarcarComoTransferencia(a.Real!, a.Transferencia.Id);
                Remover(a.Criada);
                break;
            case TipoAchadoContrapartida.ParDuplicado:
                // Dois pares pra mesma transferência: fica um só, com as duas pontas reais.
                MarcarComoTransferencia(a.Real!, a.Transferencia.Id);
                Remover(a.Criada);
                Remover(a.OutraCriada!);
                break;
            default:
                // Sem o lançamento real: vira provisória (a importação troca pela linha real; "não foi
                // pra essa conta" apaga) e para de copiar a descrição do outro banco.
                var outraConta = a.OutraPonta != null ? nomes.GetValueOrDefault(a.OutraPonta.ContaId, "outra conta") : "outra conta";
                var descricao = ContrapartidaAnalise.DescricaoContrapartida(a.Criada.Tipo, outraConta);
                foreach (var e in a.Criada.Registro.Entradas.Where(e => e.Id == a.Criada.ItemId)) { e.Provisoria = true; e.Descricao = descricao; }
                foreach (var s in a.Criada.Registro.Saidas.Where(s => s.Id == a.Criada.ItemId)) { s.Provisoria = true; s.Descricao = descricao; }
                a.Criada.Registro.SalvoEm = DateTime.UtcNow;
                break;
        }
    }

    private static void MarcarComoTransferencia(PontaLancamento p, Guid transferenciaId)
    {
        foreach (var e in p.Registro.Entradas.Where(e => e.Id == p.ItemId))
        {
            e.TransferenciaId = transferenciaId; e.TipoCusto = LancamentoFiltro.TipoTransferencia; e.Categoria = "Transferência";
            e.PendenteCategorizacao = false; e.CategoriaSugerida = false;
        }
        foreach (var s in p.Registro.Saidas.Where(s => s.Id == p.ItemId))
        {
            s.TransferenciaId = transferenciaId; s.TipoCusto = LancamentoFiltro.TipoTransferencia; s.Categoria = "Transferência";
            s.PendenteCategorizacao = false; s.CategoriaSugerida = false;
        }
        p.Registro.SalvoEm = DateTime.UtcNow;
    }

    private static void Remover(PontaLancamento p)
    {
        p.Registro.Entradas.RemoveAll(e => e.Id == p.ItemId);
        p.Registro.Saidas.RemoveAll(s => s.Id == p.ItemId);
        p.Registro.SalvoEm = DateTime.UtcNow;
    }

    private static ItemCorrecaoTransferenciaDto Item(Guid clienteId, AchadoContrapartida a, Dictionary<Guid, string> nomes) => new()
    {
        ClienteId = clienteId, TransferenciaId = a.Transferencia.Id, Tipo = a.Tipo.ToString(),
        ContaNome = nomes.GetValueOrDefault(a.Criada.ContaId, "—"), Data = a.Criada.Data, Valor = a.Criada.ValorComSinal,
        DescricaoCriada = a.Criada.Descricao, DescricaoReal = a.Real?.Descricao,
        Acao = a.Tipo switch
        {
            TipoAchadoContrapartida.DuplicadaComReal => "Trocar a contrapartida criada pelo lançamento real do extrato",
            TipoAchadoContrapartida.ParDuplicado => "Juntar os dois pares da mesma transferência em um",
            TipoAchadoContrapartida.AguardandoExtrato => "Marcar como provisória até o extrato chegar",
            _ => "Marcar como provisória (o extrato não tem esse lançamento)",
        },
    };

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }
}
