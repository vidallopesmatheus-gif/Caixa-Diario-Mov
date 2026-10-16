using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class GrupoServiceTests
{
    private readonly Mock<IGrupoRepository> _repoMock = new();
    private readonly Mock<ICategoriaRepository> _categoriaRepoMock = new();
    private readonly GrupoService _sut;

    public GrupoServiceTests()
    {
        _repoMock.Setup(r => r.ListarTodosAsync()).ReturnsAsync(new List<Grupo>());
        _repoMock.Setup(r => r.ContarCategoriasAsync(It.IsAny<Guid>())).ReturnsAsync(0);
        _sut = new GrupoService(_repoMock.Object, _categoriaRepoMock.Object);
    }

    [Fact]
    public async Task CriarAsync_ComBlocoValido_CriaGrupo()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Grupo>())).ReturnsAsync((Grupo g) => g);
        var dto = new CriarGrupoDto { Nome = "Despesas com Ocupação", Bloco = Blocos.DespesasOperacionais };

        var criado = await _sut.CriarAsync(dto);

        Assert.Equal("Despesas com Ocupação", criado.Nome);
        Assert.Equal(Blocos.DespesasOperacionais, criado.Bloco);
        _repoMock.Verify(r => r.AdicionarAsync(It.Is<Grupo>(g => g.Nome == "Despesas com Ocupação" && g.Bloco == Blocos.DespesasOperacionais)), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_ComBlocoInvalido_LancaDadosInvalidos()
    {
        var dto = new CriarGrupoDto { Nome = "X", Bloco = "BLOCO_QUE_NAO_EXISTE" };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_ComNomeDuplicado_LancaConflito()
    {
        var existente = new Grupo { Id = Guid.NewGuid(), Nome = "Custos Diretos", Bloco = Blocos.CustosOperacionais };
        _repoMock.Setup(r => r.ObterPorNomeAsync("Custos Diretos")).ReturnsAsync(existente);
        var dto = new CriarGrupoDto { Nome = "Custos Diretos", Bloco = Blocos.CustosOperacionais };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task AtualizarAsync_MudaBloco_AtualizaTipoDeTodasCategoriasDoGrupo()
    {
        var grupoId = Guid.NewGuid();
        var grupo = new Grupo { Id = grupoId, Nome = "Marketing", Bloco = Blocos.CustosOperacionais, Ativo = true };
        _repoMock.Setup(r => r.ObterPorIdAsync(grupoId)).ReturnsAsync(grupo);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Grupo>())).ReturnsAsync((Grupo g) => g);

        var categoriaDoGrupo = new Categoria { Id = Guid.NewGuid(), Nome = "Publicidade", Tipo = "CustoVariavel", GrupoId = grupoId, Ativa = true };
        var categoriaDeOutroGrupo = new Categoria { Id = Guid.NewGuid(), Nome = "Aluguel", Tipo = "CustoFixo", GrupoId = Guid.NewGuid(), Ativa = true };
        _categoriaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoriaDoGrupo, categoriaDeOutroGrupo });
        _categoriaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Categoria>())).ReturnsAsync((Categoria c) => c);

        var dto = new AtualizarGrupoDto { Nome = "Despesas com Marketing", Bloco = Blocos.DespesasOperacionais, Ativo = true };
        await _sut.AtualizarAsync(grupoId, dto);

        _categoriaRepoMock.Verify(r => r.AtualizarAsync(It.Is<Categoria>(c => c.Id == categoriaDoGrupo.Id && c.Tipo == "CustoFixo")), Times.Once);
        _categoriaRepoMock.Verify(r => r.AtualizarAsync(It.Is<Categoria>(c => c.Id == categoriaDeOutroGrupo.Id)), Times.Never);
    }

    [Fact]
    public async Task AtualizarAsync_MudaBloco_RecalculaTipoMovimentoDeQuemAindaEstavaNoPadraoAntigo()
    {
        var grupoId = Guid.NewGuid();
        var grupo = new Grupo { Id = grupoId, Nome = "Marketing", Bloco = Blocos.CustosOperacionais, Ativo = true };
        _repoMock.Setup(r => r.ObterPorIdAsync(grupoId)).ReturnsAsync(grupo);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Grupo>())).ReturnsAsync((Grupo g) => g);

        // CustosOperacionais → padrão "Saida"; nunca foi mexido manualmente.
        var categoriaNoPadrao = new Categoria { Id = Guid.NewGuid(), Nome = "Publicidade", Tipo = "CustoVariavel", TipoMovimento = "Saida", GrupoId = grupoId, Ativa = true };
        _categoriaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoriaNoPadrao });
        _categoriaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Categoria>())).ReturnsAsync((Categoria c) => c);

        // Move pro Bloco de Investimento, cujo padrão é "Ambos" — diferente do padrão antigo
        // ("Saida"), então dá pra distinguir "recalculou" de "não mudou nada".
        var dto = new AtualizarGrupoDto { Nome = "Imobilizado", Bloco = Blocos.AtividadesDeInvestimento, Ativo = true };
        await _sut.AtualizarAsync(grupoId, dto);

        _categoriaRepoMock.Verify(r => r.AtualizarAsync(It.Is<Categoria>(c => c.Id == categoriaNoPadrao.Id && c.TipoMovimento == "Ambos")), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_MudaBloco_PreservaTipoMovimentoCustomizadoManualmente()
    {
        var grupoId = Guid.NewGuid();
        var grupo = new Grupo { Id = grupoId, Nome = "Marketing", Bloco = Blocos.CustosOperacionais, Ativo = true };
        _repoMock.Setup(r => r.ObterPorIdAsync(grupoId)).ReturnsAsync(grupo);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Grupo>())).ReturnsAsync((Grupo g) => g);

        // Padrão de CustosOperacionais seria "Saida", mas o usuário ajustou manualmente pra
        // "Entrada" — diferente tanto do padrão antigo quanto do novo (Investimento → "Ambos"),
        // então qualquer recálculo automático seria visivelmente errado aqui.
        var categoriaCustomizada = new Categoria { Id = Guid.NewGuid(), Nome = "Publicidade", Tipo = "CustoVariavel", TipoMovimento = "Entrada", GrupoId = grupoId, Ativa = true };
        _categoriaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoriaCustomizada });
        _categoriaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Categoria>())).ReturnsAsync((Categoria c) => c);

        var dto = new AtualizarGrupoDto { Nome = "Imobilizado", Bloco = Blocos.AtividadesDeInvestimento, Ativo = true };
        await _sut.AtualizarAsync(grupoId, dto);

        // Tipo (DRE) muda de qualquer forma — TipoMovimento (ajuste manual) fica intacto.
        _categoriaRepoMock.Verify(r => r.AtualizarAsync(It.Is<Categoria>(c => c.Id == categoriaCustomizada.Id && c.Tipo == "Investimento" && c.TipoMovimento == "Entrada")), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_SemMudarBloco_NaoTocaCategorias()
    {
        var grupoId = Guid.NewGuid();
        var grupo = new Grupo { Id = grupoId, Nome = "Custos Diretos", Bloco = Blocos.CustosOperacionais, Ativo = true };
        _repoMock.Setup(r => r.ObterPorIdAsync(grupoId)).ReturnsAsync(grupo);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Grupo>())).ReturnsAsync((Grupo g) => g);

        var dto = new AtualizarGrupoDto { Nome = "Custos Diretos (renomeado)", Bloco = Blocos.CustosOperacionais, Ativo = true };
        await _sut.AtualizarAsync(grupoId, dto);

        _categoriaRepoMock.Verify(r => r.ListarTodasAsync(), Times.Never);
    }

    [Fact]
    public async Task ListarBlocosAsync_RetornaOs7BlocosFixos()
    {
        var blocos = await _sut.ListarBlocosAsync();

        Assert.Equal(7, blocos.Length);
        Assert.Contains(Blocos.ReceitasOperacionais, blocos);
        Assert.Contains(Blocos.AtividadesDeFinanciamento, blocos);
        Assert.Contains(Blocos.RetiradasSocios, blocos);
    }
}
