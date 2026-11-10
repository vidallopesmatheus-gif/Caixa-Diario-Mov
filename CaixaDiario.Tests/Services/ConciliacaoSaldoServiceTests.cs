using CaixaDiario.API.DTOs.ConciliacaoSaldo;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ConciliacaoSaldoServiceTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 8);
    private readonly Mock<IContaBancariaRepository> _contaRepo = new();
    private readonly Mock<IRegistroRepository> _registroRepo = new();
    private readonly Mock<ITransacaoImportadaRepository> _importRepo = new();
    private readonly Mock<IPontoConciliacaoRepository> _pontoRepo = new();
    private readonly Mock<IPeriodoExtratoRepository> _periodoRepo = new();
    private readonly ContaBancaria _conta = new() { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Nome = "Nubank", Tipo = "ContaCorrente", SaldoInicial = 1000m, Ativa = true };
    private readonly RegistroDiario _dia;
    private readonly ConciliacaoSaldoService _sut;

    public ConciliacaoSaldoServiceTests()
    {
        _dia = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = _conta.ClienteId, ContaBancariaId = _conta.Id, Data = new DateOnly(2026, 11, 5),
            Inicio = 1000m, SaldoFinal = 900m, Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Mercado", Valor = 100m, FitId = "F1" } },
        };
        _contaRepo.Setup(r => r.ObterPorIdAsync(_conta.Id)).ReturnsAsync(_conta);
        _registroRepo.Setup(r => r.ListarPorContaAsync(_conta.Id)).ReturnsAsync(() => new List<RegistroDiario> { _dia });
        _registroRepo.Setup(r => r.ListarPorClienteAsync(_conta.ClienteId)).ReturnsAsync(() => new List<RegistroDiario> { _dia });
        _importRepo.Setup(r => r.ListarPorContaAsync(_conta.Id)).ReturnsAsync(new List<TransacaoImportada>());
        _periodoRepo.Setup(r => r.ListarPorContaAsync(_conta.Id)).ReturnsAsync(new List<PeriodoExtrato>
        {
            new() { ContaBancariaId = _conta.Id, De = new DateOnly(2026, 11, 1), Ate = new DateOnly(2026, 11, 7) },
        });
        _pontoRepo.Setup(r => r.ListarPorContaAsync(_conta.Id)).ReturnsAsync(new List<PontoConciliacao>());
        _pontoRepo.Setup(r => r.ListarPorClienteAsync(_conta.ClienteId)).ReturnsAsync(new List<PontoConciliacao>());
        _pontoRepo.Setup(r => r.SalvarAsync(It.IsAny<PontoConciliacao>())).ReturnsAsync((PontoConciliacao p) => p);
        _sut = new ConciliacaoSaldoService(_contaRepo.Object, _registroRepo.Object, _importRepo.Object, _pontoRepo.Object, _periodoRepo.Object, new RelogioFixo(Hoje));
    }

    [Fact]
    public async Task RegistrarSaldoBancoAsync_SaldoIgualAoDoApp_GravaPontoConciliadoSemAjuste()
    {
        var ponto = await _sut.RegistrarSaldoBancoAsync(_conta.Id, new RegistrarSaldoBancoDto { Data = new DateOnly(2026, 11, 7), SaldoBanco = 900m }, _conta.ClienteId, "cliente");

        Assert.True(ponto.Conciliado);
        _pontoRepo.Verify(r => r.SalvarAsync(It.Is<PontoConciliacao>(p => p.SaldoApp == 900m && p.Origem == "Informado")), Times.Once);
        _registroRepo.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarSaldoBancoAsync_DataFutura_LancaErro()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.RegistrarSaldoBancoAsync(_conta.Id, new RegistrarSaldoBancoDto { Data = Hoje.AddDays(1), SaldoBanco = 1m }, _conta.ClienteId, "cliente"));
        Assert.Equal("data", ex.Campo);
    }

    [Fact]
    public async Task ObterCoberturaAsync_DevolveDiasDoPeriodoSaldoPorDiaEStatus()
    {
        var cobertura = await _sut.ObterCoberturaAsync(_conta.Id, _conta.ClienteId, "cliente");

        Assert.Equal(7, cobertura.DiasComExtrato.Count);
        Assert.Equal(900m, Assert.Single(cobertura.SaldosPorDia).SaldoFinal);
        Assert.Equal((new DateOnly(2026, 11, 7), 1), (cobertura.Status.ExtratoAte!.Value, cobertura.Status.DiasSemExtrato!.Value));
    }

    [Fact]
    public async Task DiagnosticarAsync_SemSaldoInformadoNemPonto_ListaAsCausasSemDiferenca()
    {
        var d = await _sut.DiagnosticarAsync(_conta.Id, new DateOnly(2026, 11, 7), null, _conta.ClienteId, "cliente");

        Assert.Equal((null, null, 900m), (d.SaldoBanco, d.Diferenca, d.SaldoApp));
    }

    [Fact]
    public async Task DiagnosticarAsync_ParQueOUsuarioMandouManter_NaoApareceComoDuplicata()
    {
        var uber1 = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Uber", Valor = 7.95m };
        var uber2 = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Uber", Valor = 7.95m };
        _dia.Saidas = new() { uber1, uber2 };
        var dispensadaRepo = new Mock<IDuplicataDispensadaRepository>();
        var (menor, maior) = ConciliacaoSaldoCalculo.ParOrdenado(uber1.Id, uber2.Id);
        dispensadaRepo.Setup(r => r.ListarPorContaAsync(_conta.Id)).ReturnsAsync(new List<DuplicataDispensada>
        {
            new() { ContaBancariaId = _conta.Id, LancamentoMenorId = menor, LancamentoMaiorId = maior },
        });
        var sut = new ConciliacaoSaldoService(_contaRepo.Object, _registroRepo.Object, _importRepo.Object, _pontoRepo.Object,
            _periodoRepo.Object, new RelogioFixo(Hoje), dispensadaRepo.Object);

        var d = await sut.DiagnosticarAsync(_conta.Id, new DateOnly(2026, 11, 7), 984.10m, _conta.ClienteId, "cliente");

        Assert.Empty(d.Duplicatas);
    }

    [Fact]
    public async Task DiagnosticarAsync_AjusteDeSaldoNoPeriodo_ApareceComoCausaComEfeitoDeDesfazer()
    {
        _dia.Entradas = new() { new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Ajuste de saldo", Valor = 500m, TipoCusto = LancamentoFiltro.TipoAjusteConciliacao } };

        var d = await _sut.DiagnosticarAsync(_conta.Id, new DateOnly(2026, 11, 7), 900m, _conta.ClienteId, "cliente");

        Assert.Equal(-500m, Assert.Single(d.Ajustes).EfeitoSeCorrigir);
    }

    [Fact]
    public async Task ListarAjustesAsync_MostraOSaldoDoDiaComESemOAjuste()
    {
        var ajuste = new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Ajuste de saldo", Valor = 300m, TipoCusto = LancamentoFiltro.TipoAjusteConciliacao };
        _dia.Entradas = new() { ajuste };
        _dia.SaldoFinal = 1200m;

        var lista = await _sut.ListarAjustesAsync(_conta.Id, _conta.ClienteId, "cliente");

        var item = Assert.Single(lista);
        Assert.Equal((ajuste.Id, 300m, 900m, 1200m, false), (item.Id, item.Valor, item.SaldoDoDiaSemAjuste, item.SaldoDoDia, item.Confirmado));
    }

    [Fact]
    public async Task ConfirmarAjusteAsync_MarcaOAjusteComoConferido()
    {
        var ajuste = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "saldo", Valor = 50m, TipoCusto = LancamentoFiltro.TipoAjusteSaldo };
        _dia.Saidas = new() { ajuste };

        await _sut.ConfirmarAjusteAsync(_conta.Id, ajuste.Id, _conta.ClienteId, "cliente");

        Assert.True(Assert.Single(_dia.Saidas).AjusteConfirmado);
        _registroRepo.Verify(r => r.AtualizarAsync(_dia), Times.Once);
    }

    [Fact]
    public async Task ConfirmarAjusteAsync_LancamentoQueNaoEAjuste_LancaNaoEncontrado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ConfirmarAjusteAsync(_conta.Id, _dia.Saidas[0].Id, _conta.ClienteId, "cliente"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ObterStatusPorContaAsync_TabelaDeConciliacaoIndisponivel_DevolveVazioSemLancar()
    {
        _pontoRepo.Setup(r => r.ListarPorClienteAsync(_conta.ClienteId)).ThrowsAsync(new InvalidOperationException("relation \"pontos_conciliacao\" does not exist"));

        var status = await _sut.ObterStatusPorContaAsync(new List<ContaBancaria> { _conta }, new List<RegistroDiario> { _dia });

        Assert.Empty(status);
    }

    [Fact]
    public async Task ObterStatusPorContaAsync_UmaContaFalha_AsOutrasContinuamComStatus()
    {
        var quebrada = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = _conta.ClienteId, Nome = "Bradesco", Tipo = "ContaCorrente", Ativa = true };
        _periodoRepo.Setup(r => r.ListarPorContaAsync(quebrada.Id)).ThrowsAsync(new InvalidOperationException("falha"));
        _importRepo.Setup(r => r.ListarPorContaAsync(quebrada.Id)).ReturnsAsync(new List<TransacaoImportada>());

        var status = await _sut.ObterStatusPorContaAsync(new List<ContaBancaria> { quebrada, _conta }, new List<RegistroDiario> { _dia });

        Assert.Equal(new[] { _conta.Id }, status.Keys);
    }

    [Fact]
    public async Task DiagnosticarAsync_UsaOSaldoDoPontoJaRegistrado()
    {
        _pontoRepo.Setup(r => r.ListarPorContaAsync(_conta.Id)).ReturnsAsync(new List<PontoConciliacao>
        {
            new() { ContaBancariaId = _conta.Id, Data = new DateOnly(2026, 11, 7), SaldoBanco = 1000m },
        });

        var d = await _sut.DiagnosticarAsync(_conta.Id, new DateOnly(2026, 11, 7), null, _conta.ClienteId, "cliente");

        Assert.Equal(100m, d.Diferenca);
    }

    [Fact]
    public async Task ObterCoberturaAsync_ClienteDeOutraConta_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterCoberturaAsync(_conta.Id, Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ObterStatusPorContaAsync_UmStatusPorConta()
    {
        var status = await _sut.ObterStatusPorContaAsync(new List<ContaBancaria> { _conta }, new List<RegistroDiario> { _dia });

        Assert.Equal(900m, status[_conta.Id].SaldoApp);
    }
}
