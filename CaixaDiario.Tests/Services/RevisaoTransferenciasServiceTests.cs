using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

// Money Vision ("transferência cria contrapartida falsa") — cliente teste, Nubank ↔ C6.
public class RevisaoTransferenciasServiceTests
{
    private readonly Guid _clienteId = Guid.NewGuid();
    private readonly ContaBancaria _nubank;
    private readonly ContaBancaria _c6;
    private readonly List<RegistroDiario> _registros = new();
    private readonly List<Transferencia> _transferencias = new();
    private readonly Dictionary<Guid, List<PeriodoExtrato>> _periodos = new();
    private readonly Mock<ITransferenciaRepository> _transferenciaRepo = new();
    private readonly Mock<IRegistroRepository> _registroRepo = new();
    private readonly RevisaoTransferenciasService _sut;

    public RevisaoTransferenciasServiceTests()
    {
        _nubank = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = _clienteId, Nome = "Nubank", Tipo = "ContaCorrente", Ativa = true, SaldoInicial = 5000m };
        _c6 = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = _clienteId, Nome = "C6 Bank", Tipo = "ContaCorrente", Ativa = true, SaldoInicial = 5000m };
        _periodos[_nubank.Id] = new();
        _periodos[_c6.Id] = new();

        var contaRepo = new Mock<IContaBancariaRepository>();
        contaRepo.Setup(r => r.ListarPorClienteAsync(_clienteId)).ReturnsAsync(new List<ContaBancaria> { _nubank, _c6 });
        contaRepo.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { _nubank, _c6 });
        _registroRepo.Setup(r => r.ListarPorClienteAsync(_clienteId)).ReturnsAsync(() => _registros.ToList());
        _registroRepo.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _transferenciaRepo.Setup(r => r.ListarPorClienteAsync(_clienteId)).ReturnsAsync(() => _transferencias.ToList());
        _transferenciaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => _transferencias.FirstOrDefault(t => t.Id == id));
        _transferenciaRepo.Setup(r => r.RemoverAsync(It.IsAny<Transferencia>())).Callback<Transferencia>(t => _transferencias.Remove(t)).Returns(Task.CompletedTask);
        var importRepo = new Mock<ITransacaoImportadaRepository>();
        importRepo.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<TransacaoImportada>());
        var periodoRepo = new Mock<IPeriodoExtratoRepository>();
        periodoRepo.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => _periodos[id]);

        _sut = new RevisaoTransferenciasService(contaRepo.Object, _registroRepo.Object, _transferenciaRepo.Object, importRepo.Object, periodoRepo.Object);
    }

    private RegistroDiario Dia(ContaBancaria conta, DateOnly data)
    {
        var r = _registros.FirstOrDefault(x => x.ContaBancariaId == conta.Id && x.Data == data);
        if (r != null) return r;
        r = new RegistroDiario { Id = Guid.NewGuid(), ClienteId = _clienteId, ContaBancariaId = conta.Id, Data = data, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new() };
        _registros.Add(r);
        return r;
    }

    private Guid Saida(ContaBancaria conta, DateOnly data, string descricao, decimal valor, Guid? transferenciaId = null, string? fitId = null)
    {
        var item = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = descricao, Valor = valor, TransferenciaId = transferenciaId, FitId = fitId,
            TipoCusto = transferenciaId.HasValue ? "Transferencia" : "CustoFixo", Categoria = transferenciaId.HasValue ? "Transferência" : "Administrativas" };
        Dia(conta, data).Saidas.Add(item);
        return item.Id;
    }

    private Guid Entrada(ContaBancaria conta, DateOnly data, string descricao, decimal valor, Guid? transferenciaId = null, string? fitId = null)
    {
        var item = new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = descricao, Valor = valor, TransferenciaId = transferenciaId, FitId = fitId,
            TipoCusto = transferenciaId.HasValue ? "Transferencia" : null };
        Dia(conta, data).Entradas.Add(item);
        return item.Id;
    }

    private Guid Transferencia(DateOnly data, decimal valor)
    {
        var t = new Transferencia { Id = Guid.NewGuid(), ClienteId = _clienteId, ContaOrigemId = _nubank.Id, ContaDestinoId = _c6.Id, Data = data, Valor = valor };
        _transferencias.Add(t);
        return t.Id;
    }

    private IEnumerable<ItemFinanceiroSaida> SaidasDo(ContaBancaria c) => _registros.Where(r => r.ContaBancariaId == c.Id).SelectMany(r => r.Saidas);
    private IEnumerable<ItemFinanceiro> EntradasDo(ContaBancaria c) => _registros.Where(r => r.ContaBancariaId == c.Id).SelectMany(r => r.Entradas);

    [Fact]
    public async Task CorrigirAsync_ContrapartidaCriadaELancamentoRealSolto_RealAssumeOParEACriadaSai()
    {
        var dia = new DateOnly(2026, 4, 20);
        var t = Transferencia(dia, 290m);
        Saida(_nubank, dia, "Pix recebido de Matheus Vidal Lopes", 290m, t);
        var real = Saida(_nubank, dia, "Transferência enviada pelo Pix - MATHEUS VIDAL", 290m, fitId: "N1");
        Entrada(_c6, dia, "Pix recebido de Matheus Vidal Lopes", 290m, t, "C1");

        var resultado = await _sut.CorrigirAsync(_clienteId, confirmar: true);

        var saida = Assert.Single(SaidasDo(_nubank));
        Assert.Equal((real, t, "Transferencia"), (saida.Id, saida.TransferenciaId!.Value, saida.TipoCusto));
        var saldo = Assert.Single(resultado.Saldos);
        Assert.Equal(290m, saldo.SaldoAtualDepois - saldo.SaldoAtualAntes);
        Assert.Equal(5000m - 290m, _registros.Single(r => r.ContaBancariaId == _nubank.Id).SaldoFinal);
    }

    [Fact]
    public async Task CorrigirAsync_DoisParesParaAMesmaTransferencia_FicaUmParComAsDuasPontasReais()
    {
        var dia = new DateOnly(2026, 9, 4);
        var t1 = Transferencia(dia, 2000m);
        var t2 = Transferencia(dia, 2000m);
        var envioReal = Saida(_nubank, dia, "Transferência enviada pelo Pix - Matheus", 2000m, t1, "N1");
        Entrada(_c6, dia, "Transferência enviada pelo Pix - Matheus", 2000m, t1);
        Saida(_nubank, dia, "Pix recebido de Matheus", 2000m, t2);
        var recebimentoReal = Entrada(_c6, dia, "Pix recebido de Matheus", 2000m, t2, "C1");

        var resultado = await _sut.CorrigirAsync(_clienteId, confirmar: true);

        var saida = Assert.Single(SaidasDo(_nubank));
        var entrada = Assert.Single(EntradasDo(_c6));
        Assert.Equal((envioReal, recebimentoReal), (saida.Id, entrada.Id));
        Assert.Equal(saida.TransferenciaId, entrada.TransferenciaId);
        Assert.Single(_transferencias);
        Assert.Equal(2000m, resultado.Saldos.Single(s => s.ContaBancariaId == _nubank.Id).SaldoAtualDepois - resultado.Saldos.Single(s => s.ContaBancariaId == _nubank.Id).SaldoAtualAntes);
        Assert.Equal(-2000m, resultado.Saldos.Single(s => s.ContaBancariaId == _c6.Id).SaldoAtualDepois - resultado.Saldos.Single(s => s.ContaBancariaId == _c6.Id).SaldoAtualAntes);
    }

    [Fact]
    public async Task ListarAsync_ContrapartidaNumDiaQueOExtratoCobreSemORecebimento_PedeParaOUsuarioDecidir()
    {
        var dia = new DateOnly(2026, 10, 14);
        var t = Transferencia(dia, 400m);
        Saida(_nubank, dia, "Transferência enviada pelo Pix - Matheus", 400m, t, "N1");
        Entrada(_c6, dia, "Transferência enviada pelo Pix - Matheus", 400m, t);
        _periodos[_c6.Id].Add(new PeriodoExtrato { ContaBancariaId = _c6.Id, De = new DateOnly(2026, 10, 1), Ate = new DateOnly(2026, 11, 8) });

        var revisao = await _sut.ListarAsync(_clienteId, _clienteId, "cliente");

        var item = Assert.Single(revisao.ParaDecidir);
        Assert.Equal((t, 400m, "C6 Bank", "Nubank"), (item.TransferenciaId, item.Valor, item.ContaCriadaNome, item.ContaOutraNome));
    }

    [Fact]
    public async Task CorrigirAsync_ContrapartidaEmMesSemExtrato_ViraProvisoriaComDescricaoPropriaSemMudarSaldo()
    {
        var dia = new DateOnly(2026, 6, 4);
        var t = Transferencia(dia, 3200m);
        Saida(_nubank, dia, "Pix recebido de Matheus", 3200m, t);
        Entrada(_c6, dia, "Pix recebido de Matheus", 3200m, t, "C1");

        var resultado = await _sut.CorrigirAsync(_clienteId, confirmar: true);

        var criada = Assert.Single(SaidasDo(_nubank));
        Assert.Equal((true, "↔ Transferência para C6 Bank"), (criada.Provisoria, criada.Descricao));
        Assert.All(resultado.Saldos, s => Assert.Equal(s.SaldoAtualAntes, s.SaldoAtualDepois));
    }

    [Fact]
    public async Task CorrigirAsync_SemConfirmar_SoRelatorioNadaMuda()
    {
        var dia = new DateOnly(2026, 4, 20);
        var t = Transferencia(dia, 290m);
        Saida(_nubank, dia, "Pix recebido de Matheus Vidal Lopes", 290m, t);
        Saida(_nubank, dia, "Transferência enviada pelo Pix - MATHEUS VIDAL", 290m, fitId: "N1");
        Entrada(_c6, dia, "Pix recebido de Matheus Vidal Lopes", 290m, t, "C1");

        var resultado = await _sut.CorrigirAsync(null, confirmar: false);

        Assert.Equal("DuplicadaComReal", Assert.Single(resultado.Itens).Tipo);
        Assert.Equal(2, SaidasDo(_nubank).Count());
        _registroRepo.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task DescartarContrapartidaAsync_NaoFoiParaEssaConta_MarcaComoProvisoriaEDesfazATransferencia()
    {
        var dia = new DateOnly(2026, 10, 14);
        var t = Transferencia(dia, 400m);
        Saida(_nubank, dia, "Transferência enviada pelo Pix - Matheus", 400m, t, "N1");
        var criadaId = Entrada(_c6, dia, "Transferência enviada pelo Pix - Matheus", 400m, t);
        _periodos[_c6.Id].Add(new PeriodoExtrato { ContaBancariaId = _c6.Id, De = new DateOnly(2026, 10, 1), Ate = new DateOnly(2026, 11, 8) });
        var transferenciaService = new Mock<ITransferenciaService>();
        var contaRepo = new Mock<IContaBancariaRepository>();
        contaRepo.Setup(r => r.ListarPorClienteAsync(_clienteId)).ReturnsAsync(new List<ContaBancaria> { _nubank, _c6 });
        var importRepo = new Mock<ITransacaoImportadaRepository>();
        importRepo.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<TransacaoImportada>());
        var periodoRepo = new Mock<IPeriodoExtratoRepository>();
        periodoRepo.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => _periodos[id]);
        var sut = new RevisaoTransferenciasService(contaRepo.Object, _registroRepo.Object, _transferenciaRepo.Object, importRepo.Object, periodoRepo.Object, transferenciaService.Object);

        await sut.DescartarContrapartidaAsync(t, _clienteId, "cliente");

        Assert.True(EntradasDo(_c6).Single(e => e.Id == criadaId).Provisoria);
        transferenciaService.Verify(s => s.DesfazerClassificacaoAsync(t, _clienteId, "cliente"), Times.Once);
    }

    [Fact]
    public async Task ConfirmarContrapartidaAsync_UsuarioDizQueFoiParaEssaConta_SaiDaRevisao()
    {
        var dia = new DateOnly(2026, 10, 14);
        var t = Transferencia(dia, 400m);
        Saida(_nubank, dia, "Transferência enviada pelo Pix - Matheus", 400m, t, "N1");
        Entrada(_c6, dia, "↔ Transferência de Nubank", 400m, t);
        _periodos[_c6.Id].Add(new PeriodoExtrato { ContaBancariaId = _c6.Id, De = new DateOnly(2026, 10, 1), Ate = new DateOnly(2026, 11, 8) });

        await _sut.ConfirmarContrapartidaAsync(t, _clienteId, "cliente");

        Assert.Empty((await _sut.ListarAsync(_clienteId, _clienteId, "cliente")).ParaDecidir);
    }
}
