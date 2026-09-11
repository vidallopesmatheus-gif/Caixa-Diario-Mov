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
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly FaturaCartaoService _sut;

    // InMemory não é relacional — o serviço detecta isso (Database.IsRelational()) e não abre
    // transação, então os testes seguem simulando só via mocks dos repositórios.
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public FaturaCartaoServiceTests()
    {
        _sut = new FaturaCartaoService(
            _contaRepoMock.Object, _registroRepoMock.Object, _pagamentoRepoMock.Object, _auditMock.Object, CriarContexto());
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
}
