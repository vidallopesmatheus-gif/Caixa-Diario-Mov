using CaixaDiario.API.Data;
using CaixaDiario.API.DTOs.FaturasCartao;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CaixaDiario.Tests.Services;

public class FaturaCartaoServiceTests
{
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IPagamentoFaturaRepository> _pagamentoRepoMock = new();
    private readonly Mock<ICategoriaRepository> _categoriaRepoMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly Mock<IFaturaRepository> _faturaRepoMock = new();
    private readonly FaturaCartaoService _sut;

    // InMemory não é relacional — o serviço detecta isso (Database.IsRelational()) e não abre
    // transação, então os testes seguem simulando só via mocks dos repositórios.
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public FaturaCartaoServiceTests()
    {
        _categoriaRepoMock.Setup(r => r.ListarTodasPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Categoria>());
        _sut = new FaturaCartaoService(
            _contaRepoMock.Object, _registroRepoMock.Object, _pagamentoRepoMock.Object, _categoriaRepoMock.Object,
            _auditMock.Object, CriarContexto(), _faturaRepoMock.Object);
    }

    private static ContaBancaria CriarConta(
        Guid clienteId, string nome, string tipo, decimal saldoInicial = 0m,
        bool ativa = true, int? diaFechamento = null, int? diaVencimento = null) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Nome = nome, Tipo = tipo, SaldoInicial = saldoInicial,
        Ativa = ativa, DataCriacao = DateTime.UtcNow, DiaFechamento = diaFechamento, DiaVencimento = diaVencimento,
    };

    // ── VincularPagamentoAsync ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task VincularPagamentoAsync_PagamentoTotal_RelabelaOrigemEAbateSaldoDoCartao()
    {
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Nubank Conta", "ContaCorrente", saldoInicial: 2000m);
        var contaCartao = CriarConta(clienteId, "Nubank Cartão", "CartaoCredito");
        var data = new DateOnly(2026, 9, 5);
        var lancamentoId = Guid.NewGuid();

        var registroOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCorrente.Id, Data = data, Inicio = 2500m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 2000m,
            Saidas = new() { new ItemFinanceiroSaida { Id = lancamentoId, Descricao = "Pagamento fatura Nubank", Valor = 500m, Categoria = "", PendenteCategorizacao = true } },
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorrente.Id)).ReturnsAsync(contaCorrente);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCorrente.Id, data)).ReturnsAsync(registroOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCartao.Id, data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaCartao.Id)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.AtualizarAsync(registroOrigem)).ReturnsAsync(registroOrigem);

        RegistroDiario? registroCartaoCriado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => registroCartaoCriado = r).ReturnsAsync((RegistroDiario r) => r);
        _pagamentoRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoFatura>())).ReturnsAsync((PagamentoFatura p) => p);

        var dto = new VincularPagamentoFaturaDto
        {
            ContaOrigemId = contaCorrente.Id, LancamentoId = lancamentoId, Data = data,
            ContaCartaoId = contaCartao.Id, Competencia = "2026-09",
        };

        var resultado = await _sut.VincularPagamentoAsync(dto, clienteId, "cliente");

        // A saída original só é relabelada — seu valor/efeito no saldo já aplicado não muda.
        var saidaOriginal = Assert.Single(registroOrigem.Saidas);
        Assert.Equal("PagamentoFatura", saidaOriginal.TipoCusto);
        Assert.False(saidaOriginal.PendenteCategorizacao);
        Assert.Equal(2000m, registroOrigem.SaldoFinal);

        // O cartão ganha uma entrada sintética que reduz a dívida (SaldoInicial 0 -> +500).
        Assert.NotNull(registroCartaoCriado);
        var entradaCartao = Assert.Single(registroCartaoCriado!.Entradas);
        Assert.Equal("PagamentoFatura", entradaCartao.TipoCusto);
        Assert.Equal(500m, entradaCartao.Valor);
        Assert.Equal(500m, registroCartaoCriado.SaldoFinal);
        Assert.Equal(saidaOriginal.PagamentoFaturaId, entradaCartao.PagamentoFaturaId);

        // Essa entrada nunca deve contar como receita/despesa em nenhuma métrica.
        Assert.False(LancamentoFiltro.EhOperacional(entradaCartao.TipoCusto));
        Assert.False(LancamentoFiltro.EhOperacional(saidaOriginal.TipoCusto));

        Assert.Equal(500m, resultado.ValorPago);
        Assert.Equal("2026-09", resultado.Competencia);
    }

    [Fact]
    public async Task VincularPagamentoAsync_ContaContrapartidaNaoECartao_LancaErro()
    {
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Conta Corrente", "ContaCorrente");
        var outraContaCorrente = CriarConta(clienteId, "Outra Conta", "ContaCorrente");

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorrente.Id)).ReturnsAsync(contaCorrente);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(outraContaCorrente.Id)).ReturnsAsync(outraContaCorrente);

        var dto = new VincularPagamentoFaturaDto
        {
            ContaOrigemId = contaCorrente.Id, LancamentoId = Guid.NewGuid(), Data = new DateOnly(2026, 9, 5),
            ContaCartaoId = outraContaCorrente.Id, Competencia = "2026-09",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.VincularPagamentoAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CONTA_NAO_E_CARTAO, ex.Codigo);
    }

    [Fact]
    public async Task VincularPagamentoAsync_LancamentoJaClassificado_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Conta Corrente", "ContaCorrente");
        var contaCartao = CriarConta(clienteId, "Cartão", "CartaoCredito");
        var data = new DateOnly(2026, 9, 5);
        var lancamentoId = Guid.NewGuid();

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCorrente.Id, Data = data,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
            Saidas = new() { new ItemFinanceiroSaida { Id = lancamentoId, Valor = 100m, TransferenciaId = Guid.NewGuid() } },
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorrente.Id)).ReturnsAsync(contaCorrente);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCorrente.Id, data)).ReturnsAsync(registro);

        var dto = new VincularPagamentoFaturaDto
        {
            ContaOrigemId = contaCorrente.Id, LancamentoId = lancamentoId, Data = data,
            ContaCartaoId = contaCartao.Id, Competencia = "2026-09",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.VincularPagamentoAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task VincularPagamentoAsync_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Conta Corrente", "ContaCorrente");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorrente.Id)).ReturnsAsync(contaCorrente);

        var dto = new VincularPagamentoFaturaDto
        {
            ContaOrigemId = contaCorrente.Id, LancamentoId = Guid.NewGuid(), Data = new DateOnly(2026, 9, 5),
            ContaCartaoId = Guid.NewGuid(), Competencia = "2026-09",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.VincularPagamentoAsync(dto, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    // ── PagarFaturaAsync (item F4 do bundle D6JWEAJa) ──────────────────────────────────────────────

    private (ContaBancaria contaCartao, ContaBancaria contaOrigem, List<RegistroDiario> registrosCartao) CriarCenarioFatura(
        Guid clienteId, decimal valorCompra = 300m)
    {
        var contaCartao = CriarConta(clienteId, "Cartão", "CartaoCredito", diaFechamento: 15);
        var contaOrigem = CriarConta(clienteId, "Conta Corrente", "ContaCorrente", saldoInicial: 1000m);
        var registrosCartao = new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCartao.Id, Data = new DateOnly(2020, 1, 10),
                Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = -valorCompra,
                Saidas = new() { new ItemFinanceiroSaida { Valor = valorCompra, Categoria = "Software" } },
            },
        };
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaOrigem.Id)).ReturnsAsync(contaOrigem);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaCartao.Id)).ReturnsAsync(registrosCartao);
        _pagamentoRepoMock.Setup(r => r.ListarPorContaCartaoAsync(contaCartao.Id)).ReturnsAsync(new List<PagamentoFatura>());
        _pagamentoRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoFatura>())).ReturnsAsync((PagamentoFatura p) => p);
        return (contaCartao, contaOrigem, registrosCartao);
    }

    [Fact]
    public async Task PagarFaturaAsync_SemValorInformado_PagaOSaldoDevedorIntegralCriandoAsDuasPontasDoZero()
    {
        var clienteId = Guid.NewGuid();
        var (contaCartao, contaOrigem, _) = CriarCenarioFatura(clienteId);
        var dataPagamento = new DateOnly(2020, 2, 1);

        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaOrigem.Id, dataPagamento)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaOrigem.Id)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCartao.Id, dataPagamento)).ReturnsAsync((RegistroDiario?)null);

        var registrosCriados = new List<RegistroDiario>();
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(registrosCriados.Add).ReturnsAsync((RegistroDiario r) => r);

        var dto = new PagarFaturaDto { ContaOrigemId = contaOrigem.Id, Data = dataPagamento, Competencia = "2020-01" };
        var resultado = await _sut.PagarFaturaAsync(contaCartao.Id, dto, clienteId, "cliente");

        Assert.Equal(300m, resultado.ValorPago);
        var regOrigemCriado = registrosCriados.Single(r => r.ContaBancariaId == contaOrigem.Id);
        var saidaOrigem = Assert.Single(regOrigemCriado.Saidas);
        Assert.Equal(300m, saidaOrigem.Valor);
        Assert.Equal(LancamentoFiltro.TipoPagamentoFatura, saidaOrigem.TipoCusto);
        Assert.Equal(1000m - 300m, regOrigemCriado.SaldoFinal);

        var regCartaoCriado = registrosCriados.Single(r => r.ContaBancariaId == contaCartao.Id);
        var entradaCartao = Assert.Single(regCartaoCriado.Entradas);
        Assert.Equal(300m, entradaCartao.Valor);
        Assert.Equal(saidaOrigem.PagamentoFaturaId, entradaCartao.PagamentoFaturaId);
    }

    [Fact]
    public async Task PagarFaturaAsync_ValorParcial_PagaSoOInformado()
    {
        var clienteId = Guid.NewGuid();
        var (contaCartao, contaOrigem, _) = CriarCenarioFatura(clienteId);
        var dataPagamento = new DateOnly(2020, 2, 1);

        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaOrigem.Id, dataPagamento)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaOrigem.Id)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCartao.Id, dataPagamento)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new PagarFaturaDto { ContaOrigemId = contaOrigem.Id, Data = dataPagamento, Competencia = "2020-01", Valor = 100m };
        var resultado = await _sut.PagarFaturaAsync(contaCartao.Id, dto, clienteId, "cliente");

        Assert.Equal(100m, resultado.ValorPago);
    }

    [Fact]
    public async Task PagarFaturaAsync_ContaOrigemECartao_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var contaCartao = CriarConta(clienteId, "Cartão", "CartaoCredito", diaFechamento: 15);
        var outroCartao = CriarConta(clienteId, "Outro Cartão", "CartaoCredito");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(outroCartao.Id)).ReturnsAsync(outroCartao);

        var dto = new PagarFaturaDto { ContaOrigemId = outroCartao.Id, Data = new DateOnly(2020, 2, 1), Competencia = "2020-01" };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.PagarFaturaAsync(contaCartao.Id, dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task PagarFaturaAsync_FaturaInexistente_LancaNaoEncontrado()
    {
        var clienteId = Guid.NewGuid();
        var (contaCartao, contaOrigem, _) = CriarCenarioFatura(clienteId);

        var dto = new PagarFaturaDto { ContaOrigemId = contaOrigem.Id, Data = new DateOnly(2020, 2, 1), Competencia = "2099-12" };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.PagarFaturaAsync(contaCartao.Id, dto, clienteId, "cliente"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task PagarFaturaAsync_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var (contaCartao, contaOrigem, _) = CriarCenarioFatura(clienteId);

        var dto = new PagarFaturaDto { ContaOrigemId = contaOrigem.Id, Data = new DateOnly(2020, 2, 1), Competencia = "2020-01" };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.PagarFaturaAsync(contaCartao.Id, dto, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    // ── DesvincularPagamentoAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DesvincularPagamentoAsync_RevertePontaOrigemERemoveEntradaSinteticaDoCartao()
    {
        var clienteId = Guid.NewGuid();
        var pagamentoId = Guid.NewGuid();
        var contaCorrenteId = Guid.NewGuid();
        var contaCartaoId = Guid.NewGuid();
        var data = new DateOnly(2026, 9, 5);

        var pagamento = new PagamentoFatura
        {
            Id = pagamentoId, ClienteId = clienteId, ContaOrigemId = contaCorrenteId, ContaCartaoId = contaCartaoId,
            Competencia = "2026-09", ValorPago = 500m, Data = data, CriadoEm = DateTime.UtcNow,
        };

        var regOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCorrenteId, Data = data,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 2000m,
            Saidas = new() { new ItemFinanceiroSaida { Id = Guid.NewGuid(), Valor = 500m, Categoria = "Pagamento de fatura", TipoCusto = "PagamentoFatura", PagamentoFaturaId = pagamentoId } },
        };
        var regCartao = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCartaoId, Data = data,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 500m,
            Entradas = new() { new ItemFinanceiro { Id = Guid.NewGuid(), Valor = 500m, Categoria = "Pagamento de fatura", TipoCusto = "PagamentoFatura", PagamentoFaturaId = pagamentoId } },
        };

        _pagamentoRepoMock.Setup(r => r.ObterPorIdAsync(pagamentoId)).ReturnsAsync(pagamento);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCorrenteId, data)).ReturnsAsync(regOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCartaoId, data)).ReturnsAsync(regCartao);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        await _sut.DesvincularPagamentoAsync(pagamentoId, clienteId, "cliente");

        var saidaRevertida = Assert.Single(regOrigem.Saidas);
        Assert.Equal(string.Empty, saidaRevertida.Categoria);
        Assert.Null(saidaRevertida.TipoCusto);
        Assert.Null(saidaRevertida.PagamentoFaturaId);
        Assert.True(saidaRevertida.PendenteCategorizacao);

        Assert.Empty(regCartao.Entradas);
        Assert.Equal(0m, regCartao.SaldoFinal); // 500 - 500 (entrada sintética removida)

        _pagamentoRepoMock.Verify(r => r.RemoverAsync(pagamento), Times.Once);
    }

    [Fact]
    public async Task DesvincularPagamentoAsync_PagamentoInexistente_LancaNaoEncontrado()
    {
        _pagamentoRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((PagamentoFatura?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.DesvincularPagamentoAsync(Guid.NewGuid(), Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.PAGAMENTO_FATURA_NAO_ENCONTRADO, ex.Codigo);
    }

    // ── ObterCompetencia ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2026, 9, 10, 15, "2026-09")]  // antes do fechamento -> cai na fatura do próprio mês
    [InlineData(2026, 9, 20, 15, "2026-10")]  // depois do fechamento -> cai na fatura do mês seguinte
    [InlineData(2026, 12, 20, 15, "2027-01")] // vira o ano
    public void ObterCompetencia_ComDiaFechamento_ClassificaPeloCicloCorreto(
        int ano, int mes, int dia, int diaFechamento, string esperado)
    {
        var competencia = FaturaCartaoService.ObterCompetencia(new DateOnly(ano, mes, dia), diaFechamento);
        Assert.Equal(esperado, competencia);
    }

    [Fact]
    public void ObterCompetencia_SemDiaFechamento_UsaMesCalendario()
    {
        var competencia = FaturaCartaoService.ObterCompetencia(new DateOnly(2026, 9, 20), null);
        Assert.Equal("2026-09", competencia);
    }

    // ── ListarFaturasAsync ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListarFaturasAsync_SomaComprasEPagamentosPorCompetencia_CalculaSaldoDevedor()
    {
        var clienteId = Guid.NewGuid();
        var contaCartao = CriarConta(clienteId, "Cartão", "CartaoCredito", diaFechamento: 15);

        // Competência bem no passado (2020) — fechamento sempre já ocorreu, teste determinístico
        // independente de quando rodar.
        var registros = new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCartao.Id, Data = new DateOnly(2020, 1, 10),
                Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = -300m,
                Saidas = new() { new ItemFinanceiroSaida { Valor = 300m, Categoria = "Software" } },
            },
        };
        var pagamentos = new List<PagamentoFatura>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, ContaCartaoId = contaCartao.Id, Competencia = "2020-01", ValorPago = 100m, Data = new DateOnly(2020, 2, 1) },
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaCartao.Id)).ReturnsAsync(registros);
        _pagamentoRepoMock.Setup(r => r.ListarPorContaCartaoAsync(contaCartao.Id)).ReturnsAsync(pagamentos);

        var faturas = await _sut.ListarFaturasAsync(contaCartao.Id, clienteId, "cliente");

        var fatura = Assert.Single(faturas);
        Assert.Equal("2020-01", fatura.Competencia);
        Assert.Equal(300m, fatura.ValorTotal);
        Assert.Equal(100m, fatura.ValorPago);
        Assert.Equal(200m, fatura.SaldoDevedor);
        Assert.Equal("Fechada", fatura.Status); // fechou em 15/01/2020, sempre no passado
    }

    [Fact]
    public async Task ListarFaturasAsync_ContaNaoECartao_LancaErro()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Conta Corrente", "ContaCorrente");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ListarFaturasAsync(conta.Id, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CONTA_NAO_E_CARTAO, ex.Codigo);
    }

    // ── LancarComprasParceladaAsync (item 9) ─────────────────────────────────────────────────────

    /// <summary>Fake simples por trás dos mocks de IRegistroRepository — ResolverOuCriarAsync e
    /// RecalcularDiasSeguintesAsync precisam ver, em cada iteração do parcelamento, o que a
    /// iteração anterior já persistiu (senão o Inicio nunca encadeia de uma parcela pra outra).</summary>
    private List<RegistroDiario> ConfigurarRegistrosFake(Guid contaCartaoId)
    {
        var persistidos = new List<RegistroDiario>();
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCartaoId, It.IsAny<DateOnly>()))
            .ReturnsAsync((Guid _, DateOnly d) => persistidos.FirstOrDefault(r => r.Data == d));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaCartaoId)).ReturnsAsync(() => persistidos.ToList());
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(persistidos.Add).ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        return persistidos;
    }

    [Fact]
    public async Task LancarComprasParceladaAsync_CriaUmaSaidaPorParcelaEmMesesConsecutivosEEncadeiaOSaldo()
    {
        var clienteId = Guid.NewGuid();
        var contaCartao = CriarConta(clienteId, "Cartão", "CartaoCredito");
        var dataCompra = new DateOnly(2026, 1, 10);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);
        _categoriaRepoMock.Setup(r => r.ListarTodasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<Categoria> { new() { Nome = "Equipamentos", Tipo = "DespesaNaoOperacional" } });
        var persistidos = ConfigurarRegistrosFake(contaCartao.Id);

        var dto = new LancarCompraParceladaDto
        {
            Descricao = "Notebook", ValorTotal = 300m, Categoria = "Equipamentos", QuantidadeParcelas = 3, Data = dataCompra,
        };

        await _sut.LancarComprasParceladaAsync(contaCartao.Id, clienteId, "cliente", dto);

        Assert.Equal(3, persistidos.Count);
        var p1 = persistidos.Single(r => r.Data == dataCompra);
        var p2 = persistidos.Single(r => r.Data == dataCompra.AddMonths(1));
        var p3 = persistidos.Single(r => r.Data == dataCompra.AddMonths(2));

        var s1 = Assert.Single(p1.Saidas);
        Assert.Equal("Notebook (1/3)", s1.Descricao);
        Assert.Equal(100m, s1.Valor);
        Assert.Equal("DespesaNaoOperacional", s1.TipoCusto);
        Assert.Equal(1, s1.NumeroParcela);
        Assert.Equal(3, s1.TotalParcelas);
        Assert.NotNull(s1.ParcelamentoId);

        var s2 = Assert.Single(p2.Saidas);
        var s3 = Assert.Single(p3.Saidas);
        Assert.Equal("Notebook (2/3)", s2.Descricao);
        Assert.Equal("Notebook (3/3)", s3.Descricao);
        Assert.Equal(s1.ParcelamentoId, s2.ParcelamentoId);
        Assert.Equal(s1.ParcelamentoId, s3.ParcelamentoId);

        // Cada parcela parte do saldo onde a anterior deixou — nunca reseta nem acumula errado.
        Assert.Equal(0m, p1.Inicio);
        Assert.Equal(-100m, p1.SaldoFinal);
        Assert.Equal(-100m, p2.Inicio);
        Assert.Equal(-200m, p2.SaldoFinal);
        Assert.Equal(-200m, p3.Inicio);
        Assert.Equal(-300m, p3.SaldoFinal);
    }

    [Fact]
    public async Task LancarComprasParceladaAsync_ValorNaoDivisivel_UltimaParcelaAbsorveOArredondamento()
    {
        var clienteId = Guid.NewGuid();
        var contaCartao = CriarConta(clienteId, "Cartão", "CartaoCredito");
        var dataCompra = new DateOnly(2026, 1, 10);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);
        var persistidos = ConfigurarRegistrosFake(contaCartao.Id);

        var dto = new LancarCompraParceladaDto
        {
            Descricao = "Compra", ValorTotal = 100m, Categoria = "Software", QuantidadeParcelas = 3, Data = dataCompra,
        };

        await _sut.LancarComprasParceladaAsync(contaCartao.Id, clienteId, "cliente", dto);

        var valores = persistidos.OrderBy(r => r.Data).Select(r => r.Saidas.Single().Valor).ToList();
        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, valores);
        Assert.Equal(100m, valores.Sum());
    }

    [Fact]
    public async Task LancarComprasParceladaAsync_ContaNaoECartao_LancaErro()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Conta Corrente", "ContaCorrente");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new LancarCompraParceladaDto
        {
            Descricao = "Compra", ValorTotal = 300m, Categoria = "Equipamentos", QuantidadeParcelas = 3, Data = new DateOnly(2026, 1, 10),
        };

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => _sut.LancarComprasParceladaAsync(conta.Id, clienteId, "cliente", dto));

        Assert.Equal(CodigoRetorno.CONTA_NAO_E_CARTAO, ex.Codigo);
    }

    [Fact]
    public async Task LancarComprasParceladaAsync_MenosDeDuasParcelas_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var contaCartao = CriarConta(clienteId, "Cartão", "CartaoCredito");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);

        var dto = new LancarCompraParceladaDto
        {
            Descricao = "Compra", ValorTotal = 300m, Categoria = "Equipamentos", QuantidadeParcelas = 1, Data = new DateOnly(2026, 1, 10),
        };

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => _sut.LancarComprasParceladaAsync(contaCartao.Id, clienteId, "cliente", dto));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    // ── ListarBancosAsync (Bloco 2) ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListarBancosAsync_RetornaBancosDistintosDoCliente()
    {
        var clienteId = Guid.NewGuid();
        var contaCartao = CriarConta(clienteId, "Cartões", "CartaoCredito");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);
        _faturaRepoMock.Setup(r => r.ListarBancosDistintosPorClienteAsync(clienteId))
            .ReturnsAsync(new List<string> { "C6", "Nubank" });

        var bancos = await _sut.ListarBancosAsync(contaCartao.Id, clienteId, "cliente");

        Assert.Equal(new[] { "C6", "Nubank" }, bancos);
    }

    [Fact]
    public async Task ListarBancosAsync_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var contaCartao = CriarConta(clienteId, "Cartões", "CartaoCredito");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartao.Id)).ReturnsAsync(contaCartao);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => _sut.ListarBancosAsync(contaCartao.Id, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }
}
