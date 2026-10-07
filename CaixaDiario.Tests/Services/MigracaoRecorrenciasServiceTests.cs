using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class MigracaoRecorrenciasServiceTests
{
    private readonly Mock<IContaRecorrenteRepository> _contaRecorrenteRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaBancariaRepoMock = new();
    private readonly MigracaoRecorrenciasService _sut;

    public MigracaoRecorrenciasServiceTests()
    {
        _sut = new MigracaoRecorrenciasService(_contaRecorrenteRepoMock.Object, _registroRepoMock.Object, _contaBancariaRepoMock.Object);
    }

    private static ContaRecorrente CriarRecorrencia(Guid clienteId, Guid contaBancariaId) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Descricao = "Telefone", Valor = 30m,
        Tipo = "Pagar", DataInicio = new DateOnly(2026, 1, 10), Periodicidade = "Mensal",
        Ativo = true, ContaBancariaId = contaBancariaId, CriadoEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task MigrarAsync_DryRun_ReportaMasNaoPersisteNada()
    {
        var clienteId = Guid.NewGuid();
        var contaCorreta = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Ativa = true, SaldoInicial = 100m };
        var contaErrada = Guid.NewGuid();
        var recorrencia = CriarRecorrencia(clienteId, contaCorreta.Id);
        var dataVencimento = new DateOnly(2026, 10, 10);

        var registroErrado = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaErrada, Data = dataVencimento,
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Telefone", Valor = 30m, DataVencimento = dataVencimento, Pago = false, RecorrenciaId = recorrencia.Id },
            },
        };

        _contaRecorrenteRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaRecorrente> { recorrencia });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroErrado });
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorreta.Id)).ReturnsAsync(contaCorreta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCorreta.Id, dataVencimento)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaCorreta.Id)).ReturnsAsync(new List<RegistroDiario>());

        var resultado = await _sut.MigrarOcorrenciasSemContaCorretaAsync(confirmar: false);

        Assert.False(resultado.Confirmado);
        Assert.Equal(1, resultado.ItensMovidos);
        Assert.Equal(1, resultado.RegistrosCriados);
        Assert.Equal(1, resultado.RegistrosExcluidosVazios); // registroErrado ficou vazio
        Assert.Empty(resultado.ItensQuePrecisamRevisaoManual);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task MigrarAsync_Confirmado_PersisteRegistroNovoEMarcaOrigemExcluida()
    {
        var clienteId = Guid.NewGuid();
        var contaCorreta = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Ativa = true, SaldoInicial = 100m };
        var contaErrada = Guid.NewGuid();
        var recorrencia = CriarRecorrencia(clienteId, contaCorreta.Id);
        var dataVencimento = new DateOnly(2026, 10, 10);

        var registroErrado = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaErrada, Data = dataVencimento,
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Telefone", Valor = 30m, DataVencimento = dataVencimento, Pago = false, RecorrenciaId = recorrencia.Id },
            },
        };

        _contaRecorrenteRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaRecorrente> { recorrencia });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroErrado });
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorreta.Id)).ReturnsAsync(contaCorreta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCorreta.Id, dataVencimento)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaCorreta.Id)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.MigrarOcorrenciasSemContaCorretaAsync(confirmar: true);

        Assert.True(resultado.Confirmado);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.Is<RegistroDiario>(novo =>
            novo.ContaBancariaId == contaCorreta.Id &&
            novo.ContasPagar.Single().RecorrenciaId == recorrencia.Id)), Times.Once);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(origem =>
            origem.Id == registroErrado.Id && origem.Excluido && origem.ContasPagar.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task MigrarAsync_ItemJaPago_NuncaMoveEVaiPraRevisaoManual()
    {
        var clienteId = Guid.NewGuid();
        var contaCorreta = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Ativa = true, SaldoInicial = 100m };
        var contaErrada = Guid.NewGuid();
        var recorrencia = CriarRecorrencia(clienteId, contaCorreta.Id);
        var dataVencimento = new DateOnly(2026, 10, 10);

        var registroErrado = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaErrada, Data = dataVencimento,
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Telefone", Valor = 30m, DataVencimento = dataVencimento, Pago = true, DataBaixa = dataVencimento, RecorrenciaId = recorrencia.Id },
            },
        };

        _contaRecorrenteRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaRecorrente> { recorrencia });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroErrado });
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorreta.Id)).ReturnsAsync(contaCorreta);

        var resultado = await _sut.MigrarOcorrenciasSemContaCorretaAsync(confirmar: true);

        Assert.Equal(0, resultado.ItensMovidos);
        Assert.Single(resultado.ItensQuePrecisamRevisaoManual);
        Assert.Single(registroErrado.ContasPagar); // item nunca saiu de onde estava
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task MigrarAsync_DuasRecorrenciasMesmaContaEData_CriaSoUmRegistroNovo()
    {
        var clienteId = Guid.NewGuid();
        var contaCorreta = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Ativa = true, SaldoInicial = 0m };
        var contaErrada = Guid.NewGuid();
        var recorrenciaA = CriarRecorrencia(clienteId, contaCorreta.Id);
        var recorrenciaB = CriarRecorrencia(clienteId, contaCorreta.Id);
        var dataVencimento = new DateOnly(2026, 10, 10);

        var registroErrado = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaErrada, Data = dataVencimento,
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Telefone", Valor = 30m, DataVencimento = dataVencimento, Pago = false, RecorrenciaId = recorrenciaA.Id },
                new() { Descricao = "Internet", Valor = 100m, DataVencimento = dataVencimento, Pago = false, RecorrenciaId = recorrenciaB.Id },
            },
        };

        _contaRecorrenteRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaRecorrente> { recorrenciaA, recorrenciaB });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroErrado });
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorreta.Id)).ReturnsAsync(contaCorreta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCorreta.Id, dataVencimento)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaCorreta.Id)).ReturnsAsync(new List<RegistroDiario>());

        var resultado = await _sut.MigrarOcorrenciasSemContaCorretaAsync(confirmar: false);

        Assert.Equal(2, resultado.ItensMovidos);
        Assert.Equal(1, resultado.RegistrosCriados); // um só, não dois
    }
}
