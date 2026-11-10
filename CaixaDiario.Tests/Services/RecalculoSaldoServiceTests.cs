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

    // ── ConsolidarAjustesAsync (Money Vision: "ajustes da migração que o usuário não entende") ──

    private static RegistroDiario Dia(ContaBancaria conta, DateOnly data, params (string Descricao, decimal Valor, string? TipoCusto)[] itens) => new()
    {
        Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Data = data, ContasReceber = new(), ContasPagar = new(),
        Entradas = itens.Where(i => i.Valor > 0).Select(i => new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = i.Descricao, Valor = i.Valor, TipoCusto = i.TipoCusto }).ToList(),
        Saidas = itens.Where(i => i.Valor < 0).Select(i => new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = i.Descricao, Valor = -i.Valor, TipoCusto = i.TipoCusto }).ToList(),
    };

    private List<RegistroDiario> ConfigurarConta(ContaBancaria conta, params RegistroDiario[] dias)
    {
        SaldoEncadeamento.Encadear(conta.SaldoInicial, dias);
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(dias.ToList());
        return dias.ToList();
    }

    [Fact]
    public async Task ConsolidarAjustesAsync_ParQueZeraEReabreAConta_ViraUmAjusteLiquidoSemMudarOSaldo()
    {
        var conta = CriarConta(saldoInicial: 3895.76m);
        var dias = ConfigurarConta(conta,
            Dia(conta, new DateOnly(2026, 9, 30), ("saldo", -3895.76m, "AjusteSaldo")),
            Dia(conta, new DateOnly(2026, 10, 1), ("saldo", 132.66m, "AjusteSaldo"), ("Venda", 50m, "Receita")));

        var resultado = await _sut.ConsolidarAjustesAsync(confirmar: true);

        var item = Assert.Single(resultado.Itens);
        Assert.Equal(("Consolidar", -3763.10m), (item.Acao, item.ValorFinal!.Value));
        var ajuste = Assert.Single(dias[0].Saidas);
        Assert.Equal(3763.10m, ajuste.Valor);
        Assert.Equal("Ajuste automático da migração — saldo informado anteriormente em 01/10 era R$ 182,66", ajuste.Descricao);
        Assert.Equal(LancamentoFiltro.TipoAjusteConciliacao, ajuste.TipoCusto);
        Assert.DoesNotContain(dias[1].Entradas, e => e.Descricao == "saldo");
        Assert.Equal((182.66m, 182.66m), (item.SaldoAtualAntes, dias[1].SaldoFinal));
    }

    [Fact]
    public async Task ConsolidarAjustesAsync_ParQueSeAnula_RemoveOsDois()
    {
        var conta = CriarConta(saldoInicial: 1022.30m);
        var dias = ConfigurarConta(conta,
            Dia(conta, new DateOnly(2026, 9, 30), ("saldo", -1022.30m, "AjusteSaldo")),
            Dia(conta, new DateOnly(2026, 10, 1), ("saldo", 1022.30m, "AjusteSaldo")));

        var resultado = await _sut.ConsolidarAjustesAsync(confirmar: true);

        Assert.Equal("RemoverPar", Assert.Single(resultado.Itens).Acao);
        Assert.All(dias, d => Assert.Empty(d.Entradas.Concat<object>(d.Saidas)));
        Assert.Equal(1022.30m, dias[1].SaldoFinal);
    }

    [Fact]
    public async Task ConsolidarAjustesAsync_SemConfirmar_SoRelatorioNadaMudaNemEmMemoria()
    {
        var conta = CriarConta(saldoInicial: 1022.30m);
        var dias = ConfigurarConta(conta,
            Dia(conta, new DateOnly(2026, 9, 30), ("saldo", -1022.30m, "AjusteSaldo")),
            Dia(conta, new DateOnly(2026, 10, 1), ("saldo", 1022.30m, "AjusteSaldo")));

        var resultado = await _sut.ConsolidarAjustesAsync(confirmar: false);

        Assert.Single(resultado.Itens);
        Assert.Single(dias[0].Saidas);
        Assert.Single(dias[1].Entradas);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task ConsolidarAjustesAsync_AjusteSoltoLancadoPeloUsuario_FicaComoEsta()
    {
        var conta = CriarConta();
        ConfigurarConta(conta, Dia(conta, new DateOnly(2026, 11, 7), ("Ajuste de saldo", 11809.52m, LancamentoFiltro.TipoAjusteConciliacao)));

        var resultado = await _sut.ConsolidarAjustesAsync(confirmar: true);

        Assert.Empty(resultado.Itens);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }
}
