using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class RecalculoSaldoServiceTests
{
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly RecalculoSaldoService _sut;

    public RecalculoSaldoServiceTests()
    {
        _sut = new RecalculoSaldoService(_contaRepoMock.Object, _registroRepoMock.Object);
    }

    private static ContaBancaria CriarConta(decimal saldoInicial = 0m) => new()
    {
        Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Nome = "Nubank", Tipo = "ContaCorrente",
        SaldoInicial = saldoInicial, Ativa = true, DataCriacao = DateTime.UtcNow,
    };

    // Efeito real do dia (SaldoEncadeamento) vem de uma entrada/saída de mesmo valor — o recálculo
    // usa os lançamentos do dia, nunca o "SaldoFinal − Inicio" gravado.
    private static RegistroDiario CriarRegistro(Guid contaId, Guid clienteId, DateOnly data, decimal inicio, decimal saldoFinal, decimal efeito) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
        Inicio = inicio, SaldoFinal = saldoFinal, ContasReceber = new(), ContasPagar = new(),
        Entradas = efeito > 0 ? new() { new() { Descricao = "Entrada", Valor = efeito } } : new(),
        Saidas = efeito < 0 ? new() { new() { Descricao = "Saída", Valor = -efeito } } : new(),
    };

    [Fact]
    public async Task RecalcularTodasAsContasAsync_SemConfirmar_SoRelatorioNaoGravaNada()
    {
        var conta = CriarConta(saldoInicial: 1000m);
        // 18/10 fecha em 816,72 mas 19/10 abre arrastando um Inicio velho de 132,66 — exatamente o
        // bug real relatado (Nubank).
        var dia1 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 18), 1000m, 816.72m, -183.28m);
        var dia2 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 19), 132.66m, -575.12m, -707.78m);
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { dia1, dia2 });

        var resultado = await _sut.RecalcularTodasAsContasAsync(confirmar: false);

        Assert.False(resultado.Confirmado);
        Assert.Equal(1, resultado.ContasComDivergencia);
        var item = Assert.Single(resultado.Itens);
        Assert.Equal(conta.Id, item.ContaBancariaId);
        Assert.Equal(new DateOnly(2026, 10, 19), item.PrimeiraDataDivergente);
        Assert.Equal(-575.12m, item.SaldoAtualAntes);
        // dia2 tem saída de 707,78; com o Inicio corrigido (816,72, o saldo final de 18/10),
        // o saldo final correto é 816,72 − 707,78 = 108,94.
        Assert.Equal(108.94m, item.SaldoAtualDepois);

        // Nada foi gravado.
        Assert.Equal(132.66m, dia2.Inicio);
        Assert.Equal(-575.12m, dia2.SaldoFinal);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task RecalcularTodasAsContasAsync_ComConfirmar_CorrigeACadeiaDeVerdade()
    {
        var conta = CriarConta(saldoInicial: 1000m);
        var dia1 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 18), 1000m, 816.72m, -183.28m);
        var dia2 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 19), 132.66m, -575.12m, -707.78m);
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { dia1, dia2 });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.RecalcularTodasAsContasAsync(confirmar: true);

        Assert.True(resultado.Confirmado);
        Assert.Equal(816.72m, dia2.Inicio);
        Assert.Equal(108.94m, dia2.SaldoFinal);
        // dia1 já estava consistente (Inicio == SaldoInicial da conta) — não precisa gravar.
        _registroRepoMock.Verify(r => r.AtualizarAsync(dia1), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(dia2), Times.Once);
    }

    [Fact]
    public async Task RecalcularTodasAsContasAsync_ContaSemDivergencia_NaoEntraNoRelatorio()
    {
        var conta = CriarConta(saldoInicial: 500m);
        var dia1 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 18), 500m, 700m, 200m);
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { dia1 });

        var resultado = await _sut.RecalcularTodasAsContasAsync(confirmar: false);

        Assert.Equal(0, resultado.ContasComDivergencia);
        Assert.Empty(resultado.Itens);
    }

    [Fact]
    public async Task MigrarAjustesConciliacaoAsync_ComConfirmar_ConverteAjustesAntigosEMantemOResto()
    {
        var conta = CriarConta(saldoInicial: 0m);
        var dia = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 18), 0m, 0m, 0m);
        dia.Entradas.Add(new ItemFinanceiro { Descricao = "Ajuste de saldo", Valor = 684.06m, Categoria = "Receitas diversas", TipoCusto = "Receita" });
        dia.Saidas.Add(new ItemFinanceiroSaida { Descricao = "saldo", Valor = 10m, Categoria = "Devoluções", TipoCusto = LancamentoFiltro.TipoAjusteSaldo });
        dia.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Aluguel", Valor = 500m, Categoria = "Aluguel", TipoCusto = "CustoFixo" });
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { dia });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.MigrarAjustesConciliacaoAsync(confirmar: true);

        Assert.Equal(2, resultado.LancamentosConvertidos);
        Assert.Equal(LancamentoFiltro.TipoAjusteConciliacao, dia.Entradas.Single().TipoCusto);
        Assert.Equal(LancamentoFiltro.TipoAjusteConciliacao, dia.Saidas.Single(s => s.Descricao == "saldo").TipoCusto);
        Assert.Equal("CustoFixo", dia.Saidas.Single(s => s.Descricao == "Aluguel").TipoCusto);
    }

    [Fact]
    public async Task MigrarAjustesConciliacaoAsync_SemConfirmar_SoRelatorio()
    {
        var conta = CriarConta();
        var dia = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 18), 0m, 0m, 0m);
        dia.Entradas.Add(new ItemFinanceiro { Descricao = "Ajuste de saldo", Valor = 50m, Categoria = "Ajuste de saldo", TipoCusto = "Receita" });
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { dia });

        var resultado = await _sut.MigrarAjustesConciliacaoAsync(confirmar: false);

        Assert.Equal(("Receita", 1), (dia.Entradas[0].TipoCusto, resultado.LancamentosConvertidos));
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task RecalcularTodasAsContasAsync_ContaSemRegistros_EIgnorada()
    {
        var conta = CriarConta();
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        var resultado = await _sut.RecalcularTodasAsContasAsync(confirmar: false);

        Assert.Empty(resultado.Itens);
    }
}
