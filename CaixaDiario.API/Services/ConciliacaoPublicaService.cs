using CaixaDiario.API.DTOs.Importacao;
using CaixaDiario.API.DTOs.PortalConciliacao;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Único ponto do backend acessível sem login — cada método valida o token do link antes de
// tocar em qualquer dado do cliente. Nunca expõe saldo, DRE ou lançamentos já categorizados:
// só o necessário pra classificar o que está pendente (ver LinkConciliacao).
public class ConciliacaoPublicaService : IConciliacaoPublicaService
{
    private readonly ILinkConciliacaoRepository _linkRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaBancariaRepository _contaRepo;
    private readonly ICategoriaRepository _categoriaRepo;
    private readonly ICategoriaService _categoriaService;
    private readonly IRegraCategorizacaoRepository _regraRepo;

    public ConciliacaoPublicaService(
        ILinkConciliacaoRepository linkRepo,
        IRegistroRepository registroRepo,
        IContaBancariaRepository contaRepo,
        ICategoriaRepository categoriaRepo,
        ICategoriaService categoriaService,
        IRegraCategorizacaoRepository regraRepo)
    {
        _linkRepo = linkRepo;
        _registroRepo = registroRepo;
        _contaRepo = contaRepo;
        _categoriaRepo = categoriaRepo;
        _categoriaService = categoriaService;
        _regraRepo = regraRepo;
    }

    public async Task<PortalConciliacaoDto> ObterPendentesAsync(string token)
    {
        var link = await ObterLinkValidoAsync(token);
        link.UltimoAcessoEm = DateTime.UtcNow;
        await _linkRepo.AtualizarAsync(link);

        var registros = (await _registroRepo.ListarPorClienteAsync(link.ClienteId))
            .Where(r => !r.Excluido && r.ContaBancariaId.HasValue)
            .ToList();
        var nomesPorConta = (await _contaRepo.ListarPorClienteAsync(link.ClienteId))
            .ToDictionary(c => c.Id, c => c.Nome);

        // Só contas com pelo menos 1 pendência aparecem — conta em dia não entra no portal.
        var contas = registros
            .SelectMany(r => PendentesDoRegistro(r).Select(item => (r.ContaBancariaId!.Value, Item: item)))
            .GroupBy(x => x.Item1)
            .Select(g => new ContaPendentesDto
            {
                ContaBancariaId = g.Key,
                ContaNome = nomesPorConta.GetValueOrDefault(g.Key, "—"),
                Itens = g.Select(x => x.Item).OrderBy(i => i.Data).ToList(),
            })
            .OrderBy(c => c.ContaNome)
            .ToList();

        var categorias = await _categoriaService.ListarAgrupadasAsync();

        return new PortalConciliacaoDto
        {
            ExpiraEm = link.ExpiraEm,
            Contas = contas,
            CategoriasEntrada = categorias.Entradas,
            CategoriasSaida = categorias.Saidas,
        };
    }

    public async Task ClassificarAsync(string token, ClassificarPendentesDto dto)
    {
        var link = await ObterLinkValidoAsync(token);
        var categoriasPorNome = (await _categoriaRepo.ListarTodasAsync()).ToDictionary(c => c.Nome, c => c.Tipo);

        var porContaEData = dto.Itens
            .Where(i => DateOnly.TryParse(i.Data, out _))
            .GroupBy(i => (i.ContaBancariaId, Data: DateOnly.Parse(i.Data)));

        var totalClassificados = 0;
        foreach (var grupo in porContaEData)
        {
            var registro = await _registroRepo.ObterPorContaEDataAsync(grupo.Key.ContaBancariaId, grupo.Key.Data);
            // Confere que o registro é mesmo do cliente dono do link — defesa contra um
            // contaBancariaId de outro cliente vindo de um payload adulterado (endpoint sem login).
            if (registro == null || registro.ClienteId != link.ClienteId) continue;

            foreach (var item in grupo)
            {
                var saida = registro.Saidas.FirstOrDefault(s => s.Id == item.Id);
                if (saida != null)
                {
                    saida.Categoria = item.Categoria;
                    if (categoriasPorNome.TryGetValue(item.Categoria, out var tipoCustoSaida))
                        saida.TipoCusto = tipoCustoSaida;
                    saida.PendenteCategorizacao = false;
                    saida.CategoriaSugerida = false;
                    saida.ClassificadoPeloCliente = true;
                    totalClassificados++;
                    continue;
                }

                var entrada = registro.Entradas.FirstOrDefault(e => e.Id == item.Id);
                if (entrada == null) continue;
                entrada.Categoria = item.Categoria;
                if (categoriasPorNome.TryGetValue(item.Categoria, out var tipoCustoEntrada))
                    entrada.TipoCusto = tipoCustoEntrada;
                entrada.PendenteCategorizacao = false;
                entrada.CategoriaSugerida = false;
                entrada.ClassificadoPeloCliente = true;
                totalClassificados++;
            }

            // Reatribui as listas — Entradas/Saidas são jsonb sem value comparer configurado, então
            // o EF só detecta a mudança se a referência da lista mudar (mesmo workaround de
            // ImportacaoService.AtualizarCategoriasAsync).
            registro.Entradas = new List<ItemFinanceiro>(registro.Entradas);
            registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
            registro.SalvoEm = DateTime.UtcNow;
            registro.AtualizadoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(registro);
        }

        if (totalClassificados > 0)
        {
            link.TotalClassificadosPeloCliente += totalClassificados;
            await _linkRepo.AtualizarAsync(link);
        }
    }

    public async Task SugerirRegraAsync(string token, SugerirRegraPortalDto dto)
    {
        var link = await ObterLinkValidoAsync(token);

        var conta = await _contaRepo.ObterPorIdAsync(dto.ContaBancariaId);
        // Mesma defesa de ClassificarAsync — nunca confia no contaBancariaId do payload sem
        // conferir que é mesmo do cliente dono do token (endpoint sem login).
        if (conta == null || conta.ClienteId != link.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        if (dto.Tipo is not ("Entrada" or "Saida"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Tipo deve ser Entrada ou Saida.");
        if (string.IsNullOrWhiteSpace(dto.Categoria))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a categoria da regra.", "categoria");

        var (criterioTipo, criterioValor) = DescricaoMatcher.DeterminarCriterio(dto.DescricaoReferencia);

        // Não duplica: já existe regra (sugerida ou aprovada) com o mesmo critério pra esse cliente.
        var existentes = await _regraRepo.ListarPorClienteAsync(link.ClienteId);
        if (existentes.Any(r => r.ContaBancariaId == dto.ContaBancariaId && r.Tipo == dto.Tipo
            && r.CriterioTipo == criterioTipo && r.CriterioValor == criterioValor))
            return;

        var regra = new RegraCategorizacao
        {
            Id = Guid.NewGuid(),
            ClienteId = link.ClienteId,
            ContaBancariaId = dto.ContaBancariaId,
            Tipo = dto.Tipo,
            CriterioTipo = criterioTipo,
            CriterioValor = criterioValor,
            DescricaoReferencia = dto.DescricaoReferencia,
            AcaoTipo = "Categoria",
            Categoria = dto.Categoria,
            // Nasce inativa e marcada como sugestão — só passa a classificar lançamento nenhum
            // depois que o consultor aprovar (RegraCategorizacaoService.AprovarSugestaoAsync).
            Ativa = false,
            Sugerida = true,
            Ordem = 0,
            CriadoEm = DateTime.UtcNow,
        };
        await _regraRepo.AdicionarAsync(regra);
    }

    private static IEnumerable<PendenteCategorizacaoDto> PendentesDoRegistro(RegistroDiario r) =>
        r.Entradas
            .Where(e => e.PendenteCategorizacao)
            .Select(e => new PendenteCategorizacaoDto { Id = e.Id, Data = r.Data.ToString("yyyy-MM-dd"), Descricao = e.Descricao, Valor = e.Valor, Tipo = "Entrada", Categoria = e.Categoria, CategoriaSugerida = e.CategoriaSugerida })
            .Concat(r.Saidas
                .Where(s => s.PendenteCategorizacao)
                .Select(s => new PendenteCategorizacaoDto { Id = s.Id, Data = r.Data.ToString("yyyy-MM-dd"), Descricao = s.Descricao, Valor = s.Valor, Tipo = "Saida", Categoria = string.IsNullOrEmpty(s.Categoria) ? null : s.Categoria, CategoriaSugerida = s.CategoriaSugerida }));

    private async Task<LinkConciliacao> ObterLinkValidoAsync(string token)
    {
        var link = await _linkRepo.ObterPorTokenAsync(token)
            ?? throw new ApiException(404, CodigoRetorno.LINK_CONCILIACAO_NAO_ENCONTRADO, "Link não encontrado.");
        if (link.RevogadoEm != null)
            throw new ApiException(410, CodigoRetorno.LINK_CONCILIACAO_REVOGADO, "Este link foi revogado. Peça um novo ao seu consultor.");
        if (DateTime.UtcNow > link.ExpiraEm)
            throw new ApiException(410, CodigoRetorno.LINK_CONCILIACAO_EXPIRADO, "Este link expirou. Peça um novo ao seu consultor.");
        return link;
    }
}
