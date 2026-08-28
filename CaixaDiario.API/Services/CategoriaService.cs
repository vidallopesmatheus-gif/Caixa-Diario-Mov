using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class CategoriaService : ICategoriaService
{
    private static readonly HashSet<string> TiposValidos = new()
    {
        "Receita", "CustoVariavel", "CustoFixo", "DespesaNaoOperacional",
    };

    private readonly ICategoriaRepository _repo;

    public CategoriaService(ICategoriaRepository repo) => _repo = repo;

    public async Task<CategoriasAgrupadasDto> ListarAgrupadasAsync()
    {
        var ativas = await _repo.ListarAtivasAsync();
        return new CategoriasAgrupadasDto
        {
            Entradas = ativas.Where(c => c.Tipo == "Receita").Select(MapToItemDto).ToList(),
            Saidas = ativas.Where(c => c.Tipo != "Receita").Select(MapToItemDto).ToList(),
        };
    }

    public async Task<List<CategoriaDto>> ListarParaGerenciarAsync()
    {
        var todas = await _repo.ListarTodasAsync();
        return todas.Select(MapToDto).ToList();
    }

    public async Task<CategoriaDto> CriarAsync(CriarCategoriaDto dto)
    {
        ValidarTipo(dto.Tipo);
        var nome = dto.Nome.Trim();
        if (await _repo.ObterPorNomeAsync(nome) is not null)
            throw new ApiException(409, CodigoRetorno.CATEGORIA_DUPLICADA, "Já existe uma categoria com esse nome.");

        var maiorOrdem = (await _repo.ListarTodasAsync()).Select(c => c.Ordem).DefaultIfEmpty(-1).Max();

        var categoria = new Categoria
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Tipo = dto.Tipo,
            Grupo = null,
            Ordem = maiorOrdem + 1,
            Ativa = true,
            CriadoEm = DateTime.UtcNow,
        };
        var criada = await _repo.AdicionarAsync(categoria);
        return MapToDto(criada);
    }

    public async Task<CategoriaDto> AtualizarAsync(Guid id, AtualizarCategoriaDto dto)
    {
        ValidarTipo(dto.Tipo);
        var categoria = await ObterOuFalharAsync(id);

        var nome = dto.Nome.Trim();
        var existente = await _repo.ObterPorNomeAsync(nome);
        if (existente is not null && existente.Id != id)
            throw new ApiException(409, CodigoRetorno.CATEGORIA_DUPLICADA, "Já existe uma categoria com esse nome.");

        categoria.Nome = nome;
        categoria.Tipo = dto.Tipo;
        categoria.Ativa = dto.Ativa;
        var atualizada = await _repo.AtualizarAsync(categoria);
        return MapToDto(atualizada);
    }

    public async Task DesativarAsync(Guid id)
    {
        var categoria = await ObterOuFalharAsync(id);
        categoria.Ativa = false;
        await _repo.AtualizarAsync(categoria);
    }

    public async Task ReordenarAsync(ReordenarCategoriasDto dto)
    {
        var novaOrdem = dto.Ids.Select((id, indice) => (Id: id, Ordem: indice)).ToList();
        await _repo.ReordenarAsync(novaOrdem);
    }

    public async Task<ExclusaoCategoriaResultDto> ExcluirOuInformarUsoAsync(Guid id)
    {
        var categoria = await ObterOuFalharAsync(id);
        var quantidade = await _repo.ContarUsoAsync(categoria.Nome);
        if (quantidade > 0)
            return new ExclusaoCategoriaResultDto { Excluida = false, QuantidadeLancamentos = quantidade };

        await _repo.RemoverAsync(categoria);
        return new ExclusaoCategoriaResultDto { Excluida = true, QuantidadeLancamentos = 0 };
    }

    public async Task MigrarLancamentosAsync(Guid origemId, Guid destinoId)
    {
        var origem = await ObterOuFalharAsync(origemId);
        var destino = await ObterOuFalharAsync(destinoId);

        await _repo.MigrarUsoAsync(origem.Nome, destino.Nome, destino.Tipo);

        origem.Ativa = false;
        await _repo.AtualizarAsync(origem);
    }

    private async Task<Categoria> ObterOuFalharAsync(Guid id) =>
        await _repo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.CATEGORIA_NAO_ENCONTRADA, "Categoria não encontrada.");

    private static void ValidarTipo(string tipo)
    {
        if (!TiposValidos.Contains(tipo))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, $"Tipo inválido. Use: {string.Join(", ", TiposValidos)}");
    }

    private static CategoriaItemDto MapToItemDto(Categoria c) => new()
    {
        Nome = c.Nome,
        TipoCusto = c.Tipo,
        Grupo = c.Grupo,
    };

    private static CategoriaDto MapToDto(Categoria c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        Tipo = c.Tipo,
        Grupo = c.Grupo,
        Ordem = c.Ordem,
        Ativa = c.Ativa,
    };
}
