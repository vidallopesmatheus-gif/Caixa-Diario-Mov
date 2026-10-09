using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class DuplicataExtratoServiceTests
{
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IDuplicataDispensadaRepository> _dispensadaRepoMock = new();
    private readonly DuplicataExtratoService _sut;

    public DuplicataExtratoServiceTests()
    {
        _dispensadaRepoMock.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<DuplicataDispensada>());
        _sut = new DuplicataExtratoService(_contaRepoMock.Object, _registroRepoMock.Object, _dispensadaRepoMock.Object);
    }

    private static ContaBancaria CriarConta(Guid contaId, Guid clienteId) => new()
    {
        Id = contaId, ClienteId = clienteId, Nome = "C6 Bank", Tipo = "ContaCorrente",
        SaldoInicial = 0m, Ativa = true, DataCriacao = DateTime.UtcNow,
    };

    private static RegistroDiario CriarRegistro(Guid contaId, Guid clienteId, DateOnly data) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
        Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task ListarProvaveisAsync_ClienteTentandoVerDeOutraConta_LancaAcessoNegado()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ListarProvaveisAsync(contaId, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ListarProvaveisAsync_MesmoValorDataProximaEDescricaoParecida_SugereComoDuplicata()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = CriarRegistro(contaId, clienteId, new DateOnly(2026, 9, 1));
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        registro.Entradas.Add(new ItemFinanceiro { Id = idA, Descricao = "Pix recebido de 53.430.745 POLLY CRISTIE EIRELI", Valor = 189.80m });
        registro.Entradas.Add(new ItemFinanceiro { Id = idB, Descricao = "Pix recebido - Polly Cristie - 53.430.745/0001-00", Valor = 189.80m });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarProvaveisAsync(contaId, clienteId, "cliente");

        var par = Assert.Single(resultado);
        Assert.Equal("Entrada", par.Tipo);
        Assert.Equal(new HashSet<Guid> { idA, idB }, new HashSet<Guid> { par.LancamentoAId, par.LancamentoBId });
        Assert.True(par.Score >= 40, $"Esperava score >= 40, obteve {par.Score}");
    }

    [Fact]
    public async Task ListarProvaveisAsync_ValorDiferente_NaoSugere()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = CriarRegistro(contaId, clienteId, new DateOnly(2026, 9, 1));
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Gym Coffee", Valor = 11.00m });
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Gym Coffee", Valor = 15.00m });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarProvaveisAsync(contaId, clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarProvaveisAsync_DataForaDaJanelaDeDoisDias_NaoSugere()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro1 = CriarRegistro(contaId, clienteId, new DateOnly(2026, 9, 1));
        registro1.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Assai Atacadista", Valor = 5.38m });
        var registro2 = CriarRegistro(contaId, clienteId, new DateOnly(2026, 9, 10));
        registro2.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Assai Atacadista", Valor = 5.38m });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro1, registro2 });

        var resultado = await _sut.ListarProvaveisAsync(contaId, clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarProvaveisAsync_LancamentoDeTransferencia_NuncaEhSugerido()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = CriarRegistro(contaId, clienteId, new DateOnly(2026, 9, 1));
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Kaique", Valor = 382.14m, TipoCusto = "Transferencia", TransferenciaId = Guid.NewGuid() });
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Kaique", Valor = 382.14m });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarProvaveisAsync(contaId, clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarProvaveisAsync_TresLancamentosParecidos_CadaUmSoApareceEmUmPar()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = CriarRegistro(contaId, clienteId, new DateOnly(2026, 9, 1));
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Kaique Transporte", Valor = 382.14m });
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Kaique Transporte", Valor = 382.14m });
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Kaique Transporte", Valor = 382.14m });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarProvaveisAsync(contaId, clienteId, "cliente");

        Assert.Single(resultado);
        var idsUsados = resultado.SelectMany(r => new[] { r.LancamentoAId, r.LancamentoBId }).ToList();
        Assert.Equal(idsUsados.Count, idsUsados.Distinct().Count());
    }

    // ── Item 3.2: "Manter os dois" ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListarProvaveisAsync_ParJaDispensado_NaoApareceMais()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = CriarRegistro(contaId, clienteId, new DateOnly(2026, 9, 1));
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        registro.Entradas.Add(new ItemFinanceiro { Id = idA, Descricao = "Pix recebido de 53.430.745 POLLY CRISTIE EIRELI", Valor = 189.80m });
        registro.Entradas.Add(new ItemFinanceiro { Id = idB, Descricao = "Pix recebido - Polly Cristie - 53.430.745/0001-00", Valor = 189.80m });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        // Dispensado com A/B na ordem CONTRÁRIA à que viria de EncontrarPares — confirma que a
        // normalização (menor/maior) funciona independente da ordem de descoberta do par.
        var (menor, maior) = idA.CompareTo(idB) <= 0 ? (idA, idB) : (idB, idA);
        _dispensadaRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<DuplicataDispensada>
        {
            new() { Id = Guid.NewGuid(), ContaBancariaId = contaId, LancamentoMenorId = menor, LancamentoMaiorId = maior, CriadoEm = DateTime.UtcNow },
        });

        var resultado = await _sut.ListarProvaveisAsync(contaId, clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ManterOsDoisAsync_GravaODispenseComParNormalizado()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        DuplicataDispensada? gravado = null;
        _dispensadaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<DuplicataDispensada>()))
            .Callback<DuplicataDispensada>(d => gravado = d).Returns(Task.CompletedTask);

        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        await _sut.ManterOsDoisAsync(contaId, idA, idB, clienteId, "cliente");

        Assert.NotNull(gravado);
        var (menorEsperado, maiorEsperado) = idA.CompareTo(idB) <= 0 ? (idA, idB) : (idB, idA);
        Assert.Equal(menorEsperado, gravado!.LancamentoMenorId);
        Assert.Equal(maiorEsperado, gravado.LancamentoMaiorId);
    }

    [Fact]
    public async Task ManterOsDoisAsync_ParJaDispensado_NaoGravaDeNovo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var (menor, maior) = idA.CompareTo(idB) <= 0 ? (idA, idB) : (idB, idA);
        _dispensadaRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<DuplicataDispensada>
        {
            new() { Id = Guid.NewGuid(), ContaBancariaId = contaId, LancamentoMenorId = menor, LancamentoMaiorId = maior, CriadoEm = DateTime.UtcNow },
        });

        await _sut.ManterOsDoisAsync(contaId, idA, idB, clienteId, "cliente");

        _dispensadaRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<DuplicataDispensada>()), Times.Never);
    }

    [Fact]
    public async Task ManterOsDoisAsync_ClienteTentandoDispensarDeOutraConta_LancaAcessoNegado()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => _sut.ManterOsDoisAsync(contaId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }
}
