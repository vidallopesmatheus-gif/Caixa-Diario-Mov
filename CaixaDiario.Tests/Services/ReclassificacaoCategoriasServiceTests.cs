using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ReclassificacaoCategoriasServiceTests
{
    private readonly Mock<ICategoriaRepository> _repoMock = new();
    private readonly ReclassificacaoCategoriasService _sut;
    private readonly Guid _clienteId = Guid.NewGuid();

    public ReclassificacaoCategoriasServiceTests()
    {
        _sut = new ReclassificacaoCategoriasService(_repoMock.Object);
    }

    private static Categoria Categoria(string nome, string tipoMovimento, Guid? clienteId, bool ativa = true) => new()
    {
        Id = Guid.NewGuid(), Nome = nome, TipoMovimento = tipoMovimento, ClienteId = clienteId, Ativa = ativa,
    };

    [Fact]
    public async Task ReclassificarPorUsoAsync_CategoriaSoUsadaEmSaida_SugereSaidaMesmoClassificadaComoAmbos()
    {
        var categoria = Categoria("Aluguel", "Ambos", _clienteId);
        _repoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoria });
        _repoMock.Setup(r => r.ContarUsoPorCategoriaAsync(_clienteId))
            .ReturnsAsync(new Dictionary<string, (int Entradas, int Saidas)> { ["Aluguel"] = (0, 12) });

        var resultado = await _sut.ReclassificarPorUsoAsync(confirmar: false);

        var item = Assert.Single(resultado.Itens);
        Assert.Equal("Ambos", item.TipoMovimentoAtual);
        Assert.Equal("Saida", item.TipoMovimentoSugerido);
        Assert.Equal(0, item.QuantidadeEntradas);
        Assert.Equal(12, item.QuantidadeSaidas);
    }

    [Fact]
    public async Task ReclassificarPorUsoAsync_CategoriaUsadaNosDoisLados_SugereAmbos()
    {
        var categoria = Categoria("Equipamentos", "Entrada", _clienteId);
        _repoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoria });
        _repoMock.Setup(r => r.ContarUsoPorCategoriaAsync(_clienteId))
            .ReturnsAsync(new Dictionary<string, (int Entradas, int Saidas)> { ["Equipamentos"] = (3, 2) });

        var resultado = await _sut.ReclassificarPorUsoAsync(confirmar: false);

        var item = Assert.Single(resultado.Itens);
        Assert.Equal("Ambos", item.TipoMovimentoSugerido);
    }

    [Fact]
    public async Task ReclassificarPorUsoAsync_CategoriaSemUso_NaoEntraNoRelatorio()
    {
        var categoria = Categoria("Nunca usada", "Entrada", _clienteId);
        _repoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoria });
        _repoMock.Setup(r => r.ContarUsoPorCategoriaAsync(_clienteId))
            .ReturnsAsync(new Dictionary<string, (int Entradas, int Saidas)>());

        var resultado = await _sut.ReclassificarPorUsoAsync(confirmar: false);

        Assert.Empty(resultado.Itens);
        Assert.Equal(1, resultado.CategoriasAnalisadas);
    }

    [Fact]
    public async Task ReclassificarPorUsoAsync_SugestaoIgualAtual_NaoEntraNoRelatorio()
    {
        var categoria = Categoria("Vendas", "Entrada", _clienteId);
        _repoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoria });
        _repoMock.Setup(r => r.ContarUsoPorCategoriaAsync(_clienteId))
            .ReturnsAsync(new Dictionary<string, (int Entradas, int Saidas)> { ["Vendas"] = (40, 0) });

        var resultado = await _sut.ReclassificarPorUsoAsync(confirmar: false);

        Assert.Empty(resultado.Itens);
    }

    [Fact]
    public async Task ReclassificarPorUsoAsync_IgnoraCategoriasDoMoldePadrao()
    {
        var molde = Categoria("Vendas", "Entrada", clienteId: null);
        _repoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { molde });

        var resultado = await _sut.ReclassificarPorUsoAsync(confirmar: false);

        Assert.Equal(0, resultado.CategoriasAnalisadas);
        _repoMock.Verify(r => r.ContarUsoPorCategoriaAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ReclassificarPorUsoAsync_SemConfirmar_NaoGravaNada()
    {
        var categoria = Categoria("Aluguel", "Ambos", _clienteId);
        _repoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoria });
        _repoMock.Setup(r => r.ContarUsoPorCategoriaAsync(_clienteId))
            .ReturnsAsync(new Dictionary<string, (int Entradas, int Saidas)> { ["Aluguel"] = (0, 12) });

        var resultado = await _sut.ReclassificarPorUsoAsync(confirmar: false);

        Assert.False(resultado.Confirmado);
        Assert.Equal(0, resultado.CategoriasReclassificadas);
        _repoMock.Verify(r => r.AtualizarAsync(It.IsAny<Categoria>()), Times.Never);
    }

    [Fact]
    public async Task ReclassificarPorUsoAsync_ComConfirmar_GravaOTipoMovimentoSugerido()
    {
        var categoria = Categoria("Aluguel", "Ambos", _clienteId);
        _repoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoria });
        _repoMock.Setup(r => r.ContarUsoPorCategoriaAsync(_clienteId))
            .ReturnsAsync(new Dictionary<string, (int Entradas, int Saidas)> { ["Aluguel"] = (0, 12) });
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Categoria>())).ReturnsAsync((Categoria c) => c);

        var resultado = await _sut.ReclassificarPorUsoAsync(confirmar: true);

        Assert.True(resultado.Confirmado);
        Assert.Equal(1, resultado.CategoriasReclassificadas);
        Assert.Equal("Saida", categoria.TipoMovimento);
        _repoMock.Verify(r => r.AtualizarAsync(categoria), Times.Once);
    }

    [Fact]
    public async Task ReclassificarPorUsoAsync_IgnoraCategoriasInativas()
    {
        var categoria = Categoria("Antiga", "Ambos", _clienteId, ativa: false);
        _repoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria> { categoria });

        var resultado = await _sut.ReclassificarPorUsoAsync(confirmar: false);

        Assert.Equal(0, resultado.CategoriasAnalisadas);
        _repoMock.Verify(r => r.ContarUsoPorCategoriaAsync(It.IsAny<Guid>()), Times.Never);
    }
}
