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

    public ConciliacaoPublicaService(
        ILinkConciliacaoRepository linkRepo,
        IRegistroRepository registroRepo,
        IContaBancariaRepository contaRepo,
        ICategoriaRepository categoriaRepo,
        ICategoriaService categoriaService)
    {
        _linkRepo = linkRepo;
        _registroRepo = registroRepo;
        _contaRepo = contaRepo;
        _categoriaRepo = categoriaRepo;
        _categoriaService = categoriaService;
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

    private static IEnumerable<PendenteCategorizacaoDto> PendentesDoRegistro(RegistroDiario r) =>
        r.Entradas
            .Where(e => e.PendenteCategorizacao)
            .Select(e => new PendenteCategorizacaoDto { Id = e.Id, Data = r.Data.ToString("yyyy-MM-dd"), Descricao = e.Descricao, Valor = e.Valor, Tipo = "Entrada" })
            .Concat(r.Saidas
                .Where(s => s.PendenteCategorizacao)
                .Select(s => new PendenteCategorizacaoDto { Id = s.Id, Data = r.Data.ToString("yyyy-MM-dd"), Descricao = s.Descricao, Valor = s.Valor, Tipo = "Saida" }));

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
