using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class GrupoService : IGrupoService
{
    private readonly IGrupoRepository _repo;
    private readonly ICategoriaRepository _categoriaRepo;

    public GrupoService(IGrupoRepository repo, ICategoriaRepository categoriaRepo)
    {
        _repo = repo;
        _categoriaRepo = categoriaRepo;
    }

    public Task<string[]> ListarBlocosAsync() => Task.FromResult(Blocos.Ordem);

    public async Task<List<GrupoDto>> ListarTodosAsync()
    {
        var grupos = await _repo.ListarTodosAsync();
        var resultado = new List<GrupoDto>();
        foreach (var g in grupos)
            resultado.Add(await MapToDtoAsync(g));
        return resultado;
    }

    public async Task<GrupoDto> CriarAsync(CriarGrupoDto dto)
    {
        ValidarBloco(dto.Bloco);
        var nome = dto.Nome.Trim();
        if (await _repo.ObterPorNomeAsync(nome) is not null)
            throw new ApiException(409, CodigoRetorno.GRUPO_DUPLICADO, "Já existe um grupo com esse nome.");

        var maiorOrdem = (await _repo.ListarTodosAsync()).Select(g => g.Ordem).DefaultIfEmpty(-1).Max();

        var grupo = new Grupo
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Bloco = dto.Bloco,
            Ordem = maiorOrdem + 1,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        var criado = await _repo.AdicionarAsync(grupo);
        return await MapToDtoAsync(criado);
    }

    public async Task<GrupoDto> AtualizarAsync(Guid id, AtualizarGrupoDto dto)
    {
        ValidarBloco(dto.Bloco);
        var grupo = await ObterOuFalharAsync(id);

        var nome = dto.Nome.Trim();
        var existente = await _repo.ObterPorNomeAsync(nome);
        if (existente is not null && existente.Id != id)
            throw new ApiException(409, CodigoRetorno.GRUPO_DUPLICADO, "Já existe um grupo com esse nome.");

        var blocoAntigo = grupo.Bloco;
        var blocoMudou = blocoAntigo != dto.Bloco;
        grupo.Nome = nome;
        grupo.Bloco = dto.Bloco;
        grupo.Ativo = dto.Ativo;
        var atualizado = await _repo.AtualizarAsync(grupo);

        // Mudar o bloco de um grupo muda o Tipo de toda categoria que já pertence a ele — o Tipo
        // é sempre derivado do Bloco (ver Blocos.TipoPadrao), nunca escolhido direto na categoria.
        if (blocoMudou)
        {
            var novoTipo = Blocos.TipoPadrao(dto.Bloco);
            // TipoMovimento é editável pelo usuário (diferente de Tipo) — só recalcula o padrão do
            // Bloco novo pra quem ainda estava no padrão do Bloco antigo (nunca foi customizado à
            // mão); uma categoria com TipoMovimento ajustado manualmente mantém o ajuste.
            var tipoMovimentoPadraoAntigo = CategoriaService.TipoMovimentoPadrao(new Grupo { Bloco = blocoAntigo });
            var tipoMovimentoPadraoNovo = CategoriaService.TipoMovimentoPadrao(atualizado);
            var categorias = await _categoriaRepo.ListarTodasAsync();
            foreach (var categoria in categorias.Where(c => c.GrupoId == id))
            {
                var precisaAtualizar = false;
                if (categoria.Tipo != novoTipo) { categoria.Tipo = novoTipo; precisaAtualizar = true; }
                if (categoria.TipoMovimento == tipoMovimentoPadraoAntigo && categoria.TipoMovimento != tipoMovimentoPadraoNovo)
                {
                    categoria.TipoMovimento = tipoMovimentoPadraoNovo;
                    precisaAtualizar = true;
                }
                if (precisaAtualizar)
                    await _categoriaRepo.AtualizarAsync(categoria);
            }
        }

        return await MapToDtoAsync(atualizado);
    }

    public async Task DesativarAsync(Guid id)
    {
        var grupo = await ObterOuFalharAsync(id);
        grupo.Ativo = false;
        await _repo.AtualizarAsync(grupo);
    }

    public async Task ReordenarAsync(ReordenarGruposDto dto)
    {
        var novaOrdem = dto.Ids.Select((id, indice) => (Id: id, Ordem: indice)).ToList();
        await _repo.ReordenarAsync(novaOrdem);
    }

    private async Task<Grupo> ObterOuFalharAsync(Guid id) =>
        await _repo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.GRUPO_NAO_ENCONTRADO, "Grupo não encontrado.");

    private static void ValidarBloco(string bloco)
    {
        if (!Blocos.Validos.Contains(bloco))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, $"Bloco inválido. Use: {string.Join(", ", Blocos.Ordem)}");
    }

    private async Task<GrupoDto> MapToDtoAsync(Grupo g) => new()
    {
        Id = g.Id,
        Nome = g.Nome,
        Bloco = g.Bloco,
        Ordem = g.Ordem,
        Ativo = g.Ativo,
        QuantidadeCategorias = await _repo.ContarCategoriasAsync(g.Id),
    };
}
