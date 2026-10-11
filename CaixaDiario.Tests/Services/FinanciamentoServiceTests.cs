using CaixaDiario.API.Data;
using CaixaDiario.API.DTOs.Financiamentos;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CaixaDiario.Tests.Services;

public class FinanciamentoServiceTests
{
    // Fake com estado (não mock comportamental) — FinanciamentoService lê/escreve dezenas de
    // RegistroDiario (um por parcela), então simular isso com Setup() por chamada seria inviável;
    // um fake simples sobre uma lista em memória reproduz o repositório real o suficiente.
    private class FakeRegistroRepository : IRegistroRepository
    {
        public readonly List<RegistroDiario> Registros = new();

        public Task<RegistroDiario?> ObterPorIdAsync(Guid id) =>
            Task.FromResult(Registros.FirstOrDefault(r => r.Id == id));

        public Task<RegistroDiario?> ObterPorContaEDataAsync(Guid contaBancariaId, DateOnly data) =>
            Task.FromResult(Registros.FirstOrDefault(r => r.ContaBancariaId == contaBancariaId && r.Data == data));

        public Task<RegistroDiario?> ObterPorClienteEDataAsync(Guid clienteId, DateOnly data) =>
            Task.FromResult(Registros.FirstOrDefault(r => r.ClienteId == clienteId && r.Data == data));

        public Task<RegistroDiario?> ObterPorClienteEDataSemContaAsync(Guid clienteId, DateOnly data) =>
            Task.FromResult(Registros.FirstOrDefault(r => r.ClienteId == clienteId && r.Data == data && r.ContaBancariaId == null));

        public Task<List<RegistroDiario>> ListarPorClienteAsync(Guid clienteId) =>
            Task.FromResult(Registros.Where(r => r.ClienteId == clienteId).ToList());

        public Task<List<RegistroDiario>> ListarPorContaAsync(Guid contaBancariaId) =>
            Task.FromResult(Registros.Where(r => r.ContaBancariaId == contaBancariaId).ToList());

        public Task<RegistroDiario> AdicionarAsync(RegistroDiario registro)
        {
            Registros.Add(registro);
            return Task.FromResult(registro);
        }

        public Task<RegistroDiario> AtualizarAsync(RegistroDiario registro) => Task.FromResult(registro);

        public Task<List<RegistroDiario>> ListarPorPeriodoAsync(Guid clienteId, DateOnly de, DateOnly ate) =>
            Task.FromResult(Registros.Where(r => r.ClienteId == clienteId && r.Data >= de && r.Data <= ate).ToList());

        public Task<int> ContarTodosPorContaAsync(Guid contaBancariaId) =>
            Task.FromResult(Registros.Count(r => r.ContaBancariaId == contaBancariaId));
    }

    private readonly Mock<IFinanciamentoRepository> _financiamentoRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly FakeRegistroRepository _registroRepo = new();
    private readonly Mock<ICategoriaRepository> _categoriaRepoMock = new();
    private readonly Mock<IMetaRepository> _metaRepoMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly FinanciamentoService _sut;
    private readonly List<Financiamento> _financiamentos = new();
    private readonly List<MetaAnual> _metas = new();

    // InMemory não é relacional — o serviço detecta isso (Database.IsRelational()) e não tenta abrir
    // transação, então o teste continua simulando só via mocks dos repositórios (mesmo padrão de
    // TransferenciaServiceTests).
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public FinanciamentoServiceTests()
    {
        _financiamentoRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Financiamento>()))
            .Callback<Financiamento>(_financiamentos.Add).ReturnsAsync((Financiamento f) => f);
        _financiamentoRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Financiamento>())).ReturnsAsync((Financiamento f) => f);
        _financiamentoRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _financiamentos.FirstOrDefault(f => f.Id == id));
        _financiamentoRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid clienteId) => _financiamentos.Where(f => f.ClienteId == clienteId).ToList());
        _financiamentoRepoMock.Setup(r => r.RemoverAsync(It.IsAny<Financiamento>()))
            .Callback<Financiamento>(f => _financiamentos.Remove(f)).Returns(Task.CompletedTask);

        _categoriaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Juros", Tipo = "CustoFixo" },
            new() { Id = Guid.NewGuid(), Nome = "Tarifas bancárias", Tipo = "CustoFixo" },
        });

        _metaRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _metas.FirstOrDefault(m => m.Id == id));
        _metaRepoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        _sut = new FinanciamentoService(
            _financiamentoRepoMock.Object, _contaRepoMock.Object, _registroRepo, _categoriaRepoMock.Object,
            _metaRepoMock.Object, _auditMock.Object, CriarContexto());
    }

    private readonly List<ContaBancaria> _contas = new();

    private ContaBancaria CriarConta(Guid clienteId, decimal saldoInicial = 0m)
    {
        var conta = new ContaBancaria
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Conta Corrente", Tipo = "ContaCorrente",
            SaldoInicial = saldoInicial, Ativa = true, DataCriacao = DateTime.UtcNow,
        };
        _contas.Add(conta);
        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(() => _contas.Where(c => c.ClienteId == clienteId).ToList());
        return conta;
    }

    // ── CriarAsync ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CriarAsync_SemTaxaInformada_ResolveTaxaImplicitaEGeraTabelaPriceDe42Parcelas()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new CriarFinanciamentoDto
        {
            Descricao = "Carro", Tipo = "Financiamento", ValorBem = 30000m, ValorEntrada = 0m,
            NumeroParcelas = 42, ValorParcela = 1000m, Sistema = "Price",
            DataPrimeiraParcela = new DateOnly(2027, 1, 10), ContaBancariaId = conta.Id,
        };

        var resultado = await _sut.CriarAsync(clienteId, clienteId, "cliente", dto);

        Assert.Equal(0.016724m, Math.Round(resultado.TaxaJurosMensal!.Value, 6));
        Assert.Equal(42, resultado.TotalParcelasAtual);
        Assert.Equal(12000m, Math.Round(resultado.JurosTotais, 0));

        var parcelas = await _sut.ListarParcelasAsync(resultado.Id, clienteId, "cliente");
        Assert.Equal(42, parcelas.Count);
        Assert.All(parcelas, p => Assert.Equal(resultado.Id, _registroRepo.Registros
            .SelectMany(r => r.ContasPagar).First(c => c.Id == p.Id).OrigemFinanciamentoId));

        var primeira = parcelas.OrderBy(p => p.Vencimento).First();
        Assert.Equal(501.73m, primeira.Juros);
        Assert.Equal(498.27m, primeira.Amortizacao);
        Assert.Equal(1000m, primeira.Valor);
    }

    [Fact]
    public async Task CriarAsync_SistemaSac_AmortizacaoFixaEJurosDecrescente()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new CriarFinanciamentoDto
        {
            Descricao = "Empréstimo", Tipo = "Emprestimo", ValorBem = 12000m, ValorEntrada = 0m,
            NumeroParcelas = 12, TaxaJurosMensal = 0.01m, Sistema = "SAC",
            DataPrimeiraParcela = new DateOnly(2027, 1, 10), ContaBancariaId = conta.Id,
        };

        var resultado = await _sut.CriarAsync(clienteId, clienteId, "cliente", dto);
        var parcelas = (await _sut.ListarParcelasAsync(resultado.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).ToList();

        Assert.Equal(12, parcelas.Count);
        Assert.Equal(1000m, parcelas[0].Amortizacao);
        Assert.Equal(120m, parcelas[0].Juros);
        Assert.Equal(1120m, parcelas[0].Valor);
        // Amortização fixa, juros decrescente -> parcela decrescente.
        Assert.True(parcelas[1].Valor < parcelas[0].Valor);
        Assert.Equal(0m, parcelas[^1].SaldoDevedor);
    }

    [Fact]
    public async Task CriarAsync_ValorEntradaMaiorOuIgualAoBem_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new CriarFinanciamentoDto
        {
            Descricao = "Carro", ValorBem = 10000m, ValorEntrada = 10000m, NumeroParcelas = 12,
            TaxaJurosMensal = 0.01m, Sistema = "Price", DataPrimeiraParcela = new DateOnly(2027, 1, 10),
            ContaBancariaId = conta.Id,
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(clienteId, clienteId, "cliente", dto));
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task CriarAsync_ComOrigemMetaId_MarcaAMetaComoConquistada()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Status = "EmAndamento", Sonho = "Carro" };
        _metas.Add(meta);

        var dto = new CriarFinanciamentoDto
        {
            Descricao = "Carro", ValorBem = 30000m, ValorEntrada = 0m, NumeroParcelas = 42,
            ValorParcela = 1000m, Sistema = "Price", DataPrimeiraParcela = new DateOnly(2027, 1, 10),
            ContaBancariaId = conta.Id, OrigemMetaId = meta.Id,
        };
        var resultado = await _sut.CriarAsync(clienteId, clienteId, "cliente", dto);

        Assert.Equal("Conquistada", meta.Status);
        Assert.Equal("Financiado", meta.FormaPagamento);
        Assert.Equal(30000m, meta.ValorPago);
        Assert.Equal(resultado.Id, meta.FinanciamentoId);
        Assert.Equal(conta.Id, meta.ContaPagamentoId);
    }

    [Fact]
    public async Task CriarAsync_ContaDeOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid());
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new CriarFinanciamentoDto
        {
            Descricao = "Carro", ValorBem = 10000m, ValorEntrada = 0m, NumeroParcelas = 12,
            TaxaJurosMensal = 0.01m, Sistema = "Price", DataPrimeiraParcela = new DateOnly(2027, 1, 10),
            ContaBancariaId = conta.Id,
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(clienteId, clienteId, "cliente", dto));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    // ── BaixarParcelaAsync ──────────────────────────────────────────────────────────────────────

    private async Task<(Guid ClienteId, ContaBancaria Conta, FinanciamentoDto Financiamento)> CriarFinanciamentoPadraoAsync(
        int numeroParcelas = 42, decimal valorParcela = 1000m, decimal valorBem = 30000m)
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, saldoInicial: 100000m);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new CriarFinanciamentoDto
        {
            Descricao = "Carro", Tipo = "Financiamento", ValorBem = valorBem, ValorEntrada = 0m,
            NumeroParcelas = numeroParcelas, ValorParcela = valorParcela, Sistema = "Price",
            DataPrimeiraParcela = new DateOnly(2027, 1, 10), ContaBancariaId = conta.Id,
        };
        var financiamento = await _sut.CriarAsync(clienteId, clienteId, "cliente", dto);
        return (clienteId, conta, financiamento);
    }

    [Fact]
    public async Task BaixarParcelaAsync_PrimeiraParcelaPagaNoValorCheio_LancaJurosEAmortizacaoSeparados()
    {
        var (clienteId, conta, financiamento) = await CriarFinanciamentoPadraoAsync();
        var parcelas = await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente");
        var primeira = parcelas.OrderBy(p => p.Vencimento).First();

        var dto = new BaixarParcelaFinanciamentoDto { ParcelaId = primeira.Id, DataPagamento = primeira.Vencimento };
        var resultado = await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente", dto);

        Assert.True(resultado.Pago);
        var registroPagamento = _registroRepo.Registros.Single(r => r.ContaBancariaId == conta.Id && r.Data == primeira.Vencimento);
        Assert.Equal(2, registroPagamento.Saidas.Count); // juros + amortização, sem encargos

        var juros = registroPagamento.Saidas.Single(s => s.Categoria == "Juros");
        Assert.Equal(501.73m, juros.Valor);
        Assert.Equal("CustoFixo", juros.TipoCusto); // entra no resultado operacional

        var amortizacao = registroPagamento.Saidas.Single(s => s.Categoria == "Amortização de financiamento");
        Assert.Equal(498.27m, amortizacao.Valor);
        Assert.Equal(LancamentoFiltro.TipoFinanciamento, amortizacao.TipoCusto); // fora do operacional

        // Nenhum lançamento extra — só os dois esperados.
        Assert.Equal(100000m - 1000m, registroPagamento.SaldoFinal);
    }

    [Fact]
    public async Task BaixarParcelaAsync_JaPaga_LancaDadosInvalidos()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync();
        var primeira = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).First();
        var dto = new BaixarParcelaFinanciamentoDto { ParcelaId = primeira.Id, DataPagamento = primeira.Vencimento };
        await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente", dto);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente", dto));
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task BaixarParcelaAsync_TodasAsParcelasPagas_MarcaFinanciamentoComoQuitado()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync(numeroParcelas: 2, valorParcela: 502m, valorBem: 1000m);
        var parcelas = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).ToList();

        foreach (var p in parcelas)
            await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente",
                new BaixarParcelaFinanciamentoDto { ParcelaId = p.Id, DataPagamento = p.Vencimento });

        var atualizado = (await _sut.ListarAsync(clienteId, clienteId, "cliente")).Single(f => f.Id == financiamento.Id);
        Assert.Equal("Quitado", atualizado.Status);
    }

    // ── 41,4% dívida quitada / 50% parcelas pagas após 21 de 42 (item 6.9) ─────────────────────────

    [Fact]
    public async Task ListarAsync_Apos21De42ParcelasPagas_BarraDeDividaQuitadaMostra41Virgula4PorCentoEParcelas50PorCento()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync();
        var parcelas = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).ToList();

        foreach (var p in parcelas.Take(21))
            await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente",
                new BaixarParcelaFinanciamentoDto { ParcelaId = p.Id, DataPagamento = p.Vencimento });

        var atualizado = (await _sut.ListarAsync(clienteId, clienteId, "cliente")).Single(f => f.Id == financiamento.Id);
        Assert.Equal(50m, atualizado.PercentualParcelasPagas);
        Assert.Equal(41.4m, atualizado.PercentualDividaQuitada);
    }

    // ── AdiantarAsync ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdiantarAsync_Simulado_NaoAlteraNadaEDevolveSaldoEParcelasRecalculados()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync();
        var parcelas = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).ToList();
        foreach (var p in parcelas.Take(12))
            await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente",
                new BaixarParcelaFinanciamentoDto { ParcelaId = p.Id, DataPagamento = p.Vencimento });

        var dto = new AdiantarFinanciamentoDto { Valor = 5000m, Data = new DateOnly(2028, 2, 1), Modo = "ReduzirPrazo", Simular = true };
        var resultado = await _sut.AdiantarAsync(financiamento.Id, clienteId, "cliente", dto);

        Assert.Equal(23438.92m, resultado.SaldoAntes);
        Assert.Equal(18438.92m, resultado.SaldoDepois);
        Assert.Equal(30, resultado.ParcelasAntes);
        Assert.Equal(23, resultado.ParcelasDepois);
        Assert.True(resultado.EconomiaJuros > 0);

        // Simulação não persiste nada.
        var naoPagas = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).Count(p => !p.Pago);
        Assert.Equal(30, naoPagas);
        var financiamentoInalterado = (await _sut.ListarAsync(clienteId, clienteId, "cliente")).Single(f => f.Id == financiamento.Id);
        Assert.Equal(0m, financiamentoInalterado.ValorAdiantado);
    }

    [Fact]
    public async Task AdiantarAsync_ReduzirPrazo_AplicaReduzONumeroDeParcelasEAcumulaValorAdiantado()
    {
        var (clienteId, conta, financiamento) = await CriarFinanciamentoPadraoAsync();
        var parcelas = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).ToList();
        foreach (var p in parcelas.Take(12))
            await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente",
                new BaixarParcelaFinanciamentoDto { ParcelaId = p.Id, DataPagamento = p.Vencimento });

        var dataAdiantamento = new DateOnly(2028, 2, 1);
        var dto = new AdiantarFinanciamentoDto { Valor = 5000m, Data = dataAdiantamento, Modo = "ReduzirPrazo", Simular = false };
        var resultado = await _sut.AdiantarAsync(financiamento.Id, clienteId, "cliente", dto);

        var naoPagasDepois = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).Count(p => !p.Pago);
        Assert.Equal(23, naoPagasDepois);

        var financiamentoAtualizado = (await _sut.ListarAsync(clienteId, clienteId, "cliente")).Single(f => f.Id == financiamento.Id);
        Assert.Equal(5000m, financiamentoAtualizado.ValorAdiantado);
        Assert.True(financiamentoAtualizado.EconomiaJurosAcumulada > 0);

        // O adiantamento em si saiu da conta como amortização extraordinária.
        var registroAdiantamento = _registroRepo.Registros.Single(r => r.ContaBancariaId == conta.Id && r.Data == dataAdiantamento);
        Assert.Contains(registroAdiantamento.Saidas, s => s.Valor == 5000m && s.TipoCusto == LancamentoFiltro.TipoFinanciamento);
    }

    [Fact]
    public async Task AdiantarAsync_ValorMaiorOuIgualAoSaldo_LancaDadosInvalidos()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync();

        var dto = new AdiantarFinanciamentoDto { Valor = 30000m, Data = new DateOnly(2027, 2, 1), Modo = "ReduzirPrazo" };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AdiantarAsync(financiamento.Id, clienteId, "cliente", dto));
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    // ── ExcluirAsync ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExcluirAsync_SemParcelaPaga_RemoveTodasAsParcelas()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync(numeroParcelas: 3, valorBem: 2500m);

        await _sut.ExcluirAsync(financiamento.Id, clienteId, "cliente");

        var restantes = _registroRepo.Registros.SelectMany(r => r.ContasPagar).Count(c => c.OrigemFinanciamentoId == financiamento.Id);
        Assert.Equal(0, restantes);
    }

    // Item A1 do bundle D6JWEAJa: excluir removia as parcelas mas deixava o contrato "fantasma"
    // (Ativo, 0/0, 100% quitado) — ExcluirAsync agora também chama RemoverAsync no repositório.
    [Fact]
    public async Task ExcluirAsync_SemParcelaPaga_RemoveOProprioContratoDaListagem()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync(numeroParcelas: 3, valorBem: 2500m);

        await _sut.ExcluirAsync(financiamento.Id, clienteId, "cliente");

        var listagem = await _sut.ListarAsync(clienteId, clienteId, "cliente");
        Assert.DoesNotContain(listagem, f => f.Id == financiamento.Id);
        _financiamentoRepoMock.Verify(r => r.RemoverAsync(It.Is<Financiamento>(f => f.Id == financiamento.Id)), Times.Once);
    }

    [Fact]
    public async Task ExcluirAsync_ComParcelaPaga_LancaDadosInvalidos()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync(numeroParcelas: 3, valorBem: 2500m);
        var primeira = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).First();
        await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente",
            new BaixarParcelaFinanciamentoDto { ParcelaId = primeira.Id, DataPagamento = primeira.Vencimento });

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirAsync(financiamento.Id, clienteId, "cliente"));
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    // ── EstornarParcelaAsync ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EstornarParcelaAsync_RemoveOsLancamentosEVoltaOSaldo()
    {
        var (clienteId, conta, financiamento) = await CriarFinanciamentoPadraoAsync();
        var primeira = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).First();
        await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente",
            new BaixarParcelaFinanciamentoDto { ParcelaId = primeira.Id, DataPagamento = primeira.Vencimento });

        await _sut.EstornarParcelaAsync(financiamento.Id, primeira.Id, clienteId, "cliente");

        var registroPagamento = _registroRepo.Registros.Single(r => r.ContaBancariaId == conta.Id && r.Data == primeira.Vencimento);
        Assert.Empty(registroPagamento.Saidas);
        Assert.Equal(100000m, registroPagamento.SaldoFinal);

        var parcelaRevertida = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).Single(p => p.Id == primeira.Id);
        Assert.False(parcelaRevertida.Pago);
    }

    // ── Casos extras: encargos embutidos, atraso/multa e modo ReduzirParcela ──────────────────────

    [Fact]
    public async Task CriarAsync_ValorParcelaMaiorQueOCalculado_DiferencaVIraEncargos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, saldoInicial: 100000m);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        // Taxa explícita 1% a.m., 12x sobre 10000 -> parcela "pura" calculada = 888,49; o contrato
        // cobra 900 (seguro/tarifa embutidos) -> os 11,51 de diferença, iguais em toda parcela, são
        // Encargos (nunca juros nem amortização).
        var dto = new CriarFinanciamentoDto
        {
            Descricao = "Moto", ValorBem = 10000m, ValorEntrada = 0m, NumeroParcelas = 12,
            ValorParcela = 900m, TaxaJurosMensal = 0.01m, Sistema = "Price",
            DataPrimeiraParcela = new DateOnly(2027, 1, 10), ContaBancariaId = conta.Id,
        };
        var resultado = await _sut.CriarAsync(clienteId, clienteId, "cliente", dto);
        var primeira = (await _sut.ListarParcelasAsync(resultado.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).First();

        Assert.True(primeira.Encargos > 0);
        Assert.Equal(900m, primeira.Valor);
        Assert.Equal(primeira.Juros + primeira.Amortizacao + primeira.Encargos, primeira.Valor);

        var baixa = await _sut.BaixarParcelaAsync(resultado.Id, clienteId, "cliente",
            new BaixarParcelaFinanciamentoDto { ParcelaId = primeira.Id, DataPagamento = primeira.Vencimento });
        var registroPagamento = _registroRepo.Registros.Single(r => r.ContaBancariaId == conta.Id && r.Data == primeira.Vencimento);
        Assert.Equal(3, registroPagamento.Saidas.Count); // juros + amortização + encargos
        Assert.Contains(registroPagamento.Saidas, s => s.Categoria == "Tarifas bancárias");
    }

    [Fact]
    public async Task BaixarParcelaAsync_ValorPagoMaiorQueAParcela_DiferencaEntraComoJurosDeAtraso()
    {
        var (clienteId, conta, financiamento) = await CriarFinanciamentoPadraoAsync();
        var primeira = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).First();

        await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente",
            new BaixarParcelaFinanciamentoDto { ParcelaId = primeira.Id, DataPagamento = primeira.Vencimento, ValorPago = 1050m });

        var registroPagamento = _registroRepo.Registros.Single(r => r.ContaBancariaId == conta.Id && r.Data == primeira.Vencimento);
        var juros = registroPagamento.Saidas.Single(s => s.Categoria == "Juros");
        Assert.Equal(501.73m + 50m, juros.Valor); // juros de tabela + os 50 de atraso/multa
        Assert.Equal(100000m - 1050m, registroPagamento.SaldoFinal);
    }

    [Fact]
    public async Task BaixarParcelaAsync_ValorPagoMenorQueAParcela_LancaDadosInvalidos()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync();
        var primeira = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).First();

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente",
            new BaixarParcelaFinanciamentoDto { ParcelaId = primeira.Id, DataPagamento = primeira.Vencimento, ValorPago = 500m }));
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task AdiantarAsync_ReduzirParcela_MantemONumeroDeParcelasEReduzOValorDeCadaUma()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync();
        var parcelas = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).ToList();
        foreach (var p in parcelas.Take(12))
            await _sut.BaixarParcelaAsync(financiamento.Id, clienteId, "cliente",
                new BaixarParcelaFinanciamentoDto { ParcelaId = p.Id, DataPagamento = p.Vencimento });

        var dto = new AdiantarFinanciamentoDto { Valor = 5000m, Data = new DateOnly(2028, 2, 1), Modo = "ReduzirParcela", Simular = false };
        var resultado = await _sut.AdiantarAsync(financiamento.Id, clienteId, "cliente", dto);

        Assert.Equal(30, resultado.ParcelasDepois); // mesmo número — só o valor de cada parcela cai
        var naoPagasDepois = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).Where(p => !p.Pago).ToList();
        Assert.Equal(30, naoPagasDepois.Count);
        Assert.All(naoPagasDepois, p => Assert.True(p.Valor < 1000m));
    }

    [Fact]
    public async Task EstornarParcelaAsync_DeParcelaNaoPaga_LancaDadosInvalidos()
    {
        var (clienteId, _, financiamento) = await CriarFinanciamentoPadraoAsync();
        var primeira = (await _sut.ListarParcelasAsync(financiamento.Id, clienteId, "cliente")).OrderBy(p => p.Vencimento).First();

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.EstornarParcelaAsync(financiamento.Id, primeira.Id, clienteId, "cliente"));
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }
}
