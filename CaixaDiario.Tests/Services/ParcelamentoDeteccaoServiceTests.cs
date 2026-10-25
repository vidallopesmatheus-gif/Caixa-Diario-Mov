using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ParcelamentoDeteccaoServiceTests
{
    private readonly Mock<IParcelamentoRepository> _parcelamentoRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly ParcelamentoDeteccaoService _sut;

    public ParcelamentoDeteccaoServiceTests()
    {
        _sut = new ParcelamentoDeteccaoService(_parcelamentoRepoMock.Object, _registroRepoMock.Object);
    }

    private static ContaBancaria CriarCartao(Guid clienteId) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Nubank", Tipo = "CartaoCredito",
        SaldoInicial = 0m, Ativa = true, DataCriacao = DateTime.UtcNow,
    };

    [Fact]
    public async Task ProcessarParcelaDetectadaAsync_PrimeiraParcela_CriaParcelamentoEGeraAsRestantesComoPrevistas()
    {
        var clienteId = Guid.NewGuid();
        var cartao = CriarCartao(clienteId);
        var dataFatura = new DateOnly(2026, 10, 20);

        _parcelamentoRepoMock.Setup(r => r.ListarAtivosPorContaAsync(cartao.Id)).ReturnsAsync(new List<Parcelamento>());
        Parcelamento? criado = null;
        _parcelamentoRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Parcelamento>()))
            .Callback<Parcelamento>(p => criado = p).ReturnsAsync((Parcelamento p) => p);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(cartao.Id)).ReturnsAsync(new List<RegistroDiario>());

        var registrosCriados = new List<RegistroDiario>();
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(cartao.Id, It.IsAny<DateOnly>())).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(registrosCriados.Add).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.ProcessarParcelaDetectadaAsync(cartao, dataFatura, "Trocafone", "Equipamentos", 1, 10, 199.16m);

        Assert.NotNull(criado);
        Assert.Equal("Trocafone", criado!.DescricaoBase);
        Assert.Equal("Equipamentos", criado.Categoria);
        Assert.Equal(199.16m, criado.ValorParcela);
        Assert.Equal(10, criado.TotalParcelas);
        Assert.Equal(dataFatura, criado.DataPrimeira); // numero=1, então a primeira é a própria data
        Assert.Equal(1991.60m, criado.ValorTotal);
        Assert.Equal("Ativo", criado.Status);
        Assert.Equal(resultado.Id, criado.Id);

        // Parcelas 2..10 — 9 previstas geradas.
        Assert.Equal(9, registrosCriados.Count);
        var numeros = registrosCriados.SelectMany(r => r.ContasPagar).Select(c => c.NumeroParcela!.Value).OrderBy(n => n).ToList();
        Assert.Equal(Enumerable.Range(2, 9).ToList(), numeros);

        var parcela2 = registrosCriados.Single(r => r.Data == dataFatura.AddMonths(1));
        var itemParcela2 = Assert.Single(parcela2.ContasPagar);
        Assert.Equal(criado.Id, itemParcela2.ParcelamentoId);
        Assert.Equal(2, itemParcela2.NumeroParcela);
        Assert.Equal(199.16m, itemParcela2.Valor);
        Assert.Equal(dataFatura.AddMonths(1), itemParcela2.DataVencimento);
        Assert.Equal("Equipamentos", itemParcela2.Categoria);
        Assert.False(itemParcela2.Pago);
    }

    [Fact]
    public async Task ProcessarParcelaDetectadaAsync_DetectadoAPartirDaParcela4_InfereDataPrimeiraEGeraSoAPartirDe5()
    {
        var clienteId = Guid.NewGuid();
        var cartao = CriarCartao(clienteId);
        var dataFatura = new DateOnly(2026, 10, 20);

        _parcelamentoRepoMock.Setup(r => r.ListarAtivosPorContaAsync(cartao.Id)).ReturnsAsync(new List<Parcelamento>());
        Parcelamento? criado = null;
        _parcelamentoRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Parcelamento>()))
            .Callback<Parcelamento>(p => criado = p).ReturnsAsync((Parcelamento p) => p);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(cartao.Id)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(cartao.Id, It.IsAny<DateOnly>())).ReturnsAsync((RegistroDiario?)null);
        var registrosCriados = new List<RegistroDiario>();
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(registrosCriados.Add).ReturnsAsync((RegistroDiario r) => r);

        await _sut.ProcessarParcelaDetectadaAsync(cartao, dataFatura, "Airbnb", null, 4, 10, 50m);

        // Parcela 1 teria vencido 3 meses antes da 4ª.
        Assert.Equal(dataFatura.AddMonths(-3), criado!.DataPrimeira);

        // Só 5..10 viram previstas — nada é inventado pras parcelas 1..3, que já aconteceram antes
        // de existir a detecção (não há dado real sobre elas).
        var numeros = registrosCriados.SelectMany(r => r.ContasPagar).Select(c => c.NumeroParcela!.Value).OrderBy(n => n).ToList();
        Assert.Equal(Enumerable.Range(5, 6).ToList(), numeros);
    }

    [Fact]
    public async Task ProcessarParcelaDetectadaAsync_SegundaFaturaComParcela2_ReaproveitaOParcelamentoERemoveAPrevista()
    {
        var clienteId = Guid.NewGuid();
        var cartao = CriarCartao(clienteId);
        var parcelamentoId = Guid.NewGuid();
        var existente = new Parcelamento
        {
            Id = parcelamentoId, ClienteId = clienteId, ContaCartaoId = cartao.Id, DescricaoBase = "Trocafone",
            Categoria = "Equipamentos", ValorParcela = 199.16m, TotalParcelas = 10,
            DataPrimeira = new DateOnly(2026, 9, 20), ValorTotal = 1991.60m, Status = "Ativo", CriadoEm = DateTime.UtcNow,
        };
        _parcelamentoRepoMock.Setup(r => r.ListarAtivosPorContaAsync(cartao.Id)).ReturnsAsync(new List<Parcelamento> { existente });

        // A prevista da parcela 2, gerada quando a parcela 1 foi importada.
        var previstaId = Guid.NewGuid();
        var registroComPrevista = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = cartao.Id, Data = new DateOnly(2026, 11, 20),
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new() { new ContaProvisionada { Id = previstaId, Descricao = "Trocafone - Parcela 2/10", Valor = 199.16m, ParcelamentoId = parcelamentoId, NumeroParcela = 2 } },
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(cartao.Id)).ReturnsAsync(new List<RegistroDiario> { registroComPrevista });
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(cartao.Id, It.IsAny<DateOnly>())).ReturnsAsync((RegistroDiario?)null);
        RegistroDiario? atualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizado = r).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.ProcessarParcelaDetectadaAsync(cartao, new DateOnly(2026, 11, 20), "Trocafone", "Equipamentos", 2, 10, 199.16m);

        // Reaproveitou o parcelamento existente — não criou um segundo.
        Assert.Equal(parcelamentoId, resultado.Id);
        _parcelamentoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<Parcelamento>()), Times.Never);

        // A prevista da parcela 2 foi removida (virou real em ItemFinanceiroSaida, fora daqui).
        Assert.NotNull(atualizado);
        Assert.Empty(atualizado!.ContasPagar);
    }

    [Fact]
    public async Task ProcessarParcelaDetectadaAsync_ValorDentroDaToleranciaDeUmPorCento_ConsideraMesmoParcelamento()
    {
        var clienteId = Guid.NewGuid();
        var cartao = CriarCartao(clienteId);
        var existente = new Parcelamento
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaCartaoId = cartao.Id, DescricaoBase = "Trocafone",
            ValorParcela = 199.16m, TotalParcelas = 10, DataPrimeira = new DateOnly(2026, 9, 20),
            ValorTotal = 1991.60m, Status = "Ativo", CriadoEm = DateTime.UtcNow,
        };
        _parcelamentoRepoMock.Setup(r => r.ListarAtivosPorContaAsync(cartao.Id)).ReturnsAsync(new List<Parcelamento> { existente });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(cartao.Id)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(cartao.Id, It.IsAny<DateOnly>())).ReturnsAsync((RegistroDiario?)null);

        // 199.50 é ~0,17% maior que 199.16 — dentro da tolerância de 1%.
        var resultado = await _sut.ProcessarParcelaDetectadaAsync(cartao, new DateOnly(2026, 11, 20), "Trocafone", null, 2, 10, 199.50m);

        Assert.Equal(existente.Id, resultado.Id);
        _parcelamentoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<Parcelamento>()), Times.Never);
    }

    [Fact]
    public async Task ProcessarParcelaDetectadaAsync_ComercianteDiferente_CriaUmParcelamentoSeparado()
    {
        var clienteId = Guid.NewGuid();
        var cartao = CriarCartao(clienteId);
        var existente = new Parcelamento
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaCartaoId = cartao.Id, DescricaoBase = "Trocafone",
            ValorParcela = 199.16m, TotalParcelas = 10, DataPrimeira = new DateOnly(2026, 9, 20),
            ValorTotal = 1991.60m, Status = "Ativo", CriadoEm = DateTime.UtcNow,
        };
        _parcelamentoRepoMock.Setup(r => r.ListarAtivosPorContaAsync(cartao.Id)).ReturnsAsync(new List<Parcelamento> { existente });
        _parcelamentoRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Parcelamento>())).ReturnsAsync((Parcelamento p) => p);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(cartao.Id)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(cartao.Id, It.IsAny<DateOnly>())).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.ProcessarParcelaDetectadaAsync(cartao, new DateOnly(2026, 11, 20), "Airbnb", null, 1, 6, 199.16m);

        Assert.NotEqual(existente.Id, resultado.Id);
        _parcelamentoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<Parcelamento>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarParcelaDetectadaAsync_UltimaParcela_MarcaParcelamentoComoQuitado()
    {
        var clienteId = Guid.NewGuid();
        var cartao = CriarCartao(clienteId);
        var existente = new Parcelamento
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaCartaoId = cartao.Id, DescricaoBase = "Trocafone",
            ValorParcela = 199.16m, TotalParcelas = 10, DataPrimeira = new DateOnly(2026, 1, 20),
            ValorTotal = 1991.60m, Status = "Ativo", CriadoEm = DateTime.UtcNow,
        };
        _parcelamentoRepoMock.Setup(r => r.ListarAtivosPorContaAsync(cartao.Id)).ReturnsAsync(new List<Parcelamento> { existente });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(cartao.Id)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(cartao.Id, It.IsAny<DateOnly>())).ReturnsAsync((RegistroDiario?)null);
        Parcelamento? atualizado = null;
        _parcelamentoRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Parcelamento>()))
            .Callback<Parcelamento>(p => atualizado = p).ReturnsAsync((Parcelamento p) => p);

        await _sut.ProcessarParcelaDetectadaAsync(cartao, new DateOnly(2026, 10, 20), "Trocafone", null, 10, 10, 199.16m);

        Assert.NotNull(atualizado);
        Assert.Equal("Quitado", atualizado!.Status);
    }
}
