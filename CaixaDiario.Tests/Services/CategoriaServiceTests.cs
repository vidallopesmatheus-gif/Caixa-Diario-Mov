using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class CategoriaServiceTests
{
    private readonly Mock<ICategoriaRepository> _repoMock = new();
    private readonly Mock<IGrupoRepository> _grupoRepoMock = new();
    private readonly CategoriaService _sut;

    public CategoriaServiceTests()
    {
        _repoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria>());
        _sut = new CategoriaService(_repoMock.Object, _grupoRepoMock.Object);
    }

    private static Grupo Grupo(string nome, string bloco) => new() { Id = Guid.NewGuid(), Nome = nome, Bloco = bloco, Ativo = true };

    [Fact]
    public async Task CriarAsync_DerivaTipoDoBlocoDoGrupo_NaoDoInputDoUsuario()
    {
        var grupo = Grupo("Imobilizado", Blocos.AtividadesDeInvestimento);
        _grupoRepoMock.Setup(r => r.ObterPorIdAsync(grupo.Id)).ReturnsAsync(grupo);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Categoria>())).ReturnsAsync((Categoria c) => c);

        var criada = await _sut.CriarAsync(new CriarCategoriaDto { Nome = "Equipamentos", GrupoId = grupo.Id });

        Assert.Equal("Investimento", criada.Tipo);
        Assert.Equal(grupo.Id, criada.GrupoId);
        Assert.Equal(Blocos.AtividadesDeInvestimento, criada.Bloco);
    }

    [Fact]
    public async Task CriarAsync_ComGrupoInexistente_LancaNaoEncontrado()
    {
        var grupoId = Guid.NewGuid();
        _grupoRepoMock.Setup(r => r.ObterPorIdAsync(grupoId)).ReturnsAsync((Grupo?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarCategoriaDto { Nome = "X", GrupoId = grupoId }));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_ComNomeDuplicado_LancaConflito()
    {
        var grupo = Grupo("Custos Diretos", Blocos.CustosOperacionais);
        _grupoRepoMock.Setup(r => r.ObterPorIdAsync(grupo.Id)).ReturnsAsync(grupo);
        _repoMock.Setup(r => r.ObterPorNomeAsync("Insumos")).ReturnsAsync(new Categoria { Id = Guid.NewGuid(), Nome = "Insumos", GrupoId = grupo.Id, Grupo = grupo });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarCategoriaDto { Nome = "Insumos", GrupoId = grupo.Id }));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task AtualizarAsync_MudaDeGrupo_AtualizaTipoJunto()
    {
        var grupoAntigo = Grupo("Custos Diretos", Blocos.CustosOperacionais);
        var grupoNovo = Grupo("Despesas com Marketing", Blocos.DespesasOperacionais);
        var categoria = new Categoria { Id = Guid.NewGuid(), Nome = "Publicidade", Tipo = "CustoVariavel", GrupoId = grupoAntigo.Id, Grupo = grupoAntigo, Ativa = true };
        _repoMock.Setup(r => r.ObterPorIdAsync(categoria.Id)).ReturnsAsync(categoria);
        _grupoRepoMock.Setup(r => r.ObterPorIdAsync(grupoNovo.Id)).ReturnsAsync(grupoNovo);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Categoria>())).ReturnsAsync((Categoria c) => c);

        var atualizada = await _sut.AtualizarAsync(categoria.Id, new AtualizarCategoriaDto { Nome = "Publicidade", GrupoId = grupoNovo.Id, Ativa = true });

        Assert.Equal("CustoFixo", atualizada.Tipo);
        Assert.Equal(grupoNovo.Id, atualizada.GrupoId);
    }

    [Fact]
    public async Task ListarAgrupadasAsync_InvestimentoEFinanciamento_AparecemNosDoisLados()
    {
        var grupoInvest = Grupo("Imobilizado", Blocos.AtividadesDeInvestimento);
        var grupoReceita = Grupo("Vendas e Serviços", Blocos.ReceitasOperacionais);
        var grupoDespesa = Grupo("Despesas com Ocupação", Blocos.DespesasOperacionais);
        var ativas = new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Vendas", Tipo = "Receita", Grupo = grupoReceita, Ativa = true },
            new() { Id = Guid.NewGuid(), Nome = "Aluguel", Tipo = "CustoFixo", Grupo = grupoDespesa, Ativa = true },
            new() { Id = Guid.NewGuid(), Nome = "Equipamentos", Tipo = "Investimento", Grupo = grupoInvest, Ativa = true },
        };
        _repoMock.Setup(r => r.ListarAtivasAsync()).ReturnsAsync(ativas);

        var agrupadas = await _sut.ListarAgrupadasAsync();

        Assert.Contains(agrupadas.Entradas, c => c.Nome == "Vendas");
        Assert.Contains(agrupadas.Entradas, c => c.Nome == "Equipamentos");
        Assert.DoesNotContain(agrupadas.Entradas, c => c.Nome == "Aluguel");

        Assert.Contains(agrupadas.Saidas, c => c.Nome == "Aluguel");
        Assert.Contains(agrupadas.Saidas, c => c.Nome == "Equipamentos");
        Assert.DoesNotContain(agrupadas.Saidas, c => c.Nome == "Vendas");
    }

    [Fact]
    public async Task ListarAgrupadasAsync_DeducoesDaReceita_AparecemNosDoisLadosENaoSeConfundemComDespesa()
    {
        // Deduções da Receita e Despesas Operacionais mapeiam pro mesmo Tipo ("CustoFixo" — ver
        // Blocos.TipoPadrao), então a distinção entrada/saída não pode depender só do Tipo aqui.
        var grupoDevolucao = Grupo("Devolução e Estorno", Blocos.DeducoesDaReceita);
        var grupoDespesa = Grupo("Despesas com Ocupação", Blocos.DespesasOperacionais);
        var ativas = new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Devoluções", Tipo = "CustoFixo", Grupo = grupoDevolucao, Ativa = true },
            new() { Id = Guid.NewGuid(), Nome = "Aluguel", Tipo = "CustoFixo", Grupo = grupoDespesa, Ativa = true },
        };
        _repoMock.Setup(r => r.ListarAtivasAsync()).ReturnsAsync(ativas);

        var agrupadas = await _sut.ListarAgrupadasAsync();

        Assert.Contains(agrupadas.Entradas, c => c.Nome == "Devoluções");
        Assert.DoesNotContain(agrupadas.Entradas, c => c.Nome == "Aluguel");

        Assert.Contains(agrupadas.Saidas, c => c.Nome == "Devoluções");
        Assert.Contains(agrupadas.Saidas, c => c.Nome == "Aluguel");
    }
}
