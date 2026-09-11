using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.DTOs.PortalConciliacao;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ConciliacaoPublicaServiceTests
{
    private readonly Mock<ILinkConciliacaoRepository> _linkRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<ICategoriaRepository> _categoriaRepoMock = new();
    private readonly Mock<ICategoriaService> _categoriaServiceMock = new();
    private readonly ConciliacaoPublicaService _sut;

    public ConciliacaoPublicaServiceTests()
    {
        _categoriaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria>());
        _categoriaServiceMock.Setup(s => s.ListarAgrupadasAsync()).ReturnsAsync(new CategoriasAgrupadasDto());
        _sut = new ConciliacaoPublicaService(
            _linkRepoMock.Object, _registroRepoMock.Object, _contaRepoMock.Object, _categoriaRepoMock.Object, _categoriaServiceMock.Object);
    }

    private static LinkConciliacao CriarLinkValido(Guid clienteId) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Token = "token-valido",
        CriadoEm = DateTime.UtcNow.AddHours(-1), ExpiraEm = DateTime.UtcNow.AddHours(23),
    };

    [Fact]
    public async Task ObterPendentesAsync_TokenInexistente_LancaNaoEncontrado()
    {
        _linkRepoMock.Setup(r => r.ObterPorTokenAsync("x")).ReturnsAsync((LinkConciliacao?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterPendentesAsync("x"));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ObterPendentesAsync_LinkExpirado_Lanca410()
    {
        var link = CriarLinkValido(Guid.NewGuid());
        link.ExpiraEm = DateTime.UtcNow.AddHours(-1);
        _linkRepoMock.Setup(r => r.ObterPorTokenAsync(link.Token)).ReturnsAsync(link);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterPendentesAsync(link.Token));
        Assert.Equal(410, ex.StatusCode);
        Assert.Equal(CaixaDiario.API.Enums.CodigoRetorno.LINK_CONCILIACAO_EXPIRADO, ex.Codigo);
    }

    [Fact]
    public async Task ObterPendentesAsync_LinkRevogado_Lanca410()
    {
        var link = CriarLinkValido(Guid.NewGuid());
        link.RevogadoEm = DateTime.UtcNow;
        _linkRepoMock.Setup(r => r.ObterPorTokenAsync(link.Token)).ReturnsAsync(link);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterPendentesAsync(link.Token));
        Assert.Equal(410, ex.StatusCode);
        Assert.Equal(CaixaDiario.API.Enums.CodigoRetorno.LINK_CONCILIACAO_REVOGADO, ex.Codigo);
    }

    [Fact]
    public async Task ObterPendentesAsync_SoIncluiContasComPeloMenosUmPendente()
    {
        var clienteId = Guid.NewGuid();
        var link = CriarLinkValido(clienteId);
        _linkRepoMock.Setup(r => r.ObterPorTokenAsync(link.Token)).ReturnsAsync(link);

        var contaComPendencia = Guid.NewGuid();
        var contaEmDia = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            new() { Id = contaComPendencia, ClienteId = clienteId, Nome = "Conta A" },
            new() { Id = contaEmDia, ClienteId = clienteId, Nome = "Conta B" },
        });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaComPendencia, Data = new DateOnly(2026, 1, 1),
                Entradas = new() { new() { Id = Guid.NewGuid(), Descricao = "Pix recebido", Valor = 100, PendenteCategorizacao = true } },
                Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            },
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaEmDia, Data = new DateOnly(2026, 1, 1),
                Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Já categorizado", Valor = 50, PendenteCategorizacao = false } },
                ContasReceber = new(), ContasPagar = new(),
            },
        });

        var resultado = await _sut.ObterPendentesAsync(link.Token);

        var conta = Assert.Single(resultado.Contas);
        Assert.Equal(contaComPendencia, conta.ContaBancariaId);
        Assert.Single(conta.Itens);
    }

    [Fact]
    public async Task ClassificarAsync_ItemValido_MarcaClassificadoPeloClienteEIncrementaContador()
    {
        var clienteId = Guid.NewGuid();
        var link = CriarLinkValido(clienteId);
        _linkRepoMock.Setup(r => r.ObterPorTokenAsync(link.Token)).ReturnsAsync(link);

        var contaId = Guid.NewGuid();
        var item = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Fornecedor X", Valor = 100, PendenteCategorizacao = true };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 1, 1),
            Entradas = new(), Saidas = new() { item }, ContasReceber = new(), ContasPagar = new(),
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, registro.Data)).ReturnsAsync(registro);

        var dto = new ClassificarPendentesDto
        {
            Itens = new() { new ClassificarPendenteItem { Id = item.Id, Data = "2026-01-01", ContaBancariaId = contaId, Categoria = "Insumos/Mercadoria" } },
        };

        await _sut.ClassificarAsync(link.Token, dto);

        Assert.True(item.ClassificadoPeloCliente);
        Assert.False(item.PendenteCategorizacao);
        Assert.Equal("Insumos/Mercadoria", item.Categoria);
        Assert.Equal(1, link.TotalClassificadosPeloCliente);
        _linkRepoMock.Verify(r => r.AtualizarAsync(link), Times.Once);
    }

    [Fact]
    public async Task ClassificarAsync_RegistroDeOutroCliente_IgnoraSemClassificarNemLancarErro()
    {
        var link = CriarLinkValido(Guid.NewGuid());
        _linkRepoMock.Setup(r => r.ObterPorTokenAsync(link.Token)).ReturnsAsync(link);

        var contaId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var registroDeOutroCliente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ContaBancariaId = contaId, Data = new DateOnly(2026, 1, 1),
            Entradas = new(), Saidas = new() { new() { Id = itemId, Descricao = "x", Valor = 10, PendenteCategorizacao = true } },
            ContasReceber = new(), ContasPagar = new(),
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, registroDeOutroCliente.Data)).ReturnsAsync(registroDeOutroCliente);

        var dto = new ClassificarPendentesDto
        {
            Itens = new() { new ClassificarPendenteItem { Id = itemId, Data = "2026-01-01", ContaBancariaId = contaId, Categoria = "Qualquer" } },
        };

        await _sut.ClassificarAsync(link.Token, dto);

        Assert.Equal(0, link.TotalClassificadosPeloCliente);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }
}
