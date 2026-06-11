using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class RecorrenciaServiceTests
{
    private readonly Mock<IContaRecorrenteRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly RecorrenciaService _sut;

    public RecorrenciaServiceTests() =>
        _sut = new RecorrenciaService(_contaRepoMock.Object, _registroRepoMock.Object);

    private static ContaRecorrente CriarConta(Guid clienteId, string tipo = "Pagar", DateOnly? dataFim = null) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = clienteId,
        Descricao = "Aluguel",
        Valor = 1000m,
        Tipo = tipo,
        DataInicio = new DateOnly(2026, 1, 1),
        DataFim = dataFim,
        Ativo = true,
        CriadoEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task MaterializarMesAtual_SemContasAtivas_NaoAcessaRegistros()
    {
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente>());

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.ListarPorPeriodoAsync(
            It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task MaterializarMesAtual_ContaJaMaterializada_NaoCriaDuplicata()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            Data = hoje,
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Aluguel", Valor = 1000m, RecorrenciaId = conta.Id }
            },
            ContasReceber = new List<ContaProvisionada>(),
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario> { registroExistente });

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task MaterializarMesAtual_ContaPendente_AdicionaContaNoRegistroExistente()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, tipo: "Pagar");
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var registroHoje = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            Data = hoje,
            ContasPagar = new List<ContaProvisionada>(),
            ContasReceber = new List<ContaProvisionada>(),
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario> { registroHoje });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .ReturnsAsync((RegistroDiario r) => r);

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(rd =>
            rd.ContasPagar.Count == 1 &&
            rd.ContasPagar[0].RecorrenciaId == conta.Id)), Times.Once);
    }

    [Fact]
    public async Task MaterializarMesAtual_ContaExpirada_NaoMaterializa()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);
        var dataFimPassado = primeiroDia.AddDays(-1);  // expired before current month
        var conta = CriarConta(clienteId, dataFim: dataFimPassado);

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario>());

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }
}
