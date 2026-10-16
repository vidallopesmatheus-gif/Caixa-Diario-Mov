using CaixaDiario.API.DTOs.Admin;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Rodada de ajustes Money Vision (item 8): "migrar categorias existentes por padrão de uso,
// gerando um relatório antes de aplicar". Compara o TipoMovimento atual de cada categoria (hoje
// herdado do Bloco do grupo, via a migration AdicionarTipoMovimentoEmCategoria) com o que o
// HISTÓRICO REAL de lançamentos sugere — só entrada usada => "Entrada", só saída => "Saida", as
// duas => "Ambos". Categoria sem nenhum uso não tem dado pra sugerir nada, então mantém o atual.
// Mesmo padrão dry-run-por-padrão do MigracaoRecorrenciasService: sem confirmar=true, só calcula e
// devolve a lista de divergências — nada é gravado.
public class ReclassificacaoCategoriasService : IReclassificacaoCategoriasService
{
    private readonly ICategoriaRepository _categoriaRepo;

    public ReclassificacaoCategoriasService(ICategoriaRepository categoriaRepo) => _categoriaRepo = categoriaRepo;

    public async Task<ReclassificacaoCategoriasResultDto> ReclassificarPorUsoAsync(bool confirmar)
    {
        var resultado = new ReclassificacaoCategoriasResultDto { Confirmado = confirmar };
        var todas = await _categoriaRepo.ListarTodasAsync();
        // O molde padrão (ClienteId == null) nunca é usado diretamente em lançamento — só as
        // cópias por cliente são.
        var porCliente = todas.Where(c => c.Ativa && c.ClienteId.HasValue).GroupBy(c => c.ClienteId!.Value);

        foreach (var grupo in porCliente)
        {
            var uso = await _categoriaRepo.ContarUsoPorCategoriaAsync(grupo.Key);
            foreach (var categoria in grupo)
            {
                resultado.CategoriasAnalisadas++;
                var (qtdEntradas, qtdSaidas) = uso.TryGetValue(categoria.Nome, out var contagem) ? contagem : (0, 0);
                if (qtdEntradas == 0 && qtdSaidas == 0) continue; // sem uso, sem dado pra sugerir

                var sugerido = (qtdEntradas > 0, qtdSaidas > 0) switch
                {
                    (true, true) => "Ambos",
                    (true, false) => "Entrada",
                    _ => "Saida",
                };
                if (sugerido == categoria.TipoMovimento) continue;

                resultado.Itens.Add(new ReclassificacaoCategoriaItemDto
                {
                    ClienteId = grupo.Key,
                    CategoriaId = categoria.Id,
                    Nome = categoria.Nome,
                    TipoMovimentoAtual = categoria.TipoMovimento,
                    TipoMovimentoSugerido = sugerido,
                    QuantidadeEntradas = qtdEntradas,
                    QuantidadeSaidas = qtdSaidas,
                });

                if (confirmar)
                {
                    categoria.TipoMovimento = sugerido;
                    await _categoriaRepo.AtualizarAsync(categoria);
                    resultado.CategoriasReclassificadas++;
                }
            }
        }

        resultado.Itens = resultado.Itens.OrderByDescending(i => i.QuantidadeEntradas + i.QuantidadeSaidas).ToList();
        return resultado;
    }
}
