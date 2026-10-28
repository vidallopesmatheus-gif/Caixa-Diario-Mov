using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class CorrigirDataParcelasServiceTests
{
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IFaturaRepository> _faturaRepoMock = new();
    private readonly CorrigirDataParcelasService _sut;

    public CorrigirDataParcelasServiceTests()
    {
        _sut = new CorrigirDataParcelasService(_contaRepoMock.Object, _registroRepoMock.Object, _faturaRepoMock.Object);
    }

    private static ContaBancaria CriarCartao(decimal saldoInicial = 0m) => new()
    {
        Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Nome = "Cartões", Tipo = "CartaoCredito",
        SaldoInicial = saldoInicial, Ativa = true, DataCriacao = DateTime.UtcNow,
    };

    [Fact]
    public async Task CorrigirAsync_SemConfirmar_SoRelatorioNaoGravaNada()
    {
        var conta = CriarCartao();
        var faturaId = Guid.NewGuid();
        var fatura = new Fatura
        {
            Id = faturaId, ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = "C6",
            Competencia = "2026-10", DataFechamento = new DateOnly(2026, 10, 13), DataVencimento = new DateOnly(2026, 10, 20),
            CriadoEm = DateTime.UtcNow,
        };
        var itemId = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 4, 1),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = -100m,
            Saidas = new() { new ItemFinanceiroSaida { Id = itemId, Descricao = "MercadoLivre - parcela 7/7", Valor = 100m, FaturaId = faturaId } },
        };
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { registro });
        _faturaRepoMock.Setup(r => r.ObterPorIdAsync(faturaId)).ReturnsAsync(fatura);

        var resultado = await _sut.CorrigirAsync(confirmar: false);

        Assert.False(resultado.Confirmado);
        Assert.Equal(1, resultado.ItensEncontrados);
        var item = Assert.Single(resultado.Itens);
        Assert.Equal(new DateOnly(2026, 4, 1), item.DataAntiga);
        Assert.Equal(new DateOnly(2026, 10, 13), item.DataNova);
        Assert.Equal(0, resultado.ContasRecalculadas);

        // Nada foi gravado.
        Assert.Single(registro.Saidas);
        Assert.Equal(new DateOnly(2026, 4, 1), registro.Data);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task CorrigirAsync_ComConfirmar_MoveOItemParaODiaDoFechamentoEDevolveOSaldo()
    {
        var conta = CriarCartao(saldoInicial: 0m);
        var faturaId = Guid.NewGuid();
        var fatura = new Fatura
        {
            Id = faturaId, ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = "C6",
            Competencia = "2026-10", DataFechamento = new DateOnly(2026, 10, 13), DataVencimento = new DateOnly(2026, 10, 20),
            CriadoEm = DateTime.UtcNow,
        };
        var itemId = Guid.NewGuid();
        var registroAntigo = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 4, 1),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), Inicio = 0m, SaldoFinal = -100m,
            Saidas = new() { new ItemFinanceiroSaida { Id = itemId, Descricao = "MercadoLivre - parcela 7/7", Valor = 100m, FaturaId = faturaId } },
        };
        var registrosDaConta = new List<RegistroDiario> { registroAntigo };
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(() => registrosDaConta.ToList());
        _faturaRepoMock.Setup(r => r.ObterPorIdAsync(faturaId)).ReturnsAsync(fatura);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, It.IsAny<DateOnly>())).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        RegistroDiario? registroNovoCriado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => { registroNovoCriado = r; registrosDaConta.Add(r); })
            .ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.CorrigirAsync(confirmar: true);

        Assert.True(resultado.Confirmado);
        Assert.Equal(1, resultado.ContasRecalculadas);

        Assert.Empty(registroAntigo.Saidas);
        Assert.Equal(0m, registroAntigo.SaldoFinal); // -100 + 100 devolvido

        Assert.NotNull(registroNovoCriado);
        Assert.Equal(new DateOnly(2026, 10, 13), registroNovoCriado!.Data);
        var itemMovido = Assert.Single(registroNovoCriado.Saidas);
        Assert.Equal(itemId, itemMovido.Id);
        Assert.Equal(new DateOnly(2026, 4, 1), itemMovido.DataCompraOriginal);
        Assert.Equal(-100m, registroNovoCriado.SaldoFinal); // 0 (saldoInicial) - 100
    }

    [Fact]
    public async Task CorrigirAsync_PrimeiraParcela_NuncaConta()
    {
        var conta = CriarCartao();
        var faturaId = Guid.NewGuid();
        var fatura = new Fatura
        {
            Id = faturaId, ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = "Nubank",
            Competencia = "2026-08", DataVencimento = new DateOnly(2026, 8, 10), CriadoEm = DateTime.UtcNow,
        };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 7, 26),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = -100m,
            Saidas = new() { new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Trocafone - Parcela 1/10", Valor = 100m, FaturaId = faturaId } },
        };
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { registro });
        _faturaRepoMock.Setup(r => r.ObterPorIdAsync(faturaId)).ReturnsAsync(fatura);

        var resultado = await _sut.CorrigirAsync(confirmar: false);

        Assert.Equal(0, resultado.ItensEncontrados);
    }

    [Fact]
    public async Task CorrigirAsync_DataJaCorreta_NuncaConta()
    {
        var conta = CriarCartao();
        var faturaId = Guid.NewGuid();
        var fatura = new Fatura
        {
            Id = faturaId, ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = "Nubank",
            Competencia = "2026-09", DataFechamento = new DateOnly(2026, 8, 30), DataVencimento = new DateOnly(2026, 9, 10),
            CriadoEm = DateTime.UtcNow,
        };
        var registro = new RegistroDiario
        {
            // Já está no dia do fechamento — nada a corrigir.
            Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 8, 30),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = -199.16m,
            Saidas = new() { new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Trocafone - Parcela 2/10", Valor = 199.16m, FaturaId = faturaId } },
        };
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { registro });
        _faturaRepoMock.Setup(r => r.ObterPorIdAsync(faturaId)).ReturnsAsync(fatura);

        var resultado = await _sut.CorrigirAsync(confirmar: false);

        Assert.Equal(0, resultado.ItensEncontrados);
    }
}
