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

    // DataInicio ancorada em "hoje" para que a ocorrência mensal (D6) caia no dia de hoje,
    // alinhando-se aos cenários de materialização que operam sobre o registro de hoje.
    private static ContaRecorrente CriarConta(Guid clienteId, string tipo = "Pagar", DateOnly? dataFim = null)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        return new()
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            Descricao = "Aluguel",
            Valor = 1000m,
            Tipo = tipo,
            DataInicio = hoje,
            DataFim = dataFim,
            Periodicidade = "Mensal",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
    }

    private static ContaRecorrente Conta(
        DateOnly dataInicio,
        string periodicidade = "Mensal",
        DateOnly? dataFim = null,
        int? quantidadeParcelas = null) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        Descricao = "Teste",
        Valor = 100m,
        Tipo = "Pagar",
        DataInicio = dataInicio,
        DataFim = dataFim,
        Periodicidade = periodicidade,
        QuantidadeParcelas = quantidadeParcelas,
        Ativo = true,
        CriadoEm = DateTime.UtcNow,
    };

    // ---- OcorreEm ----

    [Fact]
    public void OcorreEm_AntesDoInicio_False()
    {
        var c = Conta(new DateOnly(2026, 1, 10));
        Assert.False(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 1, 9)));
    }

    [Fact]
    public void OcorreEm_NoInicio_True()
    {
        var c = Conta(new DateOnly(2026, 1, 10));
        Assert.True(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 1, 10)));
    }

    [Theory]
    [InlineData("2026-01-08", true)]   // +7 dias
    [InlineData("2026-01-15", true)]   // +14 dias
    [InlineData("2026-01-09", false)]  // +8 dias
    public void OcorreEm_Semanal(string dia, bool esperado)
    {
        var c = Conta(new DateOnly(2026, 1, 1), "Semanal");
        Assert.Equal(esperado, RecorrenciaService.OcorreEm(c, DateOnly.Parse(dia)));
    }

    [Theory]
    [InlineData("2026-01-15", true)]   // +14 dias
    [InlineData("2026-01-29", true)]   // +28 dias
    [InlineData("2026-01-08", false)]  // +7 dias (não casa quinzenal)
    public void OcorreEm_Quinzenal(string dia, bool esperado)
    {
        var c = Conta(new DateOnly(2026, 1, 1), "Quinzenal");
        Assert.Equal(esperado, RecorrenciaService.OcorreEm(c, DateOnly.Parse(dia)));
    }

    [Theory]
    [InlineData("2026-02-10", true)]
    [InlineData("2026-12-10", true)]
    [InlineData("2026-02-11", false)]
    public void OcorreEm_Mensal(string dia, bool esperado)
    {
        var c = Conta(new DateOnly(2026, 1, 10), "Mensal");
        Assert.Equal(esperado, RecorrenciaService.OcorreEm(c, DateOnly.Parse(dia)));
    }

    [Fact]
    public void OcorreEm_Mensal_Dia31_MesSemAqueleDia_NaoOcorre()
    {
        // Borda D6: correspondência exata de dia; fevereiro não tem dia 31 -> sem ocorrência
        var c = Conta(new DateOnly(2026, 1, 31), "Mensal");
        Assert.False(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 2, 28)));
        Assert.True(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 3, 31)));
    }

    [Theory]
    [InlineData("2026-04-10", true)]   // +3 meses
    [InlineData("2026-07-10", true)]   // +6 meses
    [InlineData("2026-02-10", false)]  // +1 mês (não casa trimestral)
    public void OcorreEm_Trimestral(string dia, bool esperado)
    {
        var c = Conta(new DateOnly(2026, 1, 10), "Trimestral");
        Assert.Equal(esperado, RecorrenciaService.OcorreEm(c, DateOnly.Parse(dia)));
    }

    [Theory]
    [InlineData("2027-01-10", true)]   // +1 ano
    [InlineData("2026-02-10", false)]  // mesmo dia, mês errado
    public void OcorreEm_Anual(string dia, bool esperado)
    {
        var c = Conta(new DateOnly(2026, 1, 10), "Anual");
        Assert.Equal(esperado, RecorrenciaService.OcorreEm(c, DateOnly.Parse(dia)));
    }

    [Fact]
    public void OcorreEm_RespeitaDataFim()
    {
        var c = Conta(new DateOnly(2026, 1, 10), "Mensal", dataFim: new DateOnly(2026, 3, 1));
        Assert.True(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 2, 10)));
        Assert.False(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 3, 10)));
    }

    [Fact]
    public void OcorreEm_RespeitaQuantidadeParcelas()
    {
        // 3 parcelas a partir de jan/10: jan, fev, mar. Abr (4ª) não ocorre.
        var c = Conta(new DateOnly(2026, 1, 10), "Mensal", quantidadeParcelas: 3);
        Assert.True(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 1, 10)));
        Assert.True(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 3, 10)));
        Assert.False(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 4, 10)));
    }

    // ---- ContarOcorrenciasAte ----

    [Fact]
    public void ContarOcorrenciasAte_Mensal()
    {
        var c = Conta(new DateOnly(2026, 1, 10), "Mensal");
        Assert.Equal(1, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 1, 10)));
        Assert.Equal(3, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 3, 10)));
    }

    [Fact]
    public void ContarOcorrenciasAte_Semanal()
    {
        var c = Conta(new DateOnly(2026, 1, 1), "Semanal");
        Assert.Equal(1, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 1, 1)));
        Assert.Equal(3, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 1, 15)));
    }

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
                new() { Descricao = "Aluguel", Valor = 1000m, RecorrenciaId = conta.Id, DataVencimento = hoje }
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
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId))
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

    [Fact]
    public async Task MaterializarMesAtual_Semanal_MaterializaCadaOcorrenciaDoMes()
    {
        // Conta semanal a partir do dia 1 do mês corrente: deve gerar uma provisão por
        // ocorrência semanal dentro do mês (em registros novos por dia).
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var conta = new ContaRecorrente
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            Descricao = "Semanal",
            Valor = 50m,
            Tipo = "Pagar",
            DataInicio = primeiroDia,
            Periodicidade = "Semanal",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        var ocorrenciasEsperadas = 0;
        for (var d = primeiroDia; d <= ultimoDia; d = d.AddDays(1))
            if ((d.DayNumber - primeiroDia.DayNumber) % 7 == 0) ocorrenciasEsperadas++;

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<RegistroDiario>());

        await _sut.MaterializarMesAtualAsync(clienteId);

        // Cada ocorrência cai num dia distinto -> um registro novo por ocorrência.
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.Is<RegistroDiario>(rd =>
            rd.ContasPagar.Count == 1 && rd.ContasPagar[0].RecorrenciaId == conta.Id)),
            Times.Exactly(ocorrenciasEsperadas));
    }
}
