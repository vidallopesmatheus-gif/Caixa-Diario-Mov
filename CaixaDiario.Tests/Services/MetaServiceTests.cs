using CaixaDiario.API.DTOs.FaturasCartao;
using CaixaDiario.API.DTOs.Metas;
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class MetaServiceTests
{
    private readonly Mock<IMetaRepository> _repoMock = new();
    private readonly Mock<IMetaProgressoService> _metaProgressoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaBancariaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<ICategoriaRepository> _categoriaRepoMock = new();
    private readonly Mock<IContaProvisionadaService> _contaProvisionadaServiceMock = new();
    private readonly Mock<IFaturaCartaoService> _faturaCartaoServiceMock = new();
    private readonly Mock<ITransferenciaService> _transferenciaServiceMock = new();
    private readonly Mock<IFinanciamentoRepository> _financiamentoRepoMock = new();
    private readonly Mock<IParcelamentoRepository> _parcelamentoRepoMock = new();
    private readonly Mock<IParcelamentoDeteccaoService> _parcelamentoDeteccaoServiceMock = new();
    private readonly Mock<IFaturaRepository> _faturaRepoMock = new();
    private static readonly DateOnly Hoje = new(2026, 11, 8);
    private readonly MetaService _sut;

    public MetaServiceTests()
    {
        _categoriaRepoMock.Setup(r => r.ListarTodasPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Categoria>());
        // Vínculos do andamento (CarregarVinculosAsync) — vazios por padrão, cada teste sobrescreve.
        _parcelamentoRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(() => new List<Parcelamento>());
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(() => new List<RegistroDiario>());
        _contaBancariaRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(() => new List<ContaBancaria>());
        _faturaRepoMock.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(() => new List<Fatura>());
        _sut = new MetaService(
            _repoMock.Object, _metaProgressoMock.Object, _contaBancariaRepoMock.Object,
            _registroRepoMock.Object, _categoriaRepoMock.Object, _contaProvisionadaServiceMock.Object,
            _faturaCartaoServiceMock.Object, _transferenciaServiceMock.Object, _financiamentoRepoMock.Object,
            _parcelamentoRepoMock.Object, _parcelamentoDeteccaoServiceMock.Object, _faturaRepoMock.Object, new RelogioFixo(Hoje));
    }

    [Fact]
    public async Task Obter_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterMetaAsync(Guid.NewGuid(), 2026, Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    [Fact]
    public async Task Obter_MetaNaoEncontrada_LancaMetaNaoEncontrada()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterMetaSimplesPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync((MetaAnual?)null);
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterMetaAsync(clienteId, 2026, clienteId, "cliente"));
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.META_NAO_ENCONTRADA, ex.Codigo);
    }

    [Fact]
    public async Task Obter_Admin_RetornaMeta()
    {
        var clienteId = Guid.NewGuid();
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, MetaReceita = 120000m, MetaLucro = 60000m, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow };
        _repoMock.Setup(r => r.ObterMetaSimplesPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync(meta);
        var resultado = await _sut.ObterMetaAsync(clienteId, 2026, Guid.NewGuid(), "admin");
        Assert.Equal(120000m, resultado.MetaReceita);
    }

    [Fact]
    public async Task ListarMetas_ClienteProprio_RetornaOrdenadoPorAnoDecrescente()
    {
        var clienteId = Guid.NewGuid();
        var metas = new List<MetaAnual>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2025, MetaReceita = 1m, MetaLucro = 1m, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2027, MetaReceita = 1m, MetaLucro = 1m, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow },
        };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(metas);

        var resultado = await _sut.ListarMetasAsync(clienteId, clienteId, "cliente");

        Assert.Equal(2027, resultado[0].Ano);
        Assert.Equal(2025, resultado[1].Ano);
    }

    // Item 5 (Money Vision): meta conquistada via "Financiado" expõe o status do contrato — base
    // pro front mostrar "Quitar <bem>" em vez de só "Conquistada" enquanto não fica "Quitado".
    [Fact]
    public async Task ListarMetas_MetaConquistadaComFinanciamentoAberto_ExpoeFinanciamentoStatusAtivo()
    {
        var clienteId = Guid.NewGuid();
        var financiamentoId = Guid.NewGuid();
        var meta = new MetaAnual
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo", Status = "Conquistada",
            FormaPagamento = "Financiado", FinanciamentoId = financiamentoId, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow,
        };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual> { meta });
        _financiamentoRepoMock.Setup(r => r.ObterPorIdAsync(financiamentoId))
            .ReturnsAsync(new Financiamento { Id = financiamentoId, ClienteId = clienteId, Status = "Ativo" });

        var resultado = await _sut.ListarMetasAsync(clienteId, clienteId, "cliente");

        Assert.Equal("Ativo", Assert.Single(resultado).FinanciamentoStatus);
    }

    [Fact]
    public async Task ListarMetas_SemFinanciamentoVinculado_FinanciamentoStatusNulo()
    {
        var clienteId = Guid.NewGuid();
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo", CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual> { meta });

        var resultado = await _sut.ListarMetasAsync(clienteId, clienteId, "cliente");

        Assert.Null(Assert.Single(resultado).FinanciamentoStatus);
        _financiamentoRepoMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ListarMetas_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ListarMetasAsync(Guid.NewGuid(), Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Salvar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var dto = new SalvarMetaAnualDto { ClienteId = Guid.NewGuid(), Ano = 2026, MetaReceita = 100000m, MetaLucro = 50000m };
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.SalvarMetaAsync(dto, Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Salvar_Nova_RetornaDto()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto { ClienteId = clienteId, Ano = 2026, MetaReceita = 120000m, MetaLucro = 60000m };
        _repoMock.Setup(r => r.ObterMetaSimplesPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync((MetaAnual?)null);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        var resultado = await _sut.SalvarMetaAsync(dto, clienteId, "cliente");
        Assert.Equal(120000m, resultado.MetaReceita);
        Assert.Equal(2026, resultado.Ano);
    }

    [Fact]
    public async Task Salvar_Existente_AtualizaERetorna()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto { ClienteId = clienteId, Ano = 2026, MetaReceita = 200000m, MetaLucro = 100000m };
        var existente = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, MetaReceita = 100000m, MetaLucro = 50000m, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow };
        _repoMock.Setup(r => r.ObterMetaSimplesPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        var resultado = await _sut.SalvarMetaAsync(dto, clienteId, "cliente");
        Assert.Equal(200000m, resultado.MetaReceita);
    }

    // ── Objetivos (modo "metodo") — vários por cliente, sem identidade por ano ──────────────

    [Fact]
    public async Task Salvar_ObjetivoSemDataAlvo_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto { ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo", ValorSonho = 10000m };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarMetaAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Salvar_ObjetivoNovoSemId_CriaSemChecarUnicidadePorAno()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto
        {
            ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo",
            Sonho = "Macbook", ValorSonho = 15000m, DataAlvo = new DateOnly(2026, 12, 1),
        };
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var resultado = await _sut.SalvarMetaAsync(dto, clienteId, "cliente");

        Assert.Equal("Macbook", resultado.Sonho);
        Assert.Equal(new DateOnly(2026, 12, 1), resultado.DataAlvo);
        _repoMock.Verify(r => r.ObterMetaSimplesPorClienteEAnoAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        _repoMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Salvar_ObjetivoComId_AtualizaOMesmoRegistro()
    {
        var clienteId = Guid.NewGuid();
        var objetivoId = Guid.NewGuid();
        var existente = new MetaAnual
        {
            Id = objetivoId, ClienteId = clienteId, ModoMeta = "metodo", Ano = 2026,
            Sonho = "Macbook", ValorSonho = 15000m, DataAlvo = new DateOnly(2026, 12, 1),
            CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow,
        };
        var dto = new SalvarMetaAnualDto
        {
            Id = objetivoId, ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo",
            Sonho = "Macbook Pro", ValorSonho = 18000m, DataAlvo = new DateOnly(2027, 3, 1),
        };
        _repoMock.Setup(r => r.ObterPorIdAsync(objetivoId)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var resultado = await _sut.SalvarMetaAsync(dto, clienteId, "cliente");

        Assert.Equal("Macbook Pro", resultado.Sonho);
        Assert.Equal(18000m, resultado.ValorSonho);
        Assert.Equal(new DateOnly(2027, 3, 1), resultado.DataAlvo);
    }

    [Fact]
    public async Task Salvar_ObjetivoComIdInexistente_LancaNaoEncontrada()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo",
            ValorSonho = 1000m, DataAlvo = new DateOnly(2026, 12, 1),
        };
        _repoMock.Setup(r => r.ObterPorIdAsync(dto.Id!.Value)).ReturnsAsync((MetaAnual?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarMetaAsync(dto, clienteId, "cliente"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Salvar_ObjetivoComIdDeOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var objetivoId = Guid.NewGuid();
        var deOutroCliente = new MetaAnual { Id = objetivoId, ClienteId = Guid.NewGuid(), ModoMeta = "metodo" };
        var dto = new SalvarMetaAnualDto
        {
            Id = objetivoId, ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo",
            ValorSonho = 1000m, DataAlvo = new DateOnly(2026, 12, 1),
        };
        _repoMock.Setup(r => r.ObterPorIdAsync(objetivoId)).ReturnsAsync(deOutroCliente);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarMetaAsync(dto, clienteId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Excluir_MetaExistenteDoProprioCliente_Remove()
    {
        var clienteId = Guid.NewGuid();
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, ModoMeta = "metodo" };
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);

        await _sut.ExcluirMetaAsync(meta.Id, clienteId, "cliente");

        _repoMock.Verify(r => r.RemoverAsync(meta), Times.Once);
    }

    [Fact]
    public async Task Excluir_MetaDeOutroCliente_LancaAcessoNegado()
    {
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "metodo" };
        var repoMock = new Mock<IMetaRepository>();
        repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        var sut = new MetaService(
            repoMock.Object, _metaProgressoMock.Object, _contaBancariaRepoMock.Object,
            _registroRepoMock.Object, _categoriaRepoMock.Object, _contaProvisionadaServiceMock.Object,
            _faturaCartaoServiceMock.Object, _transferenciaServiceMock.Object, _financiamentoRepoMock.Object,
            _parcelamentoRepoMock.Object, _parcelamentoDeteccaoServiceMock.Object, _faturaRepoMock.Object);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ExcluirMetaAsync(meta.Id, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
        repoMock.Verify(r => r.RemoverAsync(It.IsAny<MetaAnual>()), Times.Never);
    }

    [Fact]
    public async Task Excluir_MetaInexistente_LancaNaoEncontrada()
    {
        _repoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((MetaAnual?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirMetaAsync(Guid.NewGuid(), Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
    }

    // ── ConquistarAsync (Fase 5 — "🏆 Conquistei") ──────────────────────────────────────────────

    private static MetaAnual CriarMetaEmAndamento(Guid clienteId, string sonho = "Macbook") => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, ModoMeta = "metodo", Sonho = sonho,
        ValorSonho = 4000m, Status = "EmAndamento",
    };

    [Fact]
    public async Task Conquistar_AVista_CriaUmaSaidaNaContaEMarcaMetaComoConquistada()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var contaId = Guid.NewGuid();
        var conta = new ContaBancaria { Id = contaId, ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true };
        var data = new DateOnly(2026, 11, 10);

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());
        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = data, FormaPagamento = "AVista",
            ContaPagamentoId = contaId, Categoria = "Equipamentos",
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        var saida = Assert.Single(criado!.Saidas);
        Assert.Equal("🏆 Macbook", saida.Descricao);
        Assert.Equal(4000m, saida.Valor);
        Assert.Equal("Equipamentos", saida.Categoria);
        Assert.Equal(meta.Id, saida.OrigemMetaId);
        Assert.Equal(-4000m, criado.SaldoFinal);

        Assert.Equal("Conquistada", resultado.Status);
        Assert.Equal(data, resultado.DataConquista);
        Assert.Equal(4000m, resultado.ValorPago);
        Assert.Equal("AVista", resultado.FormaPagamento);
    }

    [Fact]
    public async Task Conquistar_AVistaComResgateDeInvestimento_CriaTransferenciaEGravaId()
    {
        var clienteId = Guid.NewGuid();
        var contaInvestimentoId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.ContaInvestimentoId = contaInvestimentoId;
        var contaId = Guid.NewGuid();
        var conta = new ContaBancaria { Id = contaId, ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true };
        var contaInvestimento = new ContaBancaria { Id = contaInvestimentoId, ClienteId = clienteId, Tipo = "Investimento", Ativa = true, SaldoInicial = 5000m };
        var data = new DateOnly(2026, 11, 10);
        var transferenciaId = Guid.NewGuid();

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaInvestimentoId)).ReturnsAsync(contaInvestimento);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());
        // Saldo real da reserva (5000, via SaldoInicial — sem registros) é maior que o valor pago
        // (4000), então o resgate sai integral — ver teste dedicado pro caso saldo insuficiente.
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaInvestimentoId)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _transferenciaServiceMock
            .Setup(s => s.CriarAsync(clienteId, contaInvestimentoId, contaId, data, 4000m, It.IsAny<string>(), clienteId, "cliente"))
            .ReturnsAsync(new TransferenciaDto { Id = transferenciaId });

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = data, FormaPagamento = "AVista",
            ContaPagamentoId = contaId, Categoria = "Equipamentos", ResgatarDoInvestimento = true,
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        _transferenciaServiceMock.Verify(s => s.CriarAsync(
            clienteId, contaInvestimentoId, contaId, data, 4000m, It.IsAny<string>(), clienteId, "cliente"), Times.Once);
        Assert.Equal(transferenciaId, resultado.TransferenciaResgateId);
    }

    // Item 2 (Money Vision): saldo real da reserva menor que o valor pago — resgata só o
    // disponível (nunca o que TransferenciaService deixaria a reserva negativa); a diferença sai
    // naturalmente do saldo que a conta de pagamento já tinha (split reserva + conta de pagamento).
    [Fact]
    public async Task Conquistar_AVistaComResgate_SaldoRealMenorQueOValorPago_ResgataSoOSaldoDisponivel()
    {
        var clienteId = Guid.NewGuid();
        var contaInvestimentoId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.ContaInvestimentoId = contaInvestimentoId;
        var contaId = Guid.NewGuid();
        var conta = new ContaBancaria { Id = contaId, ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true };
        // Saldo real da reserva: só 1500 (via SaldoInicial), bem menos que os 4000 pagos.
        var contaInvestimento = new ContaBancaria { Id = contaInvestimentoId, ClienteId = clienteId, Tipo = "Investimento", Ativa = true, SaldoInicial = 1500m };
        var data = new DateOnly(2026, 11, 10);
        var transferenciaId = Guid.NewGuid();

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaInvestimentoId)).ReturnsAsync(contaInvestimento);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaInvestimentoId)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _transferenciaServiceMock
            .Setup(s => s.CriarAsync(clienteId, contaInvestimentoId, contaId, data, 1500m, It.IsAny<string>(), clienteId, "cliente"))
            .ReturnsAsync(new TransferenciaDto { Id = transferenciaId });

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = data, FormaPagamento = "AVista",
            ContaPagamentoId = contaId, Categoria = "Equipamentos", ResgatarDoInvestimento = true,
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        _transferenciaServiceMock.Verify(s => s.CriarAsync(
            clienteId, contaInvestimentoId, contaId, data, 1500m, It.IsAny<string>(), clienteId, "cliente"), Times.Once);
        Assert.Equal(transferenciaId, resultado.TransferenciaResgateId);
    }

    [Fact]
    public async Task Conquistar_AVistaSemPedirResgate_NaoCriaTransferencia()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.ContaInvestimentoId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var data = new DateOnly(2026, 11, 10);
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaId))
            .ReturnsAsync(new ContaBancaria { Id = contaId, ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true });
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = data, FormaPagamento = "AVista",
            ContaPagamentoId = contaId, Categoria = "Equipamentos", ResgatarDoInvestimento = false,
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        _transferenciaServiceMock.Verify(s => s.CriarAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        Assert.Null(resultado.TransferenciaResgateId);
    }

    [Fact]
    public async Task Conquistar_Boleto_CriaUmaContaAPagarPorParcelaComOrigemMeta()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId, "Viagem");
        var contaId = Guid.NewGuid();
        var conta = new ContaBancaria { Id = contaId, ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true };
        var dataCompra = new DateOnly(2026, 11, 10);
        var primeiraParcela = new DateOnly(2026, 12, 5);

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        var criadas = new List<CriarContaProvisionadaDto>();
        _contaProvisionadaServiceMock
            .Setup(s => s.CriarAsync(It.IsAny<CriarContaProvisionadaDto>(), clienteId, "cliente", meta.Id))
            .Callback<CriarContaProvisionadaDto, Guid, string, Guid?>((d, _, _, _) => criadas.Add(d))
            .ReturnsAsync(new ContaProvisionadaDto());

        var dto = new ConquistarMetaDto
        {
            ValorPago = 3000m, Data = dataCompra, FormaPagamento = "Boleto", Parcelas = 3,
            DataPrimeiraParcela = primeiraParcela, ContaPagamentoId = contaId, Categoria = "Viagem",
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        Assert.Equal(3, criadas.Count);
        Assert.All(criadas, c => Assert.Equal("Pagar", c.Tipo));
        Assert.Equal(new[] { 1000m, 1000m, 1000m }, criadas.Select(c => c.Valor));
        Assert.Equal(
            new DateOnly?[] { primeiraParcela, primeiraParcela.AddMonths(1), primeiraParcela.AddMonths(2) },
            criadas.Select(c => c.DataVencimento));
        Assert.Equal("🏆 Viagem (1/3)", criadas[0].Descricao);
        Assert.Equal("Conquistada", resultado.Status);
        Assert.Equal(3, resultado.Parcelas);
    }

    [Fact]
    public async Task Conquistar_MetaJaConquistada_LancaMetaJaConquistada()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = new DateOnly(2026, 11, 10), FormaPagamento = "AVista",
            ContaPagamentoId = Guid.NewGuid(), Categoria = "Equipamentos",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.META_JA_CONQUISTADA, ex.Codigo);
    }

    [Fact]
    public async Task Conquistar_FormaFinanciado_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = new DateOnly(2026, 11, 10), FormaPagamento = "Financiado",
            ContaPagamentoId = Guid.NewGuid(), Categoria = "Equipamentos",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Conquistar_Cartao_ChamaFaturaCartaoServiceComOrigemMeta()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId, "Notebook");
        var contaCartaoId = Guid.NewGuid();
        var contaCartao = new ContaBancaria { Id = contaCartaoId, ClienteId = clienteId, Tipo = "CartaoCredito", Ativa = true };
        var data = new DateOnly(2026, 11, 10);

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartaoId)).ReturnsAsync(contaCartao);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var dataPrimeiraParcela = new DateOnly(2026, 12, 5);
        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = data, FormaPagamento = "Cartao", Parcelas = 10,
            DataPrimeiraParcela = dataPrimeiraParcela, ContaPagamentoId = contaCartaoId, Categoria = "Equipamentos",
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        // Item 3 (Money Vision): a 1ª parcela vence no próximo vencimento do cartão
        // (DataPrimeiraParcela); a compra em si continua na data da compra.
        _faturaCartaoServiceMock.Verify(s => s.LancarComprasParceladaAsync(
            contaCartaoId, clienteId, "cliente",
            It.Is<LancarCompraParceladaDto>(d => d.ValorTotal == 4000m && d.QuantidadeParcelas == 10
                && d.Descricao == "🏆 Notebook" && d.Categoria == "Equipamentos" && d.Data == data
                && d.DataPrimeiraParcela == dataPrimeiraParcela),
            meta.Id), Times.Once);
        Assert.Equal("Conquistada", resultado.Status);
    }

    [Fact]
    public async Task Conquistar_CartaoSemDataPrimeiraParcela_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var contaCartaoId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartaoId))
            .ReturnsAsync(new ContaBancaria { Id = contaCartaoId, ClienteId = clienteId, Tipo = "CartaoCredito", Ativa = true });

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = new DateOnly(2026, 11, 10), FormaPagamento = "Cartao", Parcelas = 10,
            ContaPagamentoId = contaCartaoId, Categoria = "Equipamentos",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("dataPrimeiraParcela", ex.Campo);
    }

    [Fact]
    public async Task Conquistar_CartaoComUmaParcela_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var contaCartaoId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaCartaoId))
            .ReturnsAsync(new ContaBancaria { Id = contaCartaoId, ClienteId = clienteId, Tipo = "CartaoCredito", Ativa = true });

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = new DateOnly(2026, 11, 10), FormaPagamento = "Cartao", Parcelas = 1,
            ContaPagamentoId = contaCartaoId, Categoria = "Equipamentos",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Conquistar_CartaoComContaQueNaoECartao_LancaContaNaoECartao()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var contaId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaId))
            .ReturnsAsync(new ContaBancaria { Id = contaId, ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true });

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = new DateOnly(2026, 11, 10), FormaPagamento = "Cartao", Parcelas = 5,
            ContaPagamentoId = contaId, Categoria = "Equipamentos",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto));

        Assert.Equal(CodigoRetorno.CONTA_NAO_E_CARTAO, ex.Codigo);
    }

    [Fact]
    public async Task Conquistar_ContaPagamentoInativa_LancaContaInativa()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var contaId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(contaId))
            .ReturnsAsync(new ContaBancaria { Id = contaId, ClienteId = clienteId, Ativa = false });

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = new DateOnly(2026, 11, 10), FormaPagamento = "AVista",
            ContaPagamentoId = contaId, Categoria = "Equipamentos",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto));

        Assert.Equal(CodigoRetorno.CONTA_INATIVA, ex.Codigo);
    }

    // ── DesfazerConquistaAsync (Fase 5.5) ───────────────────────────────────────────────────────

    [Fact]
    public async Task DesfazerConquista_AVista_RemoveASaidaEVoltaMetaParaEmAndamento()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        meta.DataConquista = new DateOnly(2026, 11, 10);
        meta.ValorPago = 4000m;
        meta.FormaPagamento = "AVista";

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = meta.DataConquista.Value,
            Inicio = 10000m, Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 6000m,
            Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "🏆 Macbook", Valor = 4000m, OrigemMetaId = meta.Id } },
        };

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            new() { Id = contaId, ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true },
        });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var resultado = await _sut.DesfazerConquistaAsync(meta.Id, clienteId, "cliente");

        Assert.Empty(registro.Saidas);
        Assert.Equal(10000m, registro.SaldoFinal);
        Assert.Equal("🏆 Macbook", resultado.Removidos.Single());
        Assert.Empty(resultado.Mantidos);
        Assert.Equal("EmAndamento", meta.Status);
        Assert.Null(meta.ValorPago);
    }

    [Fact]
    public async Task DesfazerConquista_Boleto_RemoveParcelasNaoPagasEMantemAsPagas()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        meta.FormaPagamento = "Boleto";

        var parcelaPaga = new ContaProvisionada { Id = Guid.NewGuid(), Descricao = "🏆 Viagem (1/3)", Valor = 1000m, OrigemMetaId = meta.Id, Pago = true };
        var parcelaPendente1 = new ContaProvisionada { Id = Guid.NewGuid(), Descricao = "🏆 Viagem (2/3)", Valor = 1000m, OrigemMetaId = meta.Id, Pago = false };
        var parcelaPendente2 = new ContaProvisionada { Id = Guid.NewGuid(), Descricao = "🏆 Viagem (3/3)", Valor = 1000m, OrigemMetaId = meta.Id, Pago = false };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 12, 5),
            Entradas = new(), Saidas = new(), ContasReceber = new(), SaldoFinal = 0m,
            ContasPagar = new() { parcelaPaga, parcelaPendente1, parcelaPendente2 },
        };

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            new() { Id = contaId, ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true },
        });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var resultado = await _sut.DesfazerConquistaAsync(meta.Id, clienteId, "cliente");

        Assert.Equal(new[] { parcelaPaga }, registro.ContasPagar);
        Assert.Equal(2, resultado.Removidos.Count);
        Assert.Contains("🏆 Viagem (1/3) (já paga)", resultado.Mantidos);
        Assert.Equal("EmAndamento", meta.Status);
    }

    [Fact]
    public async Task DesfazerConquista_Cartao_MantemParcelaDeFaturaJaPaga()
    {
        var clienteId = Guid.NewGuid();
        var contaCartaoId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        meta.FormaPagamento = "Cartao";

        var registroPago = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCartaoId, Data = new DateOnly(2026, 11, 10),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = -400m,
            Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "🏆 Notebook (1/10)", Valor = 400m, OrigemMetaId = meta.Id } },
        };
        var registroNaoPago = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCartaoId, Data = new DateOnly(2026, 12, 10),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = -400m,
            Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "🏆 Notebook (2/10)", Valor = 400m, OrigemMetaId = meta.Id } },
        };

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            new() { Id = contaCartaoId, ClienteId = clienteId, Tipo = "CartaoCredito", Ativa = true },
        });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<RegistroDiario> { registroPago, registroNaoPago });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaCartaoId))
            .ReturnsAsync(new List<RegistroDiario> { registroPago, registroNaoPago });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _faturaCartaoServiceMock.Setup(s => s.ListarFaturasAsync(contaCartaoId, clienteId, "cliente"))
            .ReturnsAsync(new List<CaixaDiario.API.DTOs.FaturasCartao.FaturaCartaoDto>
            {
                new() { Competencia = "2026-11", Status = "Paga" },
                new() { Competencia = "2026-12", Status = "Aberta" },
            });

        var resultado = await _sut.DesfazerConquistaAsync(meta.Id, clienteId, "cliente");

        Assert.Empty(registroNaoPago.Saidas);
        Assert.Single(registroPago.Saidas); // mantido — fatura de novembro já paga
        Assert.Equal("🏆 Notebook (2/10)", resultado.Removidos.Single());
        Assert.Contains("🏆 Notebook (1/10) (fatura 2026-11 já paga)", resultado.Mantidos);
    }

    [Fact]
    public async Task DesfazerConquista_ResgateAindaProvisorio_ExcluiATransferencia()
    {
        var clienteId = Guid.NewGuid();
        var contaInvestimentoId = Guid.NewGuid();
        var contaPagamentoId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var data = new DateOnly(2026, 11, 10);
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        meta.ContaInvestimentoId = contaInvestimentoId;
        meta.ContaPagamentoId = contaPagamentoId;
        meta.DataConquista = data;
        meta.TransferenciaResgateId = transferenciaId;

        var regOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaInvestimentoId, Data = data,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
            Saidas = new() { new() { Id = Guid.NewGuid(), TransferenciaId = transferenciaId, Provisoria = true, Valor = 4000m } },
        };
        var regDestino = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaPagamentoId, Data = data,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
            Entradas = new() { new() { Id = Guid.NewGuid(), TransferenciaId = transferenciaId, Provisoria = true, Valor = 4000m } },
        };

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>());
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaInvestimentoId, data)).ReturnsAsync(regOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaPagamentoId, data)).ReturnsAsync(regDestino);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var resultado = await _sut.DesfazerConquistaAsync(meta.Id, clienteId, "cliente");

        _transferenciaServiceMock.Verify(s => s.ExcluirAsync(transferenciaId, clienteId, "cliente"), Times.Once);
        Assert.Contains("Resgate do investimento", resultado.Removidos);
        Assert.Null(meta.TransferenciaResgateId);
    }

    [Fact]
    public async Task DesfazerConquista_ResgateJaReconciliado_MantemATransferencia()
    {
        var clienteId = Guid.NewGuid();
        var contaInvestimentoId = Guid.NewGuid();
        var contaPagamentoId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var data = new DateOnly(2026, 11, 10);
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        meta.ContaInvestimentoId = contaInvestimentoId;
        meta.ContaPagamentoId = contaPagamentoId;
        meta.DataConquista = data;
        meta.TransferenciaResgateId = transferenciaId;

        var regOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaInvestimentoId, Data = data,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
            // Provisoria=false — já reconciliou com o extrato real importado depois.
            Saidas = new() { new() { Id = Guid.NewGuid(), TransferenciaId = transferenciaId, Provisoria = false, Valor = 4000m } },
        };
        var regDestino = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaPagamentoId, Data = data,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
            Entradas = new() { new() { Id = Guid.NewGuid(), TransferenciaId = transferenciaId, Provisoria = true, Valor = 4000m } },
        };

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>());
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaInvestimentoId, data)).ReturnsAsync(regOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaPagamentoId, data)).ReturnsAsync(regDestino);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var resultado = await _sut.DesfazerConquistaAsync(meta.Id, clienteId, "cliente");

        _transferenciaServiceMock.Verify(s => s.ExcluirAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        Assert.Contains("Resgate do investimento (já reconciliado com o extrato)", resultado.Mantidos);
        Assert.Equal(transferenciaId, meta.TransferenciaResgateId);
    }

    [Fact]
    public async Task DesfazerConquista_MetaNaoConquistada_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.DesfazerConquistaAsync(meta.Id, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Conquistar_ClienteTentandoConquistarMetaDeOutroCliente_LancaAcessoNegado()
    {
        var meta = CriarMetaEmAndamento(Guid.NewGuid());
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);

        var dto = new ConquistarMetaDto
        {
            ValorPago = 4000m, Data = new DateOnly(2026, 11, 10), FormaPagamento = "AVista",
            ContaPagamentoId = Guid.NewGuid(), Categoria = "Equipamentos",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConquistarAsync(meta.Id, Guid.NewGuid(), "cliente", dto));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    // ── "Conquistei → Já lancei essa compra" (Money Vision: compra duplicada) ────────────────────

    private static Parcelamento CriarParcelamentoTrocafone(Guid clienteId, Guid contaCartaoId, decimal valorParcela) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, ContaCartaoId = contaCartaoId, DescricaoBase = "Trocafone",
        ValorParcela = valorParcela, TotalParcelas = 10, DataPrimeira = new DateOnly(2026, 10, 20),
        ValorTotal = valorParcela * 10, Status = "Ativo",
    };

    // Parcela 1 real (na fatura do banco) + parcelas 2..10 previstas em Contas a Pagar.
    private static List<RegistroDiario> CriarParcelasTrocafone(Guid clienteId, Guid contaCartaoId, Parcelamento p, Guid faturaId)
    {
        var real = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCartaoId, Data = new DateOnly(2026, 10, 5),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Trocafone - Parcela 1/10", Valor = p.ValorParcela, ParcelamentoId = p.Id, NumeroParcela = 1, TotalParcelas = 10, FaturaId = faturaId } },
        };
        var previstas = Enumerable.Range(2, 9).Select(n => new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCartaoId, Data = p.DataPrimeira.AddMonths(n - 1),
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new() { new() { Id = Guid.NewGuid(), Descricao = $"Trocafone - Parcela {n}/10", Valor = p.ValorParcela, ParcelamentoId = p.Id, NumeroParcela = n } },
        });
        return previstas.Prepend(real).ToList();
    }

    [Fact]
    public async Task Conquistar_JaLancadoComDoisParcelamentos_VinculaSemCriarLancamentoEMostraAndamento()
    {
        var clienteId = Guid.NewGuid();
        var contaCartaoId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var nubank = CriarParcelamentoTrocafone(clienteId, contaCartaoId, 199.16m);
        var c6 = CriarParcelamentoTrocafone(clienteId, contaCartaoId, 210.00m);
        var faturaNubank = new Fatura { Id = Guid.NewGuid(), Banco = "Nubank", Competencia = "2026-10" };
        var faturaC6 = new Fatura { Id = Guid.NewGuid(), Banco = "C6", Competencia = "2026-10" };
        var registros = CriarParcelasTrocafone(clienteId, contaCartaoId, nubank, faturaNubank.Id)
            .Concat(CriarParcelasTrocafone(clienteId, contaCartaoId, c6, faturaC6.Id)).ToList();

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _parcelamentoRepoMock.Setup(r => r.ObterPorIdAsync(nubank.Id)).ReturnsAsync(nubank);
        _parcelamentoRepoMock.Setup(r => r.ObterPorIdAsync(c6.Id)).ReturnsAsync(c6);
        _parcelamentoRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Parcelamento>())).ReturnsAsync((Parcelamento p) => p);
        _parcelamentoRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<Parcelamento> { nubank, c6 });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);
        _contaBancariaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            new() { Id = contaCartaoId, ClienteId = clienteId, Tipo = "CartaoCredito", Ativa = true },
        });
        _faturaRepoMock.Setup(r => r.ListarPorContaAsync(contaCartaoId)).ReturnsAsync(new List<Fatura> { faturaNubank, faturaC6 });

        var dto = new ConquistarMetaDto
        {
            Modo = "JaLancado", FormaPagamento = "Cartao", Data = new DateOnly(2026, 10, 29), ParcelamentoIds = { nubank.Id, c6.Id },
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        _faturaCartaoServiceMock.Verify(s => s.LancarComprasParceladaAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<LancarCompraParceladaDto>(), It.IsAny<Guid?>()), Times.Never);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        Assert.Equal((meta.Id, meta.Id), (nubank.MetaId, c6.MetaId));
        Assert.Equal(("Conquistada", 4091.60m, contaCartaoId), (resultado.Status, resultado.ValorPago, resultado.ContaPagamentoId));

        var andamento = resultado.Andamento!;
        Assert.Equal((409.16m, 4091.60m), (andamento.ValorPago, andamento.ValorTotal));
        Assert.Equal(new DateOnly(2027, 7, 20), andamento.DataTermino);
        Assert.Equal(409.16m, andamento.ValorMensal);
        Assert.Equal(new[] { "Nubank", "C6" }, andamento.Parcelamentos.Select(p => p.Banco));
        Assert.All(andamento.Parcelamentos, p => Assert.Equal((1, 10), (p.ParcelasPagas, p.TotalParcelas)));
    }

    [Fact]
    public async Task Conquistar_JaLancadoComLancamentoSoltoParcela1De10_CriaOParcelamentoAPartirDele()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var cartao = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Tipo = "CartaoCredito", Ativa = true };
        var fatura = new Fatura { Id = Guid.NewGuid(), Banco = "C6", Competencia = "2026-10", DataVencimento = new DateOnly(2026, 10, 20) };
        var saida = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "TROCAFONE", Valor = 210m, Categoria = "Equipamentos", FaturaId = fatura.Id };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = cartao.Id, Data = new DateOnly(2026, 10, 5),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), Saidas = new() { saida },
        };
        var criado = CriarParcelamentoTrocafone(clienteId, cartao.Id, 210m);

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(cartao.Id)).ReturnsAsync(cartao);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(cartao.Id, registro.Data)).ReturnsAsync(registro);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _faturaRepoMock.Setup(r => r.ObterPorIdAsync(fatura.Id)).ReturnsAsync(fatura);
        _parcelamentoDeteccaoServiceMock.Setup(s => s.ProcessarParcelaDetectadaAsync(
                cartao, fatura.DataVencimento, "TROCAFONE", "Equipamentos", 1, 10, 210m))
            .ReturnsAsync(criado);
        _parcelamentoRepoMock.Setup(r => r.ObterPorIdAsync(criado.Id)).ReturnsAsync(criado);
        _parcelamentoRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Parcelamento>())).ReturnsAsync((Parcelamento p) => p);

        var dto = new ConquistarMetaDto
        {
            Modo = "JaLancado", FormaPagamento = "Cartao", Data = new DateOnly(2026, 10, 29),
            Lancamentos = { new() { LancamentoId = saida.Id, ContaBancariaId = cartao.Id, Data = registro.Data, NumeroParcela = 1, TotalParcelas = 10 } },
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        Assert.Equal((criado.Id, 1, 10), (saida.ParcelamentoId!.Value, saida.NumeroParcela!.Value, saida.TotalParcelas!.Value));
        Assert.Equal(meta.Id, criado.MetaId);
        Assert.Null(saida.MetaVinculadaId); // o vínculo é o parcelamento inteiro, não só esta parcela
        Assert.Equal(2100m, resultado.ValorPago);
    }

    [Fact]
    public async Task Conquistar_JaLancadoComLancamentoAVista_MarcaComoVinculadoSemApagarNemCriar()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var conta = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true };
        var saida = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Pix Loja", Valor = 3800m, Categoria = "Equipamentos" };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 10, 5),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), Saidas = new() { saida }, SaldoFinal = 1000m,
        };
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, registro.Data)).ReturnsAsync(registro);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new ConquistarMetaDto
        {
            Modo = "JaLancado", FormaPagamento = "AVista", Data = new DateOnly(2026, 10, 5),
            Lancamentos = { new() { LancamentoId = saida.Id, ContaBancariaId = conta.Id, Data = registro.Data } },
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        Assert.Equal(meta.Id, Assert.Single(registro.Saidas).MetaVinculadaId);
        Assert.Equal(1000m, registro.SaldoFinal);
        Assert.Equal(3800m, resultado.ValorPago);
    }

    [Fact]
    public async Task Conquistar_JaLancadoSemNenhumItem_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);

        var dto = new ConquistarMetaDto { Modo = "JaLancado", FormaPagamento = "Cartao", Data = new DateOnly(2026, 10, 29) };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto));

        Assert.Equal((400, CodigoRetorno.DADOS_INVALIDOS), (ex.StatusCode, ex.Codigo));
    }

    [Fact]
    public async Task Conquistar_ParcelamentoDeOutraMeta_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var deOutra = CriarParcelamentoTrocafone(clienteId, Guid.NewGuid(), 199.16m);
        deOutra.MetaId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _parcelamentoRepoMock.Setup(r => r.ObterPorIdAsync(deOutra.Id)).ReturnsAsync(deOutra);

        var dto = new ConquistarMetaDto { Modo = "JaLancado", FormaPagamento = "Cartao", Data = new DateOnly(2026, 10, 29), ParcelamentoIds = { deOutra.Id } };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto));

        Assert.Equal("parcelamentoIds", ex.Campo);
        _parcelamentoRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<Parcelamento>()), Times.Never);
    }

    [Fact]
    public async Task Conquistar_CartaoComValorDaParcela_GravaOTotalReal()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var cartao = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Tipo = "CartaoCredito", Ativa = true };
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _contaBancariaRepoMock.Setup(r => r.ObterPorIdAsync(cartao.Id)).ReturnsAsync(cartao);

        var dto = new ConquistarMetaDto
        {
            FormaPagamento = "Cartao", Data = new DateOnly(2026, 10, 15), Parcelas = 10, ValorParcela = 199.16m, Banco = "Nubank",
            DataPrimeiraParcela = new DateOnly(2026, 10, 20), ContaPagamentoId = cartao.Id, Categoria = "Equipamentos",
        };

        var resultado = await _sut.ConquistarAsync(meta.Id, clienteId, "cliente", dto);

        _faturaCartaoServiceMock.Verify(s => s.LancarComprasParceladaAsync(cartao.Id, clienteId, "cliente",
            It.Is<LancarCompraParceladaDto>(d => d.Banco == "Nubank" && d.ValorParcela == 199.16m), meta.Id), Times.Once);
        Assert.Equal(1991.60m, resultado.ValorPago);
    }

    // ── Desfazer: apaga tudo que o "Conquistei" criou, só solta o que já existia ────────────────

    [Fact]
    public async Task DesfazerConquista_CartaoLancadoPeloApp_RemoveParcelaPrevistasParcelamentoEFaturaVazia()
    {
        var clienteId = Guid.NewGuid();
        var contaCartaoId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        meta.FormaPagamento = "Cartao";
        var parcelamento = CriarParcelamentoTrocafone(clienteId, contaCartaoId, 400m);
        parcelamento.MetaId = meta.Id;
        var fatura = new Fatura { Id = Guid.NewGuid(), Banco = "Nubank", Competencia = "2026-10" };
        var registros = CriarParcelasTrocafone(clienteId, contaCartaoId, parcelamento, fatura.Id);
        foreach (var s in registros.SelectMany(r => r.Saidas)) s.OrigemMetaId = meta.Id;
        foreach (var c in registros.SelectMany(r => r.ContasPagar)) c.OrigemMetaId = meta.Id;
        registros[0].SaldoFinal = -400m;

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _contaBancariaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            new() { Id = contaCartaoId, ClienteId = clienteId, Tipo = "CartaoCredito", Ativa = true },
        });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaCartaoId)).ReturnsAsync(registros);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _parcelamentoRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<Parcelamento> { parcelamento });
        _faturaRepoMock.Setup(r => r.ObterPorIdAsync(fatura.Id)).ReturnsAsync(fatura);
        // Antes de remover: a fatura tem a parcela 1; depois, nenhuma compra (some da listagem).
        _faturaCartaoServiceMock.SetupSequence(s => s.ListarFaturasAsync(contaCartaoId, clienteId, "cliente"))
            .ReturnsAsync(new List<CaixaDiario.API.DTOs.FaturasCartao.FaturaCartaoDto> { new() { Id = fatura.Id, Competencia = "2026-10", Status = "Aberta" } })
            .ReturnsAsync(new List<CaixaDiario.API.DTOs.FaturasCartao.FaturaCartaoDto>());

        var resultado = await _sut.DesfazerConquistaAsync(meta.Id, clienteId, "cliente");

        Assert.Empty(registros.SelectMany(r => r.Saidas));
        Assert.Empty(registros.SelectMany(r => r.ContasPagar));
        Assert.Equal(0m, registros[0].SaldoFinal);
        _parcelamentoRepoMock.Verify(r => r.RemoverAsync(parcelamento), Times.Once);
        _faturaRepoMock.Verify(r => r.RemoverAsync(fatura), Times.Once);
        Assert.Empty(resultado.Mantidos);
        Assert.Equal("EmAndamento", meta.Status);
    }

    [Fact]
    public async Task DesfazerConquista_JaLancado_SoDesvinculaSemApagarNada()
    {
        var clienteId = Guid.NewGuid();
        var contaCartaoId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        meta.FormaPagamento = "Cartao";
        var parcelamento = CriarParcelamentoTrocafone(clienteId, contaCartaoId, 199.16m);
        parcelamento.MetaId = meta.Id;
        var registros = CriarParcelasTrocafone(clienteId, contaCartaoId, parcelamento, Guid.NewGuid());
        var avulsa = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Capa", Valor = 50m, MetaVinculadaId = meta.Id };
        registros[0].Saidas.Add(avulsa);

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _parcelamentoRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<Parcelamento> { parcelamento });
        _parcelamentoRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Parcelamento>())).ReturnsAsync((Parcelamento p) => p);

        var resultado = await _sut.DesfazerConquistaAsync(meta.Id, clienteId, "cliente");

        Assert.Equal(2, registros[0].Saidas.Count);
        Assert.Null(avulsa.MetaVinculadaId);
        Assert.Equal(9, registros.SelectMany(r => r.ContasPagar).Count());
        Assert.Null(parcelamento.MetaId);
        _parcelamentoRepoMock.Verify(r => r.RemoverAsync(It.IsAny<Parcelamento>()), Times.Never);
        Assert.Empty(resultado.Removidos);
    }

    [Fact]
    public async Task DesfazerConquista_ParcelaDoAppJaMescladaComAFaturaReal_MantemOLancamento()
    {
        var clienteId = Guid.NewGuid();
        var contaCartaoId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        meta.FormaPagamento = "Cartao";
        var mesclada = new ItemFinanceiroSaida
        {
            Id = Guid.NewGuid(), Descricao = "Trocafone - Parcela 1/10", Valor = 199.16m, OrigemMetaId = meta.Id, FitId = "nubank-123",
        };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCartaoId, Data = new DateOnly(2026, 10, 5),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), Saidas = new() { mesclada }, SaldoFinal = -199.16m,
        };
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.DesfazerConquistaAsync(meta.Id, clienteId, "cliente");

        Assert.Equal((-199.16m, (Guid?)null), (registro.SaldoFinal, Assert.Single(registro.Saidas).OrigemMetaId));
        Assert.Single(resultado.Mantidos);
    }

    // ── ListarCandidatosVinculoAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListarCandidatosVinculo_SoOfereceCompraDosUltimos90DiasQueNaoEhDeOutraMeta()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        var cartao = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Cartões", Tipo = "CartaoCredito", Ativa = true };
        var fatura = new Fatura { Id = Guid.NewGuid(), Banco = "Nubank", Competencia = "2026-10" };
        var livre = CriarParcelamentoTrocafone(clienteId, cartao.Id, 199.16m);
        var deOutraMeta = CriarParcelamentoTrocafone(clienteId, cartao.Id, 210m);
        deOutraMeta.MetaId = Guid.NewGuid();

        ItemFinanceiroSaida Saida(string descricao) => new() { Id = Guid.NewGuid(), Descricao = descricao, Valor = 10m };
        var trocafone = new ItemFinanceiroSaida
        {
            Id = Guid.NewGuid(), Descricao = "Trocafone - Parcela 1/10", Valor = 199.16m, FaturaId = fatura.Id,
            ParcelamentoId = livre.Id, NumeroParcela = 1, TotalParcelas = 10,
        };
        var recente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = cartao.Id, Data = Hoje.AddDays(-30),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new()
            {
                trocafone,
                new() { Id = Guid.NewGuid(), Descricao = "Pagamento fatura", Valor = 500m, TipoCusto = LancamentoFiltro.TipoPagamentoFatura },
                new() { Id = Guid.NewGuid(), Descricao = "🏆 Outra meta", Valor = 100m, OrigemMetaId = Guid.NewGuid() },
                new() { Id = Guid.NewGuid(), Descricao = "Vinculado a outra", Valor = 100m, MetaVinculadaId = Guid.NewGuid() },
            },
        };
        var antigo = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = cartao.Id, Data = Hoje.AddDays(-91),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), Saidas = new() { Saida("Antiga") },
        };

        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        _contaBancariaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { cartao });
        _faturaRepoMock.Setup(r => r.ListarPorContaAsync(cartao.Id)).ReturnsAsync(new List<Fatura> { fatura });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { recente, antigo });
        _parcelamentoRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<Parcelamento> { livre, deOutraMeta });

        var resultado = await _sut.ListarCandidatosVinculoAsync(meta.Id, clienteId, "cliente");

        var lancamento = Assert.Single(resultado.Lancamentos);
        Assert.Equal((trocafone.Id, "Nubank", true, "Cartões"), (lancamento.Id, lancamento.Banco, lancamento.ContaEhCartao, lancamento.ContaNome));
        var parcelamento = Assert.Single(resultado.Parcelamentos);
        Assert.Equal((livre.Id, "Nubank", 10), (parcelamento.Id, parcelamento.Banco, parcelamento.ParcelasPagas));
    }

    [Fact]
    public async Task ListarCandidatosVinculo_ClienteAcessandoMetaDeOutro_LancaAcessoNegado()
    {
        var meta = CriarMetaEmAndamento(Guid.NewGuid());
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ListarCandidatosVinculoAsync(meta.Id, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ListarMetas_MetaConquistadaAVista_AndamentoMostraOValorPago()
    {
        var clienteId = Guid.NewGuid();
        var meta = CriarMetaEmAndamento(clienteId);
        meta.Status = "Conquistada";
        meta.FormaPagamento = "AVista";
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = Guid.NewGuid(), Data = new DateOnly(2026, 10, 5),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "🏆 Macbook", Valor = 3900m, OrigemMetaId = meta.Id } },
        };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual> { meta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var andamento = Assert.Single(await _sut.ListarMetasAsync(clienteId, clienteId, "cliente")).Andamento!;

        Assert.Equal((3900m, 3900m, 0), (andamento.ValorPago, andamento.ValorTotal, andamento.Parcelamentos.Count));
        Assert.Null(andamento.DataTermino);
    }
}
