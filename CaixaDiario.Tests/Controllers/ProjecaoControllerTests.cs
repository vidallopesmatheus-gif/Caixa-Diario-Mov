using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Projecao;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class ProjecaoControllerTests
{
    private readonly Mock<IProjecaoService> _projecaoMock = new();
    private readonly Mock<IRegistroRepository> _registroMock = new();
    private readonly Mock<IContaRecorrenteRepository> _contaMock = new();
    private readonly Mock<IContaBancariaRepository> _contaBancariaMock = new();
    private readonly Mock<IPagamentoFaturaRepository> _pagamentoFaturaMock = new();

    private ProjecaoController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new ProjecaoController(
            _projecaoMock.Object, _registroMock.Object, _contaMock.Object, _contaBancariaMock.Object, _pagamentoFaturaMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    private void ConfigurarRepositorios(Guid clienteId)
    {
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
        _contaBancariaMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>());
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    public async Task ObterProjecao_ComDiasValido_UsaValorInformado(int dias)
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        int? diasRecebidos = null;
        _projecaoMock.Setup(s => s.Calcular(
                It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<ProjecaoOpcoes>()))
            .Callback<List<RegistroDiario>, List<ContaRecorrente>, int, Guid?, ProjecaoOpcoes>((_, _, d, _, _) => diasRecebidos = d)
            .Returns(new ProjecaoDto());

        await CriarSut(Guid.NewGuid(), "admin").ObterProjecao(clienteId, dias);

        Assert.Equal(dias, diasRecebidos);
    }

    [Fact]
    public async Task ObterProjecao_ComDiasInvalido_NormalizaPara30()
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        int? diasRecebidos = null;
        _projecaoMock.Setup(s => s.Calcular(
                It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<ProjecaoOpcoes>()))
            .Callback<List<RegistroDiario>, List<ContaRecorrente>, int, Guid?, ProjecaoOpcoes>((_, _, d, _, _) => diasRecebidos = d)
            .Returns(new ProjecaoDto());

        await CriarSut(Guid.NewGuid(), "admin").ObterProjecao(clienteId, 45);

        Assert.Equal(30, diasRecebidos);
    }

    [Fact]
    public async Task ObterProjecao_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        _projecaoMock.Setup(s => s.Calcular(
                It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<ProjecaoOpcoes>()))
            .Returns(new ProjecaoDto());

        var result = await CriarSut(Guid.NewGuid(), "admin").ObterProjecao(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<ProjecaoDto>>(ok.Value);
    }

    [Fact]
    public async Task ObterProjecao_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ObterProjecao(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }

    // Bloco 2 da rodada "Visão de Futuro": cenário inválido cai pra "Provavel" — nunca passa
    // adiante um texto arbitrário pro service.
    [Theory]
    [InlineData("Conservador", "Conservador")]
    [InlineData("Provavel", "Provavel")]
    [InlineData("lixo", "Provavel")]
    [InlineData("", "Provavel")]
    public async Task ObterProjecao_ComCenario_NormalizaOuMantem(string cenarioInformado, string cenarioEsperado)
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        ProjecaoOpcoes? opcoesRecebidas = null;
        _projecaoMock.Setup(s => s.Calcular(
                It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<ProjecaoOpcoes>()))
            .Callback<List<RegistroDiario>, List<ContaRecorrente>, int, Guid?, ProjecaoOpcoes>((_, _, _, _, o) => opcoesRecebidas = o)
            .Returns(new ProjecaoDto());

        await CriarSut(Guid.NewGuid(), "admin").ObterProjecao(clienteId, cenario: cenarioInformado);

        Assert.Equal(cenarioEsperado, opcoesRecebidas!.Cenario);
    }

    [Fact]
    public async Task ObterTrajetoria_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        _projecaoMock.Setup(s => s.CalcularTrajetoria(
                It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<ProjecaoOpcoes>()))
            .Returns(new TrajetoriaDto());

        var result = await CriarSut(Guid.NewGuid(), "admin").ObterTrajetoria(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<TrajetoriaDto>>(ok.Value);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(30, 30, 12, 12)]
    public async Task ObterTrajetoria_ComMesesForaDoIntervalo_ClampaEntre1E12(int mesesPassado, int mesesFuturo, int passadoEsperado, int futuroEsperado)
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        (int Passado, int Futuro)? recebido = null;
        _projecaoMock.Setup(s => s.CalcularTrajetoria(
                It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<ProjecaoOpcoes>()))
            .Callback<List<RegistroDiario>, List<ContaRecorrente>, int, int, Guid?, ProjecaoOpcoes>((_, _, p, f, _, _) => recebido = (p, f))
            .Returns(new TrajetoriaDto());

        await CriarSut(Guid.NewGuid(), "admin").ObterTrajetoria(clienteId, mesesPassado, mesesFuturo);

        Assert.Equal((passadoEsperado, futuroEsperado), recebido);
    }

    [Fact]
    public async Task ObterTrajetoria_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ObterTrajetoria(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }

    // Item 4 da rodada "Visão de Futuro": agrega os pagamentos de fatura de TODOS os cartões do
    // cliente (não só o primeiro) antes de montar as opções passadas pro service.
    [Fact]
    public async Task ObterProjecao_ComDoisCartoes_AgregaPagamentosDeFaturaDosDois()
    {
        var clienteId = Guid.NewGuid();
        var cartao1 = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Cartão 1", Tipo = "CartaoCredito", Ativa = true };
        var cartao2 = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Cartão 2", Tipo = "CartaoCredito", Ativa = true };
        var corrente = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Corrente", Tipo = "ContaCorrente", Ativa = true };

        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
        _contaBancariaMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { cartao1, cartao2, corrente });

        var pagamento1 = new PagamentoFatura { Id = Guid.NewGuid(), ContaCartaoId = cartao1.Id, Competencia = "2026-01" };
        var pagamento2 = new PagamentoFatura { Id = Guid.NewGuid(), ContaCartaoId = cartao2.Id, Competencia = "2026-01" };
        _pagamentoFaturaMock.Setup(r => r.ListarPorContaCartaoAsync(cartao1.Id)).ReturnsAsync(new List<PagamentoFatura> { pagamento1 });
        _pagamentoFaturaMock.Setup(r => r.ListarPorContaCartaoAsync(cartao2.Id)).ReturnsAsync(new List<PagamentoFatura> { pagamento2 });

        ProjecaoOpcoes? opcoesRecebidas = null;
        _projecaoMock.Setup(s => s.Calcular(
                It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<ProjecaoOpcoes>()))
            .Callback<List<RegistroDiario>, List<ContaRecorrente>, int, Guid?, ProjecaoOpcoes>((_, _, _, _, o) => opcoesRecebidas = o)
            .Returns(new ProjecaoDto());

        await CriarSut(Guid.NewGuid(), "admin").ObterProjecao(clienteId);

        Assert.Equal(3, opcoesRecebidas!.Contas.Count);
        Assert.Equal(2, opcoesRecebidas.PagamentosFatura.Count);
        Assert.Contains(opcoesRecebidas.PagamentosFatura, p => p.Id == pagamento1.Id);
        Assert.Contains(opcoesRecebidas.PagamentosFatura, p => p.Id == pagamento2.Id);
    }
}
