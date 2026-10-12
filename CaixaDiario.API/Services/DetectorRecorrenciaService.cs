using CaixaDiario.API.DTOs.ContasRecorrentes;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

/// <summary>
/// Item 2 do Bloco 4 ("Visão de Futuro"): analisa o histórico de Entradas/Saídas (últimos 3 meses
/// fechados + o atual) procurando descrições que se repetem em pelo menos 3 meses distintos com
/// valor parecido (±30%) e que AINDA não têm uma ContaRecorrente correspondente — candidatas a
/// "isso parece uma conta recorrente, quer cadastrar?". Mesmo espírito de ConciliacaoService
/// (motor de detecção + "ignorar" persistido), só que o padrão aqui é a descrição normalizada +
/// tipo, não um par título×lançamento.
/// </summary>
public class DetectorRecorrenciaService : IDetectorRecorrenciaService
{
    // Em pelo menos 3 meses civis distintos pra ser considerado "recorrente" — 1 ou 2 ocorrências
    // isoladas são coincidência (ex.: duas compras parecidas no mesmo mês).
    private const int MesesMinimos = 3;
    // Valores dentro de ±30% da média — fora disso, provavelmente não é a mesma conta recorrendo
    // (ex.: conta de luz que varia MUITO mês a mês não serve de candidata confiável).
    private const decimal ToleranciaValor = 0.30m;

    private readonly IRegistroRepository _registroRepo;
    private readonly IContaRecorrenteRepository _recorrenteRepo;
    private readonly ISugestaoRecorrenciaIgnoradaRepository _ignoradaRepo;

    public DetectorRecorrenciaService(
        IRegistroRepository registroRepo, IContaRecorrenteRepository recorrenteRepo,
        ISugestaoRecorrenciaIgnoradaRepository ignoradaRepo)
    {
        _registroRepo = registroRepo;
        _recorrenteRepo = recorrenteRepo;
        _ignoradaRepo = ignoradaRepo;
    }

    public async Task<List<SugestaoRecorrenciaDto>> ListarSugestoesAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);

        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _recorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var ignoradas = await _ignoradaRepo.ListarPorClienteAsync(clienteId);
        var chavesIgnoradas = ignoradas.Select(i => i.ChaveNormalizada).ToHashSet();

        return Detectar(registros, recorrentes, chavesIgnoradas, DataLocalHelper.Hoje());
    }

    public async Task IgnorarAsync(Guid clienteId, string chave, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        if (string.IsNullOrWhiteSpace(chave))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Chave é obrigatória.", "chave");

        await _ignoradaRepo.AdicionarAsync(new SugestaoRecorrenciaIgnorada
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ChaveNormalizada = chave, CriadoEm = DateTime.UtcNow,
        });
    }

    private static List<SugestaoRecorrenciaDto> Detectar(
        List<RegistroDiario> registros, List<ContaRecorrente> recorrentes, HashSet<string> chavesIgnoradas, DateOnly hoje)
    {
        var desde = hoje.AddMonths(-3);
        var recentes = registros.Where(r => r.Data >= desde && r.Data <= hoje && !r.Excluido).ToList();

        var candidatosReceber = recentes
            .SelectMany(r => r.Entradas
                .Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto) && !string.IsNullOrWhiteSpace(e.Categoria))
                .Select(e => new CandidatoLancamento(r.Data, e.Descricao, e.Categoria, e.Valor, r.ContaBancariaId)))
            .ToList();
        var candidatosPagar = recentes
            .SelectMany(r => r.Saidas
                .Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto) && !string.IsNullOrWhiteSpace(s.Categoria))
                .Select(s => new CandidatoLancamento(r.Data, s.Descricao, s.Categoria, s.Valor, r.ContaBancariaId)))
            .ToList();

        var resultado = new List<SugestaoRecorrenciaDto>();
        resultado.AddRange(DetectarPorTipo(candidatosReceber, "Receber", recorrentes, chavesIgnoradas));
        resultado.AddRange(DetectarPorTipo(candidatosPagar, "Pagar", recorrentes, chavesIgnoradas));
        return resultado.OrderByDescending(s => s.QuantidadeOcorrencias).ThenBy(s => s.Descricao).ToList();
    }

    private static List<SugestaoRecorrenciaDto> DetectarPorTipo(
        List<CandidatoLancamento> candidatos, string tipo, List<ContaRecorrente> recorrentes, HashSet<string> chavesIgnoradas)
    {
        var recorrentesDoTipo = recorrentes.Where(r => r.Tipo == tipo).ToList();

        var grupos = candidatos
            .GroupBy(c => DescricaoMatcher.NormalizarExata(c.Descricao))
            .Where(g => g.Select(c => (c.Data.Year, c.Data.Month)).Distinct().Count() >= MesesMinimos);

        var resultado = new List<SugestaoRecorrenciaDto>();
        foreach (var grupo in grupos)
        {
            var chave = MontarChave(tipo, grupo.Key);
            if (chavesIgnoradas.Contains(chave)) continue;

            // Já existe recorrência cobrindo essa descrição (mesma normalização)? Nunca sugerir de
            // novo o que o cliente já cadastrou.
            if (recorrentesDoTipo.Any(r => DescricaoMatcher.NormalizarExata(r.Descricao) == grupo.Key)) continue;

            var valores = grupo.Select(c => c.Valor).ToList();
            var media = valores.Average();
            if (media <= 0) continue;
            var maiorDesvio = valores.Max(v => Math.Abs(v - media)) / media;
            if (maiorDesvio > ToleranciaValor) continue;

            var ultimo = grupo.OrderByDescending(c => c.Data).First();
            resultado.Add(new SugestaoRecorrenciaDto
            {
                Chave = chave,
                Descricao = ultimo.Descricao,
                Categoria = ultimo.Categoria,
                Tipo = tipo,
                ValorMedio = Math.Round(media, 2),
                QuantidadeOcorrencias = grupo.Select(c => (c.Data.Year, c.Data.Month)).Distinct().Count(),
                ContaBancariaId = ultimo.ContaBancariaId,
                UltimaOcorrencia = ultimo.Data,
            });
        }
        return resultado;
    }

    private static string MontarChave(string tipo, string descricaoNormalizada) => $"{tipo}:{descricaoNormalizada}";

    private record CandidatoLancamento(DateOnly Data, string Descricao, string? Categoria, decimal Valor, Guid? ContaBancariaId);

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }
}
