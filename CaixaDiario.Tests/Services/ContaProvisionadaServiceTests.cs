using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ContaProvisionadaServiceTests
{
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaBancariaRepoMock = new();
    private readonly ContaProvisionadaService _sut;

    public ContaProvisionadaServiceTests()
    {
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _sut = new ContaProvisionadaService(_registroRepoMock.Object, _contaBancariaRepoMock.Object);
    }

    private static ContaBancaria CriarConta(Guid clienteId, decimal saldoInicial = 0m) =>
        new() { Id = Guid.NewGuid(), ClienteId = clienteId, Ativa = true, SaldoInicial = saldoInicial };

    [Fact]
    public async Task CriarAsync_TipoInvalido_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var dto = new CriarContaProvisionadaDto { ClienteId = clienteId, ContaBancariaId = Guid.NewGuid(), Tipo = "Invalido", Descricao = "x", Valor = 10m };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto, clienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_Valido_CriaItemComIdNoRegistroDaContaCerta()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var data = new DateOnly(2026, 10, 10);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        var dto = new CriarContaProvisionadaDto
        {
            ClienteId = clienteId, ContaBancariaId = conta.Id, Tipo = "Pagar",
            Descricao = "Telefone", Valor = 30m, DataVencimento = data,
        };

        var resultado = await _sut.CriarAsync(dto, clienteId, "cliente");

        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal("Telefone", resultado.Descricao);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.Is<RegistroDiario>(reg =>
            reg.ContaBancariaId == conta.Id && reg.ContasPagar.Single().Descricao == "Telefone")), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_ItemNaoEncontrado_LancaNaoEncontrado()
    {
        var clienteId = Guid.NewGuid();
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(clienteId, Guid.NewGuid(), new AtualizarContaProvisionadaDto(), clienteId, "cliente"));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task AtualizarAsync_EdicaoSimples_AtualizaCamposSemTocarEmPago()
    {
        var clienteId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = new DateOnly(2026, 10, 10),
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada> { new() { Id = itemId, Descricao = "Telefone", Valor = 30m, Pago = false } },
        };
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.AtualizarAsync(clienteId, itemId,
            new AtualizarContaProvisionadaDto { Valor = 45m, Descricao = "Telefone + internet" }, clienteId, "cliente");

        Assert.Equal(45m, resultado.Valor);
        Assert.Equal("Telefone + internet", resultado.Descricao);
        Assert.False(resultado.Pago);
    }

    [Fact]
    public async Task AtualizarAsync_Baixa_CriaLancamentoRealNaDataDoPagamentoEAjustaSaldo()
    {
        var clienteId = Guid.NewGuid();
        var contaOrigem = CriarConta(clienteId);
        var contaPagamento = CriarConta(clienteId, saldoInicial: 100m);
        var itemId = Guid.NewGuid();
        var dataVencimento = new DateOnly(2026, 10, 10);
        var dataPagamento = new DateOnly(2026, 10, 12); // pago com atraso, dia diferente do vencimento

        var registroOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaOrigem.Id, Data = dataVencimento,
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Id = itemId, Descricao = "Telefone", Valor = 30m, DataVencimento = dataVencimento, Pago = false, ContaBancariaId = contaPagamento.Id },
            },
        };
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroOrigem });
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaPagamento.Id)).ReturnsAsync(contaPagamento);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaPagamento.Id, dataPagamento)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaPagamento.Id)).ReturnsAsync(new List<RegistroDiario>());

        var resultado = await _sut.AtualizarAsync(clienteId, itemId,
            new AtualizarContaProvisionadaDto { Pago = true, DataPagamento = dataPagamento }, clienteId, "cliente");

        Assert.True(resultado.Pago);
        Assert.Equal(dataPagamento, resultado.DataBaixa);
        Assert.NotNull(resultado.LancamentoVinculadoId);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.Is<RegistroDiario>(reg =>
            reg.ContaBancariaId == contaPagamento.Id && reg.Data == dataPagamento &&
            reg.Saidas.Single().Valor == 30m && reg.SaldoFinal == 70m)), Times.Once); // 100 - 30
    }

    [Fact]
    public async Task AtualizarAsync_BaixaComValorRealizadoDiferente_UsaOValorRealNoLancamento()
    {
        var clienteId = Guid.NewGuid();
        var contaPagamento = CriarConta(clienteId, saldoInicial: 100m);
        var itemId = Guid.NewGuid();
        var data = new DateOnly(2026, 10, 10);

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada> { new() { Id = itemId, Descricao = "Fornecedor", Valor = 200m, Pago = false, ContaBancariaId = contaPagamento.Id } },
        };
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaPagamento.Id)).ReturnsAsync(contaPagamento);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaPagamento.Id, data)).ReturnsAsync(registro);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaPagamento.Id)).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.AtualizarAsync(clienteId, itemId,
            new AtualizarContaProvisionadaDto { Pago = true, DataPagamento = data, ValorRealizado = 180m }, clienteId, "cliente");

        Assert.Equal(180m, resultado.ValorRealizado);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(reg =>
            reg.Saidas.Any(s => s.Valor == 180m))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AtualizarAsync_BaixaVinculadaALancamentoExistente_NaoCriaNovoLancamentoNemAjustaSaldo()
    {
        var clienteId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var lancamentoExistenteId = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = new DateOnly(2026, 10, 10),
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada> { new() { Id = itemId, Descricao = "Fornecedor", Valor = 200m, Pago = false } },
        };
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.AtualizarAsync(clienteId, itemId,
            new AtualizarContaProvisionadaDto { Pago = true, LancamentoVinculadoId = lancamentoExistenteId }, clienteId, "cliente");

        Assert.Equal(lancamentoExistenteId, resultado.LancamentoVinculadoId);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _contaBancariaRepoMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task AtualizarAsync_EstornoDeLancamentoCriadoPelaBaixa_RemoveOLancamentoEAjustaSaldo()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var itemId = Guid.NewGuid();
        var lancamentoId = Guid.NewGuid();
        var data = new DateOnly(2026, 10, 10);

        var registroOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Id = itemId, Descricao = "Telefone", Valor = 30m, Pago = true, DataBaixa = data,
                    ContaBancariaId = conta.Id, LancamentoVinculadoId = lancamentoId, LancamentoCriadoPelaBaixa = true },
            },
        };
        var registroLancamento = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = data, Inicio = 100m, SaldoFinal = 70m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new List<ItemFinanceiroSaida> { new() { Id = lancamentoId, Descricao = "Telefone", Valor = 30m } },
        };
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroOrigem, registroLancamento });
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, data)).ReturnsAsync(registroLancamento);

        var resultado = await _sut.AtualizarAsync(clienteId, itemId, new AtualizarContaProvisionadaDto { Pago = false }, clienteId, "cliente");

        Assert.False(resultado.Pago);
        Assert.Null(resultado.LancamentoVinculadoId);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(reg =>
            reg.Id == registroLancamento.Id && reg.Saidas.Count == 0 && reg.SaldoFinal == 100m)), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_EstornoDeLancamentoVinculado_NuncaApagaOLancamentoReal()
    {
        var clienteId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var lancamentoId = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = new DateOnly(2026, 10, 10),
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Id = itemId, Descricao = "Telefone", Valor = 30m, Pago = true, LancamentoVinculadoId = lancamentoId, LancamentoCriadoPelaBaixa = false },
            },
        };
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        await _sut.AtualizarAsync(clienteId, itemId, new AtualizarContaProvisionadaDto { Pago = false }, clienteId, "cliente");

        // Nunca busca/toca o registro do lançamento vinculado — ele é dinheiro real, intocável.
        _registroRepoMock.Verify(r => r.ObterPorContaEDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task ExcluirAsync_ItemPago_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = new DateOnly(2026, 10, 10),
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada> { new() { Id = itemId, Descricao = "x", Valor = 10m, Pago = true } },
        };
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirAsync(clienteId, itemId, clienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task ExcluirAsync_ItemPendente_RemoveDoRegistro()
    {
        var clienteId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = new DateOnly(2026, 10, 10),
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada> { new() { Id = itemId, Descricao = "x", Valor = 10m, Pago = false } },
        };
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        await _sut.ExcluirAsync(clienteId, itemId, clienteId, "cliente");

        _registroRepoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(reg => reg.ContasPagar.Count == 0)), Times.Once);
    }
}
