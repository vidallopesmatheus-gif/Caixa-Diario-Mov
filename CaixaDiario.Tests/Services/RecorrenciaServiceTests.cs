using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class RecorrenciaServiceTests
{
    private readonly Mock<IContaRecorrenteRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IOcorrenciaRecorrenteDispensadaRepository> _dispensadaRepoMock = new();
    private readonly RecorrenciaService _sut;

    public RecorrenciaServiceTests()
    {
        _dispensadaRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<OcorrenciaRecorrenteDispensada>());
        _sut = new RecorrenciaService(_contaRepoMock.Object, _registroRepoMock.Object, _dispensadaRepoMock.Object);
    }

    // DataInicio ancorada em "hoje" para que a ocorrência mensal (D6) caia no dia de hoje,
    // alinhando-se aos cenários de materialização que operam sobre o registro de hoje.
    private static ContaRecorrente CriarConta(Guid clienteId, string tipo = "Pagar", DateOnly? dataFim = null, Guid? contaBancariaId = null)
    {
        var hoje = DataLocalHelper.Hoje();
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
            ContaBancariaId = contaBancariaId ?? Guid.NewGuid(),
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
        var hoje = DataLocalHelper.Hoje();
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
        var hoje = DataLocalHelper.Hoje();
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var registroHoje = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            ContaBancariaId = conta.ContaBancariaId,
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
    public async Task MaterializarMesAtual_DuasRecorrenciasDeContasDiferentesNoMesmoDia_NaoMisturaNoMesmoRegistro()
    {
        // Bug da Fase 0.2: a chave de find-or-create do registro era só o dia (ignorava a conta),
        // então a segunda recorrência do dia acabava gravada no registro — e na conta — da primeira.
        var clienteId = Guid.NewGuid();
        var contaBancoA = Guid.NewGuid();
        var contaBancoB = Guid.NewGuid();
        var recorrenciaA = CriarConta(clienteId, tipo: "Pagar", contaBancariaId: contaBancoA);
        var recorrenciaB = CriarConta(clienteId, tipo: "Pagar", contaBancariaId: contaBancoB);
        var hoje = DataLocalHelper.Hoje();
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { recorrenciaA, recorrenciaB });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<RegistroDiario>());

        await _sut.MaterializarMesAtualAsync(clienteId);

        // Dois registros NOVOS e DISTINTOS, um por conta — nunca as duas ocorrências no mesmo.
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.Is<RegistroDiario>(rd =>
            rd.ContaBancariaId == contaBancoA &&
            rd.ContasPagar.Count == 1 && rd.ContasPagar[0].RecorrenciaId == recorrenciaA.Id)), Times.Once);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.Is<RegistroDiario>(rd =>
            rd.ContaBancariaId == contaBancoB &&
            rd.ContasPagar.Count == 1 && rd.ContasPagar[0].RecorrenciaId == recorrenciaB.Id)), Times.Once);
    }

    [Fact]
    public async Task MaterializarMesAtual_OcorrenciaDispensadaPeloCliente_NaoRecria()
    {
        // Reproduz o bug relatado: cliente exclui a ocorrência do dia (RegistroService grava a
        // dispensa) e, na materialização seguinte (toda vez que a lista é recarregada), ela não
        // pode reaparecer — antes disso, "não está nos registros do mês" era tratado como
        // "ainda não foi gerada", recriando a ocorrência na hora.
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, tipo: "Pagar");
        var hoje = DataLocalHelper.Hoje();
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario>());
        _dispensadaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<OcorrenciaRecorrenteDispensada>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, RecorrenciaId = conta.Id, DataVencimento = hoje, CriadoEm = DateTime.UtcNow },
        });

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task DispensarOcorrenciaAsync_RegistraNoRepositorio()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var data = new DateOnly(2026, 9, 10);
        _dispensadaRepoMock.Setup(r => r.ExisteAsync(recorrenciaId, data)).ReturnsAsync(false);

        await _sut.DispensarOcorrenciaAsync(clienteId, recorrenciaId, data);

        _dispensadaRepoMock.Verify(r => r.AdicionarAsync(It.Is<OcorrenciaRecorrenteDispensada>(o =>
            o.ClienteId == clienteId && o.RecorrenciaId == recorrenciaId && o.DataVencimento == data)), Times.Once);
    }

    [Fact]
    public async Task DispensarOcorrenciaAsync_JaDispensada_NaoDuplica()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var data = new DateOnly(2026, 9, 10);
        _dispensadaRepoMock.Setup(r => r.ExisteAsync(recorrenciaId, data)).ReturnsAsync(true);

        await _sut.DispensarOcorrenciaAsync(clienteId, recorrenciaId, data);

        _dispensadaRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<OcorrenciaRecorrenteDispensada>()), Times.Never);
    }

    [Fact]
    public async Task MaterializarMesAtual_ContaExpirada_NaoMaterializa()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
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
        var hoje = DataLocalHelper.Hoje();
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

    // ---- Fase 1.1: DiaVencimento ----

    [Fact]
    public void OcorreEm_ComDiaVencimento_UsaOOverrideEmVezDoDiaDeInicio()
    {
        // DataInicio no dia 5, mas DiaVencimento diz que o vencimento real é todo dia 15.
        var c = Conta(new DateOnly(2026, 1, 5));
        c.DiaVencimento = 15;

        Assert.False(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 2, 5)));  // dia de início, não mais o vencimento
        Assert.True(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 2, 15))); // dia do override
    }

    [Fact]
    public void OcorreEm_SemDiaVencimento_ContinuaUsandoDiaDeInicio()
    {
        // Nenhuma recorrência existente deve mudar de comportamento (DiaVencimento null).
        var c = Conta(new DateOnly(2026, 1, 10));
        Assert.True(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 2, 10)));
        Assert.False(RecorrenciaService.OcorreEm(c, new DateOnly(2026, 2, 15)));
    }

    [Fact]
    public void ContarOcorrenciasAte_TrimestralComDiaVencimento_ContaCorretamenteNoDiaCerto()
    {
        var c = Conta(new DateOnly(2026, 1, 5), "Trimestral");
        c.DiaVencimento = 20;

        // Trimestral a partir de jan: ocorre em jan, abr, jul — sempre dia 20 (nunca dia 5).
        Assert.Equal(1, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 1, 20)));
        Assert.Equal(2, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 4, 20)));
        // No dia 5 (antigo vencimento) não conta mais nenhuma ocorrência extra.
        Assert.Equal(1, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 4, 5)));
    }

    [Fact]
    public void ContarOcorrenciasAte_ComDiaVencimentoQueNaoExisteNoMes_PulaOMesSemQuebrarContagem()
    {
        // DiaVencimento 31 — fevereiro não tem esse dia, mas março tem; a contagem não pode parar
        // nem quebrar por causa do mês sem o dia.
        var c = Conta(new DateOnly(2026, 1, 1));
        c.DiaVencimento = 31;

        Assert.Equal(1, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 1, 31)));
        Assert.Equal(1, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 2, 28))); // fev sem dia 31
        Assert.Equal(2, RecorrenciaService.ContarOcorrenciasAte(c, new DateOnly(2026, 3, 31)));
    }

    // ---- Fase 1.1: ValorVariavel / CalcularValorPrevisto ----

    [Fact]
    public void CalcularValorPrevisto_SemValorVariavel_RetornaOValorCadastrado()
    {
        var conta = new ContaRecorrente { Id = Guid.NewGuid(), Valor = 100m, ValorVariavel = false };
        var resultado = RecorrenciaService.CalcularValorPrevisto(conta, new List<RegistroDiario>());
        Assert.Equal(100m, resultado);
    }

    [Fact]
    public void CalcularValorPrevisto_ValorVariavelSemHistorico_CaiNoValorCadastrado()
    {
        var conta = new ContaRecorrente { Id = Guid.NewGuid(), Valor = 100m, ValorVariavel = true };
        var resultado = RecorrenciaService.CalcularValorPrevisto(conta, new List<RegistroDiario>());
        Assert.Equal(100m, resultado);
    }

    [Fact]
    public void CalcularValorPrevisto_ValorVariavelComHistorico_UsaMediaDasUltimas3PagasPorValorEfetivo()
    {
        var clienteId = Guid.NewGuid();
        var conta = new ContaRecorrente { Id = Guid.NewGuid(), Valor = 100m, ValorVariavel = true };

        // 4 ocorrências pagas; a mais antiga (50) não deve entrar na média das últimas 3.
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = new DateOnly(2026, 4, 10),
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { RecorrenciaId = conta.Id, Valor = 100m, Pago = true, DataBaixa = new DateOnly(2026, 1, 10) },
                // ValorRealizado (juros) prevalece sobre Valor quando preenchido.
                new() { RecorrenciaId = conta.Id, Valor = 100m, ValorRealizado = 130m, Pago = true, DataBaixa = new DateOnly(2026, 2, 10) },
                new() { RecorrenciaId = conta.Id, Valor = 110m, Pago = true, DataBaixa = new DateOnly(2026, 3, 10) },
                new() { RecorrenciaId = conta.Id, Valor = 120m, Pago = true, DataBaixa = new DateOnly(2026, 4, 10) },
            },
        };

        var resultado = RecorrenciaService.CalcularValorPrevisto(conta, new List<RegistroDiario> { registro });

        // Média das 3 mais recentes por DataBaixa: 130 (fev) + 110 (mar) + 120 (abr) = 360 / 3 = 120.
        Assert.Equal(120m, resultado);
    }

    [Fact]
    public async Task MaterializarMesAtual_ContaValorVariavel_MaterializaComValorMedioNaoComValorCadastrado()
    {
        var clienteId = Guid.NewGuid();
        var contaBancariaId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var conta = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Descricao = "Energia", Valor = 100m,
            Tipo = "Pagar", DataInicio = hoje, Periodicidade = "Mensal", Ativo = true,
            ContaBancariaId = contaBancariaId, ValorVariavel = true, CriadoEm = DateTime.UtcNow,
        };
        var registroAntigo = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaBancariaId, Data = hoje.AddMonths(-1),
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada> { new() { RecorrenciaId = conta.Id, Valor = 100m, ValorRealizado = 150m, Pago = true, DataBaixa = hoje.AddMonths(-1) } },
        };

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroAntigo });

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.AdicionarAsync(It.Is<RegistroDiario>(rd =>
            rd.ContasPagar.Single().Valor == 150m)), Times.Once); // média de 1 ocorrência (150), nunca os 100 cadastrados
    }
}
