using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class LinkConciliacaoServiceTests
{
    private readonly Mock<ILinkConciliacaoRepository> _repoMock = new();
    private readonly LinkConciliacaoService _sut;

    public LinkConciliacaoServiceTests()
    {
        _sut = new LinkConciliacaoService(_repoMock.Object);
    }

    [Fact]
    public async Task GerarAsync_SemLinkAtivo_CriaLinkComExpiracaoEm24Horas()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterAtivoPorClienteAsync(clienteId)).ReturnsAsync((LinkConciliacao?)null);
        LinkConciliacao? salvo = null;
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<LinkConciliacao>()))
            .Callback<LinkConciliacao>(l => salvo = l)
            .ReturnsAsync((LinkConciliacao l) => l);

        var dto = await _sut.GerarAsync(clienteId, clienteId, "cliente");

        Assert.NotNull(salvo);
        Assert.Equal(clienteId, salvo!.ClienteId);
        Assert.False(string.IsNullOrWhiteSpace(salvo.Token));
        Assert.Equal(salvo.CriadoEm.AddHours(24), salvo.ExpiraEm);
        Assert.Equal("Ativo", dto.Status);
    }

    [Fact]
    public async Task GerarAsync_ComLinkAtivoExistente_RevogaOAnteriorAntesDeCriarNovo()
    {
        var clienteId = Guid.NewGuid();
        var anterior = new LinkConciliacao { Id = Guid.NewGuid(), ClienteId = clienteId, Token = "abc", CriadoEm = DateTime.UtcNow.AddHours(-1), ExpiraEm = DateTime.UtcNow.AddHours(23) };
        _repoMock.Setup(r => r.ObterAtivoPorClienteAsync(clienteId)).ReturnsAsync(anterior);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<LinkConciliacao>())).ReturnsAsync((LinkConciliacao l) => l);

        await _sut.GerarAsync(clienteId, clienteId, "cliente");

        Assert.NotNull(anterior.RevogadoEm);
        _repoMock.Verify(r => r.AtualizarAsync(anterior), Times.Once);
    }

    [Fact]
    public async Task GerarAsync_ClienteTentandoGerarParaOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.GerarAsync(Guid.NewGuid(), Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Theory]
    [InlineData(true, null, "Revogado")]
    [InlineData(false, -1, "Expirado")]
    [InlineData(false, 1, "Ativo")]
    public async Task ListarAsync_CalculaStatusConformeRevogacaoEExpiracao(bool revogado, int? horasParaExpirar, string statusEsperado)
    {
        var clienteId = Guid.NewGuid();
        var link = new LinkConciliacao
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            CriadoEm = DateTime.UtcNow.AddHours(-2),
            ExpiraEm = DateTime.UtcNow.AddHours(horasParaExpirar ?? 24),
            RevogadoEm = revogado ? DateTime.UtcNow : null,
        };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<LinkConciliacao> { link });

        var resultado = await _sut.ListarAsync(clienteId, clienteId, "cliente");

        Assert.Equal(statusEsperado, Assert.Single(resultado).Status);
    }

    [Fact]
    public async Task RevogarAsync_LinkAtivo_MarcaRevogadoEm()
    {
        var clienteId = Guid.NewGuid();
        var link = new LinkConciliacao { Id = Guid.NewGuid(), ClienteId = clienteId };
        _repoMock.Setup(r => r.ObterPorIdAsync(link.Id)).ReturnsAsync(link);

        await _sut.RevogarAsync(link.Id, clienteId, "cliente");

        Assert.NotNull(link.RevogadoEm);
    }

    [Fact]
    public async Task RevogarAsync_LinkJaRevogado_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var link = new LinkConciliacao { Id = Guid.NewGuid(), ClienteId = clienteId, RevogadoEm = DateTime.UtcNow };
        _repoMock.Setup(r => r.ObterPorIdAsync(link.Id)).ReturnsAsync(link);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.RevogarAsync(link.Id, clienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task RevogarAsync_ClienteTentandoRevogarLinkDeOutroCliente_LancaAcessoNegado()
    {
        var link = new LinkConciliacao { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid() };
        _repoMock.Setup(r => r.ObterPorIdAsync(link.Id)).ReturnsAsync(link);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.RevogarAsync(link.Id, Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }
}
