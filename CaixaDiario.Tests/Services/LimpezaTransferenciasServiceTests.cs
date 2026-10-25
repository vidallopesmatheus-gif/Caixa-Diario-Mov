using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class LimpezaTransferenciasServiceTests
{
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly LimpezaTransferenciasService _sut;

    public LimpezaTransferenciasServiceTests()
    {
        _sut = new LimpezaTransferenciasService(_registroRepoMock.Object, _contaRepoMock.Object);
    }

    private static ContaBancaria CriarConta(Guid clienteId, string nome, decimal saldoInicial = 0m) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Nome = nome, Tipo = "Investimento", SaldoInicial = saldoInicial, Ativa = true, DataCriacao = DateTime.UtcNow,
    };

    [Fact]
    public async Task LimparContrapartidasOrfasAsync_AchaSaidaProvisoriaSemTransferenciaId()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Investimento Nubank");
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 10, 15),
            Inicio = 1335.53m, SaldoFinal = 815.53m, Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Pix recebido", Valor = 520m, Categoria = "", PendenteCategorizacao = true, Provisoria = true, TransferenciaId = null } },
        };
        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro });
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var resultado = await _sut.LimparContrapartidasOrfasAsync(confirmar: false);

        var item = Assert.Single(resultado.Itens);
        Assert.Equal("Pix recebido", item.Descricao);
        Assert.Equal(520m, item.Valor);
        Assert.Equal("Saida", item.Tipo);
        Assert.Equal("Investimento Nubank", item.ContaBancariaNome);
        Assert.Equal(1, resultado.ItensEncontrados);
    }

    [Fact]
    public async Task LimparContrapartidasOrfasAsync_ProvisoriaMasAindaVinculada_NaoEntraNoRelatorio()
    {
        // Provisória de uma Transferência ainda ativa (TransferenciaId preenchido) — normal, está só
        // aguardando o extrato real conciliar (ver ImportacaoService.ReconciliarProvisoria), não é órfã.
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Investimento");
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 10, 15),
            Inicio = 1000m, SaldoFinal = 500m, Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Ainda ativa", Valor = 500m, Provisoria = true, TransferenciaId = Guid.NewGuid() } },
        };
        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.LimparContrapartidasOrfasAsync(confirmar: false);

        Assert.Empty(resultado.Itens);
    }

    [Fact]
    public async Task LimparContrapartidasOrfasAsync_NaoProvisoria_NaoEntraNoRelatorioMesmoSemTransferenciaId()
    {
        // Lançamento real desvinculado por um "Desfazer transferência" normal (não provisório) —
        // esse é o comportamento CORRETO (ver DesfazerClassificacaoAsync), não uma órfã pra limpar.
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Conta Corrente");
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 10, 15),
            Inicio = 1000m, SaldoFinal = 1000m, Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            Entradas = new() { new ItemFinanceiro { Descricao = "Pix real desvinculado", Valor = 300m, Provisoria = false, TransferenciaId = null, PendenteCategorizacao = true } },
        };
        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.LimparContrapartidasOrfasAsync(confirmar: false);

        Assert.Empty(resultado.Itens);
    }

    [Fact]
    public async Task LimparContrapartidasOrfasAsync_SemConfirmar_NaoGravaNada()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Investimento");
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 10, 15),
            Inicio = 1335.53m, SaldoFinal = 815.53m, Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Pix recebido", Valor = 520m, Provisoria = true, TransferenciaId = null } },
        };
        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro });
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var resultado = await _sut.LimparContrapartidasOrfasAsync(confirmar: false);

        Assert.False(resultado.Confirmado);
        Assert.Equal(0, resultado.ContasRecalculadas);
        Assert.Single(registro.Saidas); // nada removido
        Assert.Equal(815.53m, registro.SaldoFinal); // saldo intacto
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task LimparContrapartidasOrfasAsync_ComConfirmar_RemoveAItemEReconstroiACadeiaDeSaldo()
    {
        // Reproduz o caso real: Investimento Nubank tinha dois incidentes no histórico — a órfã do
        // Pix em 15/10 (−520 fantasma) e um saldo já arrastado errado desde 14/10 (o "R$400 a mais"
        // do relato, aqui simulado como um Inicio de 14/10 desalinhado do saldo inicial da conta).
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Investimento Nubank", saldoInicial: 1335.53m);
        var dia14 = new DateOnly(2026, 10, 14);
        var dia15 = new DateOnly(2026, 10, 15);

        var registro14 = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = dia14,
            Inicio = 1335.53m, SaldoFinal = 1335.53m, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        };
        var registro15 = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = dia15,
            // Inicio arrastado do SaldoFinal (stale) de 14/10 antes da reconstrução — igual ao real.
            Inicio = 1335.53m, SaldoFinal = 415.53m, Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Pix recebido", Valor = 520m, Provisoria = true, TransferenciaId = null } },
        };

        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro14, registro15 });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { registro14, registro15 });
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var resultado = await _sut.LimparContrapartidasOrfasAsync(confirmar: true);

        Assert.True(resultado.Confirmado);
        Assert.Equal(1, resultado.ItensEncontrados);
        Assert.Equal(1, resultado.ContasRecalculadas);

        Assert.Empty(registro15.Saidas); // linha fantasma removida
        Assert.Equal(1335.53m, registro14.SaldoFinal); // dia 14 recalculado (sem entradas/saídas = Inicio)
        Assert.Equal(1335.53m, registro15.Inicio); // herda o SaldoFinal correto de 14/10
        Assert.Equal(1335.53m, registro15.SaldoFinal); // sem a saída fantasma, bate com a soma do dia
    }

    [Fact]
    public async Task LimparContrapartidasOrfasAsync_RegistroSemContaVinculada_EIgnorado()
    {
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ContaBancariaId = null, Data = new DateOnly(2026, 10, 15),
            Inicio = 0m, SaldoFinal = 0m, Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "x", Valor = 10m, Provisoria = true, TransferenciaId = null } },
        };
        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.LimparContrapartidasOrfasAsync(confirmar: false);

        Assert.Empty(resultado.Itens);
    }
}
