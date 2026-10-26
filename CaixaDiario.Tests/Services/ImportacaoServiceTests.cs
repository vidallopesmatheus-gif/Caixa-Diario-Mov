using System.Text;
using ClosedXML.Excel;
using CaixaDiario.API.Data;
using CaixaDiario.API.DTOs.Importacao;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ImportacaoServiceTests
{
    // InMemory não é relacional — o serviço detecta isso (Database.IsRelational()) e não tenta abrir
    // transação em ExcluirLoteAsync, então os testes continuam simulando só via mocks dos repositórios.
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<ITransacaoImportadaRepository> _importRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<ICategoriaRepository> _categoriaRepoMock = new();
    private readonly Mock<IRegraCategorizacaoRepository> _regraRepoMock = new();
    private readonly Mock<ITransferenciaService> _transferenciaServiceMock = new();
    private readonly Mock<IConciliacaoService> _conciliacaoServiceMock = new();
    private readonly Mock<IFaturaRepository> _faturaRepoMock = new();
    private readonly Mock<IParcelamentoDeteccaoService> _parcelamentoDeteccaoServiceMock = new();
    private readonly ImportacaoService _sut;

    public ImportacaoServiceTests()
    {
        // Espelha o Plano de Contas real (nome -> tipo) só com as categorias usadas nos testes,
        // pra AtualizarCategoriasAsync/ImportarArquivoAsync resolverem o TipoCusto sem precisar
        // configurar isso em todo teste individualmente.
        _categoriaRepoMock.Setup(r => r.ListarTodasPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Manutenção", Tipo = "CustoFixo" },
            new() { Id = Guid.NewGuid(), Nome = "Material de Escritório", Tipo = "CustoFixo" },
            new() { Id = Guid.NewGuid(), Nome = "Aluguel", Tipo = "CustoFixo" },
        });
        // Nenhuma regra ativa por padrão — os testes que não mexem com regra ficam com o
        // comportamento de sempre (sugestão por palavra-chave / pendente).
        _regraRepoMock.Setup(r => r.ListarAtivasPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<RegraCategorizacao>());
        // Fallback padrão pro histórico de importação (dedup via FitId/ignoradas) — sem isso,
        // ObterIgnoradasAsync quebraria em todo teste que não mexe especificamente com exclusão.
        _importRepoMock.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<TransacaoImportada>());
        // Fallback padrão pro motor de sugestão de vínculo (Fase 1.2/1.7) — só o resumo final usa.
        _conciliacaoServiceMock.Setup(s => s.ListarSugestoesAsync(
            It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new List<CaixaDiario.API.DTOs.Conciliacao.SugestaoVinculoDto>());
        _faturaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Fatura>())).ReturnsAsync((Fatura f) => f);
        // Fallback padrão pra detecção de parcelamento — a maioria dos testes não liga pra qual
        // Parcelamento foi resolvido, só não pode quebrar com NullReferenceException.
        _parcelamentoDeteccaoServiceMock.Setup(s => s.ProcessarParcelaDetectadaAsync(
            It.IsAny<ContaBancaria>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
            .ReturnsAsync((ContaBancaria conta, DateOnly _, string comerciante, string? categoria, int numero, int total, decimal valor) => new Parcelamento
            {
                Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaCartaoId = conta.Id, DescricaoBase = comerciante,
                Categoria = categoria, ValorParcela = valor, TotalParcelas = total, Status = "Ativo", CriadoEm = DateTime.UtcNow,
            });
        _sut = new ImportacaoService(_contaRepoMock.Object, _importRepoMock.Object, _registroRepoMock.Object, _categoriaRepoMock.Object,
            _regraRepoMock.Object, _transferenciaServiceMock.Object, _conciliacaoServiceMock.Object, _faturaRepoMock.Object,
            _parcelamentoDeteccaoServiceMock.Object, CriarContexto());
    }

    private static ContaBancaria CriarConta(Guid contaId, Guid clienteId, decimal saldoInicial = 0m, string tipo = "ContaCorrente") => new()
    {
        Id = contaId,
        ClienteId = clienteId,
        Nome = "Conta Teste",
        Tipo = tipo,
        SaldoInicial = saldoInicial,
        Ativa = true,
        DataCriacao = DateTime.UtcNow,
    };

    private static IFormFile CriarArquivoTexto(string nome, string conteudo)
    {
        var bytes = Encoding.Latin1.GetBytes(conteudo);
        var stream = new MemoryStream(bytes);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(nome);
        mock.Setup(f => f.OpenReadStream()).Returns(stream);
        mock.Setup(f => f.Length).Returns(bytes.Length);
        return mock.Object;
    }

    private void ConfigurarSemHistoricoOuRegistros(Guid contaId)
    {
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);
    }

    // ── Preview (resumo agregado — sem lista linha a linha) ──────────────────────

    [Fact]
    public async Task PreviewAsync_ArquivoNovo_TudoContaComoNovo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());

        var csv = "Data;Descricao;Valor\n26/07/2026;Venda balcao;500,00\n26/07/2026;Posto;-50,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(2, resultado.TotalEncontradas);
        Assert.Equal(0, resultado.TotalJaImportadas);
        Assert.Equal(2, resultado.TotalNovas);
        Assert.Equal(500m, resultado.TotalEntradas);
        Assert.Equal(50m, resultado.TotalSaidas);
        Assert.Equal("2026-07-26", resultado.DataInicioArquivo);
        Assert.Equal("2026-07-26", resultado.DataFimArquivo);
    }

    [Fact]
    public async Task PreviewAsync_OfxComFitIdJaLancadoDeFato_ContaComoJaImportada()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 11),
                Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Ja importada", Valor = 50m, FitId = "2" } },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        var ofx = "<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>" +
            "<STMTTRN><TRNTYPE>DEBIT<TRNAMT>-150.00<DTPOSTED>20260710<FITID>1<MEMO>Posto Ipiranga</STMTTRN>" +
            "<STMTTRN><TRNTYPE>DEBIT<TRNAMT>-50.00<DTPOSTED>20260711<FITID>2<MEMO>Ja importada</STMTTRN>" +
            "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
        var arquivo = CriarArquivoTexto("extrato.ofx", ofx);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(2, resultado.TotalEncontradas);
        Assert.Equal(1, resultado.TotalJaImportadas);
        Assert.Equal(1, resultado.TotalNovas);
        Assert.Equal(150m, resultado.TotalSaidas); // só a nova (FITID 1) entra na soma
    }

    [Fact]
    public async Task PreviewAsync_CsvComHeuristicaBatendoNoLancamentoReal_ContaComoJaImportada()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 26),
                Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Aluguel escritorio", Valor = 300m } },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        var csv = "Data;Descricao;Valor\n26/07/2026;Aluguel escritorio;-300,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(1, resultado.TotalJaImportadas);
        Assert.Equal(0, resultado.TotalNovas);
    }

    [Fact]
    public async Task PreviewAsync_DuasTransacoesLegitimasIdenticasNoMesmoDia_NenhumaVirouDuplicataQuandoRazaoEstaVazio()
    {
        // O caso de atenção pedido explicitamente: duas transações reais idênticas (mesma data,
        // valor e descrição — ex.: duas consultas de mesmo valor) não podem virar 1 duplicata +
        // 1 perdida. Sem nada no razão ainda, as DUAS devem contar como novas.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());

        var csv = "Data;Descricao;Valor\n26/07/2026;Consulta medica;-150,00\n26/07/2026;Consulta medica;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(2, resultado.TotalEncontradas);
        Assert.Equal(0, resultado.TotalJaImportadas);
        Assert.Equal(2, resultado.TotalNovas);
        Assert.Equal(300m, resultado.TotalSaidas);
    }

    [Fact]
    public async Task PreviewAsync_DuasIdenticasNoArquivoMasSoUmaJaNoRazao_ApenasUmaViraDuplicata()
    {
        // Razão já tem 1 dessas transações (de uma importação anterior); o arquivo trazendo as
        // MESMAS 2 linhas de sempre só deve marcar 1 como duplicata — a outra é nova de verdade.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 26),
                Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Consulta medica", Valor = 150m } },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        var csv = "Data;Descricao;Valor\n26/07/2026;Consulta medica;-150,00\n26/07/2026;Consulta medica;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(2, resultado.TotalEncontradas);
        Assert.Equal(1, resultado.TotalJaImportadas);
        Assert.Equal(1, resultado.TotalNovas);
        Assert.Equal(150m, resultado.TotalSaidas);
    }

    [Fact]
    public async Task PreviewAsync_ReimportacaoDoMesmoArquivoComDuplicataLegitima_AsDuasContamComoJaImportadas()
    {
        // Reimportar o mesmo arquivo depois que as 2 transações legítimas já foram lançadas —
        // agora o razão tem as 2, então as 2 do arquivo devem ser reconhecidas como duplicata
        // (nenhuma nova entra), sem exigir nenhuma decisão manual.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 26),
                Entradas = new(),
                Saidas = new()
                {
                    new() { Id = Guid.NewGuid(), Descricao = "Consulta medica", Valor = 150m },
                    new() { Id = Guid.NewGuid(), Descricao = "Consulta medica", Valor = 150m },
                },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        var csv = "Data;Descricao;Valor\n26/07/2026;Consulta medica;-150,00\n26/07/2026;Consulta medica;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(2, resultado.TotalJaImportadas);
        Assert.Equal(0, resultado.TotalNovas);
    }

    [Fact]
    public async Task PreviewAsync_RegistroComFitIdMasExcluido_NaoContaComoJaImportada()
    {
        // Reproduz o bug relatado: um dia foi excluído (o usuário desfez o registro) depois de uma
        // importação anterior — o FITID não pode continuar bloqueando a reimportação para sempre.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 11),
                Excluido = true,
                Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Ja importada", Valor = 50m, FitId = "2" } },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        var ofx = "<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>" +
            "<STMTTRN><TRNTYPE>DEBIT<TRNAMT>-50.00<DTPOSTED>20260711<FITID>2<MEMO>Ja importada</STMTTRN>" +
            "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
        var arquivo = CriarArquivoTexto("extrato.ofx", ofx);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(0, resultado.TotalJaImportadas);
        Assert.Equal(1, resultado.TotalNovas);
    }

    [Fact]
    public async Task PreviewAsync_ComIntervaloDeDatas_SoContaTransacoesDentroDoIntervalo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());

        var csv = "Data;Descricao;Valor\n" +
            "05/07/2026;Venda dia 5;100,00\n" +
            "20/07/2026;Venda dia 20;200,00\n" +
            "28/07/2026;Venda dia 28;300,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15));

        Assert.Equal(1, resultado.TotalEncontradas);
        Assert.Equal(100m, resultado.TotalEntradas);
        // Datas do ARQUIVO INTEIRO (não do intervalo) — usadas pra pré-preencher o seletor.
        Assert.Equal("2026-07-05", resultado.DataInicioArquivo);
        Assert.Equal("2026-07-28", resultado.DataFimArquivo);
    }

    [Fact]
    public async Task PreviewAsync_ExtensaoInvalida_LancaExcecao()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var arquivo = CriarArquivoTexto("extrato.pdf", "conteudo qualquer");

        await Assert.ThrowsAsync<ApiException>(() => _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null));
    }

    [Fact]
    public async Task PreviewAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        var arquivo = CriarArquivoTexto("extrato.csv", "Data;Descricao;Valor\n26/07/2026;Venda;100,00\n");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.PreviewAsync(contaId, Guid.NewGuid(), "cliente", arquivo, null, null));

        Assert.Equal(403, ex.StatusCode);
    }

    // ── Importar (lança direto no RegistroDiario) ────────────────────────────────

    [Fact]
    public async Task ImportarArquivoAsync_EntradaESaida_AfetaSaldoNaHoraSemPassoDeConfirmacao()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, saldoInicial: 100m));
        ConfigurarSemHistoricoOuRegistros(contaId);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Venda balcao;500,00\n26/07/2026;Posto Ipiranga;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.NotNull(criado);
        Assert.Equal(450m, criado!.SaldoFinal); // 100 + 500 - 150, sem nenhuma etapa de confirmação
        Assert.Single(criado.Entradas);
        Assert.Single(criado.Saidas);
        Assert.Equal(2, resultado.TotalImportadas);
        _importRepoMock.Verify(r => r.AdicionarLoteAsync(It.Is<IEnumerable<TransacaoImportada>>(l => l.Count() == 2)), Times.Once);
    }

    // ── Cartão (Bloco 2, Money Vision): sinal invertido + fatura única por importação ───────────

    [Fact]
    public async Task ImportarArquivoAsync_ContaCartaoCredito_InverteSinalDasTransacoes()
    {
        // Numa fatura de cartão, valor positivo = compra (dívida), negativo = estorno/crédito —
        // o contrário da convenção de conta corrente que os parsers assumem por padrão.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        ConfigurarSemHistoricoOuRegistros(contaId);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Compra Mercado;150,00\n26/07/2026;Estorno Loja;-30,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null,
            banco: "Nubank", dataVencimentoFatura: new DateOnly(2026, 8, 10));

        var compra = Assert.Single(criado!.Saidas);
        Assert.Equal(150m, compra.Valor);
        var estorno = Assert.Single(criado.Entradas);
        Assert.Equal(30m, estorno.Valor);
        Assert.Equal(-120m, criado.SaldoFinal); // dívida: -150 (compra) + 30 (estorno)
    }

    // Item 7 (Bloco 5, opcional): compra parcelada no próprio extrato ("<comerciante> - Parcela
    // N/M") grava NumeroParcela/TotalParcelas — base pra Projeção/Meu Futuro projetarem as
    // parcelas futuras (ver FaturaCartaoService.CalcularParcelasFuturasProjetadas).
    [Fact]
    public async Task ImportarArquivoAsync_ContaCartaoCredito_CompraParcelada_GravaNumeroETotalDeParcelas()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        ConfigurarSemHistoricoOuRegistros(contaId);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Trocafone - Parcela 1/10;100,00\n26/07/2026;Apple.Com/Bill;30,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null,
            banco: "Nubank", dataVencimentoFatura: new DateOnly(2026, 8, 10));

        var parcelada = criado!.Saidas.Single(s => s.Descricao == "Trocafone - Parcela 1/10");
        Assert.Equal(1, parcelada.NumeroParcela);
        Assert.Equal(10, parcelada.TotalParcelas);

        var naoParcelada = criado.Saidas.Single(s => s.Descricao == "Apple.Com/Bill");
        Assert.Null(naoParcelada.NumeroParcela);
        Assert.Null(naoParcelada.TotalParcelas);
    }

    // Rodada de ajustes "parcelamentos": a compra parcelada detectada aciona a resolução/criação do
    // Parcelamento (ver ParcelamentoDeteccaoService) e grava o Id dele na parcela real importada.
    [Fact]
    public async Task ImportarArquivoAsync_ContaCartaoCredito_CompraParcelada_ResolveParcelamentoEGravaOIdNaParcela()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        ConfigurarSemHistoricoOuRegistros(contaId);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var parcelamentoId = Guid.NewGuid();
        _parcelamentoDeteccaoServiceMock.Setup(s => s.ProcessarParcelaDetectadaAsync(
            It.IsAny<ContaBancaria>(), new DateOnly(2026, 8, 10), "Trocafone", It.IsAny<string?>(), 1, 10, 100m))
            .ReturnsAsync(new Parcelamento { Id = parcelamentoId, Status = "Ativo" });

        var csv = "Data;Descricao;Valor\n26/07/2026;Trocafone - Parcela 1/10;100,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null,
            banco: "Nubank", dataVencimentoFatura: new DateOnly(2026, 8, 10));

        var parcelada = Assert.Single(criado!.Saidas);
        Assert.Equal(parcelamentoId, parcelada.ParcelamentoId);
        _parcelamentoDeteccaoServiceMock.Verify(s => s.ProcessarParcelaDetectadaAsync(
            It.IsAny<ContaBancaria>(), new DateOnly(2026, 8, 10), "Trocafone", It.IsAny<string?>(), 1, 10, 100m), Times.Once);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ContaCartaoCredito_CompraParcelada_ResumoListaOParcelamentoDetectado()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        ConfigurarSemHistoricoOuRegistros(contaId);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var parcelamentoId = Guid.NewGuid();
        _parcelamentoDeteccaoServiceMock.Setup(s => s.ProcessarParcelaDetectadaAsync(
            It.IsAny<ContaBancaria>(), It.IsAny<DateOnly>(), "Trocafone", It.IsAny<string?>(), 1, 10, 100m))
            .ReturnsAsync(new Parcelamento { Id = parcelamentoId, Status = "Ativo" });

        var csv = "Data;Descricao;Valor\n26/07/2026;Trocafone - Parcela 1/10;100,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null,
            banco: "Nubank", dataVencimentoFatura: new DateOnly(2026, 8, 10));

        var resumo = Assert.Single(resultado.ParcelamentosDetectados);
        Assert.Equal(parcelamentoId, resumo.ParcelamentoId);
        Assert.Equal("Trocafone", resumo.Comerciante);
        Assert.Equal(1, resumo.NumeroParcela);
        Assert.Equal(10, resumo.TotalParcelas);
        Assert.Equal(9, resumo.ParcelasRestantes);
        Assert.Equal(900m, resumo.ValorRestante);
        Assert.Equal("Ativo", resumo.Status);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ContaCartaoCredito_SemBanco_LancaDadosInvalidos()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        ConfigurarSemHistoricoOuRegistros(contaId);
        var arquivo = CriarArquivoTexto("fatura.csv", "Data;Descricao;Valor\n26/07/2026;Compra;100,00\n");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ImportarArquivoAsync(
            contaId, clienteId, "cliente", arquivo, null, null, dataVencimentoFatura: new DateOnly(2026, 8, 10)));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("banco", ex.Campo);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ContaCartaoCredito_SemVencimento_LancaDadosInvalidos()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        ConfigurarSemHistoricoOuRegistros(contaId);
        var arquivo = CriarArquivoTexto("fatura.csv", "Data;Descricao;Valor\n26/07/2026;Compra;100,00\n");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ImportarArquivoAsync(
            contaId, clienteId, "cliente", arquivo, null, null, banco: "Nubank"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("dataVencimentoFatura", ex.Campo);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ContaCartaoCredito_CriaUmaFaturaELigaTodasAsCompras()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        ConfigurarSemHistoricoOuRegistros(contaId);

        Fatura? faturaCriada = null;
        _faturaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Fatura>()))
            .Callback<Fatura>(f => faturaCriada = f).ReturnsAsync((Fatura f) => f);

        var registrosCriados = new List<RegistroDiario>();
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => registrosCriados.Add(r)).ReturnsAsync((RegistroDiario r) => r);

        // Duas datas diferentes — mesmo arquivo, mesma fatura (nunca mais particionada por data).
        var csv = "Data;Descricao;Valor\n26/07/2026;Compra A;100,00\n28/08/2026;Compra B;50,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null,
            banco: "Nubank", dataVencimentoFatura: new DateOnly(2026, 9, 10), dataFechamentoFatura: new DateOnly(2026, 9, 1));

        Assert.NotNull(faturaCriada);
        Assert.Equal("Nubank", faturaCriada!.Banco);
        Assert.Equal("2026-09", faturaCriada.Competencia);
        Assert.Equal(new DateOnly(2026, 9, 10), faturaCriada.DataVencimento);
        Assert.Equal(new DateOnly(2026, 9, 1), faturaCriada.DataFechamento);
        Assert.Equal(faturaCriada.Id, resultado.FaturaId);

        var todasAsCompras = registrosCriados.SelectMany(r => r.Saidas).ToList();
        Assert.Equal(2, todasAsCompras.Count);
        Assert.All(todasAsCompras, c => Assert.Equal(faturaCriada.Id, c.FaturaId));
    }

    [Fact]
    public async Task PreviewAsync_ContaCartaoCredito_InverteSinalNosTotais()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());

        var csv = "Data;Descricao;Valor\n26/07/2026;Compra;150,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(150m, resultado.TotalSaidas);
        Assert.Equal(0m, resultado.TotalEntradas);
        Assert.True(resultado.SinalInvertidoParaCartao);
    }

    [Fact]
    public async Task PreviewAsync_ContaCartaoCredito_ArquivoJaUsaNegativoParaCompra_NaoInverte()
    {
        // Bug reportado: a inversão antiga era cega (sempre inverte) — bancos cujo arquivo já
        // exporta compra como negativo (convenção igual a conta corrente) tinham o sinal virado
        // errado. A maioria das linhas sem palavra-chave de pagamento/estorno já leu como Saída
        // (negativo) na heurística genérica, então não deve inverter nada.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());

        var csv = "Data;Descricao;Valor\n26/07/2026;Compra A;-100,00\n26/07/2026;Compra B;-50,00\n26/07/2026;Compra C;30,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(150m, resultado.TotalSaidas); // A + B, compras, continuam saída
        Assert.Equal(30m, resultado.TotalEntradas); // C, já era a minoria positiva
        Assert.False(resultado.SinalInvertidoParaCartao);
    }

    [Fact]
    public async Task PreviewAsync_ContaCartaoCredito_LinhaDePagamentoOuEstorno_SempreEntradaIndependenteDaMaioria()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());

        // Maioria das compras (sem palavra-chave) é positiva → heurística decide inverter. A linha
        // de pagamento vem negativa no arquivo, mas tem que entrar como Entrada de qualquer jeito.
        var csv = "Data;Descricao;Valor\n26/07/2026;Compra Mercado;100,00\n26/07/2026;Compra Padaria;50,00\n26/07/2026;Pagamento recebido;-150,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(150m, resultado.TotalSaidas); // Mercado + Padaria, invertidas pra saída
        Assert.Equal(150m, resultado.TotalEntradas); // Pagamento recebido, forçada entrada
    }

    [Fact]
    public async Task PreviewAsync_ContaCartaoCredito_InverterSinaisManualmente_ForcaOSentido()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());

        // Maioria já é negativa (a detecção automática não inverteria), mas o usuário força inverter.
        var csv = "Data;Descricao;Valor\n26/07/2026;Compra A;-100,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null, inverterSinaisManualmente: true);

        Assert.Equal(0m, resultado.TotalSaidas);
        Assert.Equal(100m, resultado.TotalEntradas);
        Assert.True(resultado.SinalInvertidoParaCartao);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ContaCartaoCredito_InverterSinaisManualmente_ForcaOSentido()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, tipo: "CartaoCredito"));
        ConfigurarSemHistoricoOuRegistros(contaId);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Compra A;-100,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null,
            banco: "Nubank", dataVencimentoFatura: new DateOnly(2026, 8, 10), inverterSinaisManualmente: true);

        var entrada = Assert.Single(criado!.Entradas);
        Assert.Equal(100m, entrada.Valor);
        Assert.Empty(criado.Saidas);
    }

    [Fact]
    public async Task ImportarArquivoAsync_SaidaComPalavraChaveDoDicionario_EntraComoSugestaoAindaPendente()
    {
        // Item 3.4: o dicionário padrão (e o histórico do cliente) só SUGEREM — diferente de uma
        // regra do cliente, que confirma sozinha — por isso o item continua pendente.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemHistoricoOuRegistros(contaId);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Posto Ipiranga Combustivel;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        var saida = Assert.Single(criado!.Saidas);
        Assert.Equal("Manutenção", saida.Categoria);
        // Reproduz o Problema 3: a categoria sugerida precisa vir com o TipoCusto do Plano de
        // Contas, senão o DRE nunca soma esse lançamento como custo fixo/variável.
        Assert.Equal("CustoFixo", saida.TipoCusto);
        Assert.True(saida.PendenteCategorizacao);
        Assert.True(saida.CategoriaSugerida);
        Assert.Equal(1, resultado.TotalPendentesCategorizacao);
    }

    // Money Vision — Bloco 4, item 2 da Parte 2: "Caixinha"/"Porquinho"/"Guardar dinheiro"/
    // "Resgate" sugerem Categoria "Transferência" assim como "Aplicação RDB/CDB" já fazia.
    [Theory]
    [InlineData("Transferência para Caixinha Nubank")]
    [InlineData("Porquinho PicPay")]
    [InlineData("Guardar dinheiro - Reserva")]
    [InlineData("Resgate de aplicação")]
    public async Task ImportarArquivoAsync_SaidaComPalavraChaveDeTransferencia_SugereCategoriaTransferencia(string descricao)
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemHistoricoOuRegistros(contaId);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = $"Data;Descricao;Valor\n26/07/2026;{descricao};-300,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        var saida = Assert.Single(criado!.Saidas);
        Assert.Equal("Transferência", saida.Categoria);
        Assert.True(saida.PendenteCategorizacao);
        Assert.True(saida.CategoriaSugerida);
    }

    [Fact]
    public async Task ImportarArquivoAsync_SaidaComRegraDoCliente_ConfirmaSozinhaSemSugestao()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemHistoricoOuRegistros(contaId);
        _regraRepoMock.Setup(r => r.ListarAtivasPorContaAsync(contaId)).ReturnsAsync(new List<RegraCategorizacao>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Tipo = "Saida",
                CriterioTipo = "DescricaoExata", CriterioValor = "ALUGUEL ESCRITORIO", AcaoTipo = "Categoria",
                Categoria = "Aluguel", Ativa = true, Ordem = 0,
            },
        });

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Aluguel Escritorio;-1200,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        var saida = Assert.Single(criado!.Saidas);
        Assert.Equal("Aluguel", saida.Categoria);
        Assert.False(saida.PendenteCategorizacao);
        Assert.False(saida.CategoriaSugerida);
        Assert.Equal(0, resultado.TotalPendentesCategorizacao);
        Assert.Equal(1, resultado.TotalCategorizadasPorRegra);
    }

    [Fact]
    public async Task ImportarArquivoAsync_SaidaComHistoricoDoClienteConfirmadoDuasVezes_SugereAMesmaCategoria()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        // Duas ocorrências já confirmadas (PendenteCategorizacao=false) com a mesma categoria pra
        // essa descrição — sinal forte o bastante pra sugerir de novo na próxima importação.
        var registrosExistentes = new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 6, 1),
                Entradas = new(), Saidas = new()
                {
                    new() { Id = Guid.NewGuid(), Descricao = "Uber Viagem 123", Valor = 40m, Categoria = "Frete e entrega", PendenteCategorizacao = false },
                },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0,
            },
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 6, 15),
                Entradas = new(), Saidas = new()
                {
                    new() { Id = Guid.NewGuid(), Descricao = "Uber Viagem 456", Valor = 35m, Categoria = "Frete e entrega", PendenteCategorizacao = false },
                },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0,
            },
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(registrosExistentes);
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Uber Viagem 789;-42,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        var saida = Assert.Single(criado!.Saidas);
        Assert.Equal("Frete e entrega", saida.Categoria);
        Assert.True(saida.CategoriaSugerida);
        Assert.True(saida.PendenteCategorizacao);
        Assert.Equal(1, resultado.TotalPendentesCategorizacao);
    }

    [Fact]
    public async Task ImportarArquivoAsync_SaidaSemSugestao_EntraPendenteDeCategorizacaoMasAfetaSaldo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, saldoInicial: 1000m));
        ConfigurarSemHistoricoOuRegistros(contaId);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Pagamento diverso XYZ;-80,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        var saida = Assert.Single(criado!.Saidas);
        Assert.True(saida.PendenteCategorizacao);
        Assert.Equal(920m, criado.SaldoFinal); // o saldo já reflete a saída mesmo sem categoria
        Assert.Equal(1, resultado.TotalPendentesCategorizacao);
    }

    [Fact]
    public async Task ImportarArquivoAsync_EntradaSemCategoria_FicaPendente()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemHistoricoOuRegistros(contaId);

        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Pix recebido de cliente;300,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        var entrada = Assert.Single(criado!.Entradas);
        Assert.True(entrada.PendenteCategorizacao);
        Assert.Equal(1, resultado.TotalPendentesCategorizacao);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ComIntervaloDeDatas_SoImportaTransacoesDentroDoIntervalo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemHistoricoOuRegistros(contaId);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n" +
            "05/07/2026;Venda dia 5;100,00\n" +
            "20/07/2026;Venda dia 20;200,00\n" +
            "28/07/2026;Venda dia 28;300,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(
            contaId, clienteId, "cliente", arquivo,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15));

        Assert.Equal(1, resultado.TotalImportadas);
        Assert.Equal(100m, resultado.TotalEntradas);
    }

    [Fact]
    public async Task ImportarArquivoAsync_CenarioSobreposto_SegundaImportacaoSoTrazTransacoesNovas()
    {
        // Simula "importar 1-15, depois importar 1-30" via FITID: a segunda leva do mesmo arquivo
        // (mesmos FITIDs 1 e 2 de antes, mais o novo FITID 3) só deve trazer o FITID 3.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 5),
                Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Posto Ipiranga", Valor = 150m, FitId = "1" } },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 10),
                Entradas = new() { new() { Id = Guid.NewGuid(), Descricao = "Venda", Valor = 200m, FitId = "2" } }, Saidas = new(),
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var ofxCompleto = "<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>" +
            "<STMTTRN><TRNTYPE>DEBIT<TRNAMT>-150.00<DTPOSTED>20260705<FITID>1<MEMO>Posto Ipiranga</STMTTRN>" +
            "<STMTTRN><TRNTYPE>CREDIT<TRNAMT>200.00<DTPOSTED>20260710<FITID>2<MEMO>Venda</STMTTRN>" +
            "<STMTTRN><TRNTYPE>CREDIT<TRNAMT>300.00<DTPOSTED>20260722<FITID>3<MEMO>Venda nova</STMTTRN>" +
            "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
        var arquivo = CriarArquivoTexto("extrato.ofx", ofxCompleto);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(1, resultado.TotalImportadas);
        Assert.Equal(300m, resultado.TotalEntradas);
    }

    [Fact]
    public async Task ImportarArquivoAsync_TransacaoJaImportadaPorFitId_EIgnoradaSemPerguntarNada()
    {
        // Substitui o antigo "forçar inclusão" (checkbox por linha, removido): a deduplicação
        // agora é inteiramente automática — o duplicado é descartado, sem intervenção do usuário.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 5),
                Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Posto Ipiranga", Valor = 150m, FitId = "1" } },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        var ofx = "<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>" +
            "<STMTTRN><TRNTYPE>DEBIT<TRNAMT>-150.00<DTPOSTED>20260705<FITID>1<MEMO>Posto Ipiranga</STMTTRN>" +
            "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
        var arquivo = CriarArquivoTexto("extrato.ofx", ofx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null));

        Assert.Equal(400, ex.StatusCode);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task ImportarArquivoAsync_DuasIdenticasNoArquivoESoUmaNoRazao_ImportaSoAExcedente()
    {
        // Mesmo cenário de atenção do preview, agora na confirmação real: das 2 transações
        // idênticas do arquivo, 1 já existe no razão — só a excedente deve ser lançada.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 26),
                Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Consulta medica", Valor = 150m } },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        RegistroDiario? atualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Consulta medica;-150,00\n26/07/2026;Consulta medica;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(1, resultado.TotalImportadas);
        Assert.Equal(2, atualizado!.Saidas.Count); // a que já existia + a excedente recém-lançada
    }

    [Fact]
    public async Task ImportarArquivoAsync_DiaComRegistroExistente_AcrescentaAoInvesDeDuplicarDia()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 26);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);

        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data, Inicio = 500m,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 500m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroExistente });

        RegistroDiario? atualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizado = r).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/07/2026;Venda extra;100,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.NotNull(atualizado);
        Assert.Equal(600m, atualizado!.SaldoFinal);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task ImportarArquivoAsync_TodasJaImportadasENaoForcadas_LancaDadosInvalidos()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 26),
                Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Aluguel escritorio", Valor = 300m } },
                ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        var csv = "Data;Descricao;Valor\n26/07/2026;Aluguel escritorio;-300,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ArquivoXlsx_ParseiaTransacoesCorretamente()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemHistoricoOuRegistros(contaId);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Extrato");
        ws.Cell(1, 1).Value = "Data";
        ws.Cell(1, 2).Value = "Descricao";
        ws.Cell(1, 3).Value = "Valor";
        ws.Cell(2, 1).Value = new DateTime(2026, 7, 20);
        ws.Cell(2, 2).Value = "Recebimento Cliente";
        ws.Cell(2, 3).Value = 500;

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns("extrato.xlsx");
        mock.Setup(f => f.OpenReadStream()).Returns(ms);
        mock.Setup(f => f.Length).Returns(ms.Length);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", mock.Object, null, null);

        Assert.Equal(1, resultado.TotalImportadas);
        Assert.Equal(500m, resultado.TotalEntradas);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ExtensaoInvalida_LancaExcecao()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var arquivo = CriarArquivoTexto("extrato.pdf", "conteudo qualquer");

        await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null));
    }

    [Fact]
    public async Task ImportarArquivoAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        var arquivo = CriarArquivoTexto("extrato.csv", "Data;Descricao;Valor\n");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ImportarArquivoAsync(contaId, Guid.NewGuid(), "cliente", arquivo, null, null));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ComContaInexistente_LancaNaoEncontrado()
    {
        var contaId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync((ContaBancaria?)null);
        var arquivo = CriarArquivoTexto("extrato.csv", "Data;Descricao;Valor\n");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ImportarArquivoAsync(contaId, Guid.NewGuid(), "admin", arquivo, null, null));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ImportarArquivoAsync_CsvSemTransacoes_LancaDadosInvalidos()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        var arquivo = CriarArquivoTexto("extrato.csv", "Data;Descricao;Valor\n");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null));

        Assert.Equal(400, ex.StatusCode);
    }

    // ── Categorização pendente ────────────────────────────────────────────────

    [Fact]
    public async Task ListarPendentesCategorizacaoAsync_RetornaEntradasEQuandoPendentes()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var idEntradaPendente = Guid.NewGuid();
        var idSaidaPendente = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 20),
            Entradas = new()
            {
                new() { Id = idEntradaPendente, Descricao = "Pix recebido de cliente", Valor = 300m, PendenteCategorizacao = true },
                new() { Id = Guid.NewGuid(), Descricao = "Venda", Valor = 100m, Categoria = "Vendas", PendenteCategorizacao = false },
            },
            Saidas = new()
            {
                new() { Id = idSaidaPendente, Descricao = "Pagamento diverso", Valor = 80m, Categoria = "", PendenteCategorizacao = true },
                new() { Id = Guid.NewGuid(), Descricao = "Aluguel", Valor = 300m, Categoria = "Aluguel", PendenteCategorizacao = false },
            },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var pendentes = await _sut.ListarPendentesCategorizacaoAsync(contaId, clienteId, "cliente");

        Assert.Equal(2, pendentes.Count);
        var entrada = Assert.Single(pendentes, p => p.Id == idEntradaPendente);
        Assert.Equal("Entrada", entrada.Tipo);
        Assert.Equal("Pix recebido de cliente", entrada.Descricao);
        var saida = Assert.Single(pendentes, p => p.Id == idSaidaPendente);
        Assert.Equal("Saida", saida.Tipo);
        Assert.Equal("Pagamento diverso", saida.Descricao);
    }

    [Fact]
    public async Task ListarPendentesCategorizacaoAsync_SaidaComValorEDataCompativelEmOutraContaAtiva_SugereTransferencia()
    {
        // Item 3.5: saída pendente de 500 na conta A + entrada de 500 um dia depois na conta B do
        // MESMO cliente — único par compatível, então o backend sugere a transferência sozinho.
        var clienteId = Guid.NewGuid();
        var contaAId = Guid.NewGuid();
        var contaBId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaAId)).ReturnsAsync(CriarConta(contaAId, clienteId));
        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            CriarConta(contaAId, clienteId),
            new() { Id = contaBId, ClienteId = clienteId, Nome = "Conta Investimento", Tipo = "Investimento", Ativa = true },
        });

        var idSaidaPendente = Guid.NewGuid();
        var idEntradaContaB = Guid.NewGuid();
        var registroContaA = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaAId, Data = new DateOnly(2026, 8, 1),
            Entradas = new(),
            Saidas = new() { new() { Id = idSaidaPendente, Descricao = "Aplicacao RDB", Valor = 500m, PendenteCategorizacao = true } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
        };
        var registroContaB = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaBId, Data = new DateOnly(2026, 8, 2),
            Entradas = new() { new() { Id = idEntradaContaB, Descricao = "Aplicacao recebida", Valor = 500m, PendenteCategorizacao = true } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaAId)).ReturnsAsync(new List<RegistroDiario> { registroContaA });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroContaA, registroContaB });

        var pendentes = await _sut.ListarPendentesCategorizacaoAsync(contaAId, clienteId, "cliente");

        var saida = Assert.Single(pendentes, p => p.Id == idSaidaPendente);
        Assert.Equal(contaBId, saida.SugestaoTransferenciaContaId);
        Assert.Equal("Conta Investimento", saida.SugestaoTransferenciaContaNome);
        Assert.Equal(idEntradaContaB, saida.SugestaoTransferenciaLancamentoId);
        Assert.Equal("2026-08-02", saida.SugestaoTransferenciaData);
    }

    [Fact]
    public async Task ListarPendentesCategorizacaoAsync_DuasCandidatasNaOutraConta_NaoSugereNadaAmbiguo()
    {
        var clienteId = Guid.NewGuid();
        var contaAId = Guid.NewGuid();
        var contaBId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaAId)).ReturnsAsync(CriarConta(contaAId, clienteId));
        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            CriarConta(contaAId, clienteId),
            new() { Id = contaBId, ClienteId = clienteId, Nome = "Conta Investimento", Tipo = "Investimento", Ativa = true },
        });

        var idSaidaPendente = Guid.NewGuid();
        var registroContaA = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaAId, Data = new DateOnly(2026, 8, 1),
            Entradas = new(),
            Saidas = new() { new() { Id = idSaidaPendente, Descricao = "Aplicacao RDB", Valor = 500m, PendenteCategorizacao = true } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
        };
        // Duas entradas de 500 na mesma janela — ambíguo demais pra decidir sozinho.
        var registroContaB = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaBId, Data = new DateOnly(2026, 8, 2),
            Entradas = new()
            {
                new() { Id = Guid.NewGuid(), Descricao = "Aplicacao 1", Valor = 500m, PendenteCategorizacao = true },
                new() { Id = Guid.NewGuid(), Descricao = "Aplicacao 2", Valor = 500m, PendenteCategorizacao = true },
            },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaAId)).ReturnsAsync(new List<RegistroDiario> { registroContaA });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroContaA, registroContaB });

        var pendentes = await _sut.ListarPendentesCategorizacaoAsync(contaAId, clienteId, "cliente");

        var saida = Assert.Single(pendentes, p => p.Id == idSaidaPendente);
        Assert.Null(saida.SugestaoTransferenciaContaId);
    }

    [Fact]
    public async Task ListarPendentesCategorizacaoAsync_CandidataJaVinculadaAOutraTransferencia_NaoSugereDeNovo()
    {
        var clienteId = Guid.NewGuid();
        var contaAId = Guid.NewGuid();
        var contaBId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaAId)).ReturnsAsync(CriarConta(contaAId, clienteId));
        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            CriarConta(contaAId, clienteId),
            new() { Id = contaBId, ClienteId = clienteId, Nome = "Conta Investimento", Tipo = "Investimento", Ativa = true },
        });

        var idSaidaPendente = Guid.NewGuid();
        var registroContaA = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaAId, Data = new DateOnly(2026, 8, 1),
            Entradas = new(),
            Saidas = new() { new() { Id = idSaidaPendente, Descricao = "Aplicacao RDB", Valor = 500m, PendenteCategorizacao = true } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
        };
        var registroContaB = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaBId, Data = new DateOnly(2026, 8, 2),
            Entradas = new() { new() { Id = Guid.NewGuid(), Descricao = "Aplicacao recebida", Valor = 500m, TransferenciaId = Guid.NewGuid() } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaAId)).ReturnsAsync(new List<RegistroDiario> { registroContaA });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroContaA, registroContaB });

        var pendentes = await _sut.ListarPendentesCategorizacaoAsync(contaAId, clienteId, "cliente");

        var saida = Assert.Single(pendentes, p => p.Id == idSaidaPendente);
        Assert.Null(saida.SugestaoTransferenciaContaId);
    }

    // ── Atualizar categoria ───────────────────────────────────────────────────

    [Fact]
    public async Task AtualizarCategoriasAsync_ItemExistente_AtualizaCategoriaELimpaPendencia()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var itemId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(),
            Saidas = new() { new() { Id = itemId, Descricao = "Pagamento diverso", Valor = 80m, Categoria = "", PendenteCategorizacao = true } },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);

        RegistroDiario? atualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizado = r).ReturnsAsync((RegistroDiario r) => r);

        var dto = new AtualizarCategoriaDto
        {
            Itens = new() { new() { Id = itemId, Data = "2026-07-20", Categoria = "Material de Escritório" } },
        };

        await _sut.AtualizarCategoriasAsync(contaId, clienteId, "cliente", dto);

        Assert.NotNull(atualizado);
        var saida = Assert.Single(atualizado!.Saidas);
        Assert.Equal("Material de Escritório", saida.Categoria);
        Assert.Equal("CustoFixo", saida.TipoCusto);
        Assert.False(saida.PendenteCategorizacao);
    }

    [Fact]
    public async Task AtualizarCategoriasAsync_CategoriaRecemCriada_AplicaTipoCustoEscolhidoNaCriacao()
    {
        // Reproduz o Problema 3 relatado: usuário cria a categoria "Frete" como Custo Variável
        // durante a categorização — o lançamento precisa nascer com esse TipoCusto, não com o
        // nome certo mas a classificação "perdida" (TipoCusto nulo, fora do DRE por custo).
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 8, 20);
        var itemId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _categoriaRepoMock.Setup(r => r.ListarTodasPorClienteAsync(clienteId)).ReturnsAsync(new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Frete", Tipo = "CustoVariavel" },
        });

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(),
            Saidas = new() { new() { Id = itemId, Descricao = "Motoboy", Valor = 40m, Categoria = "", PendenteCategorizacao = true } },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);

        RegistroDiario? atualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizado = r).ReturnsAsync((RegistroDiario r) => r);

        var dto = new AtualizarCategoriaDto
        {
            Itens = new() { new() { Id = itemId, Data = "2026-08-20", Categoria = "Frete" } },
        };

        await _sut.AtualizarCategoriasAsync(contaId, clienteId, "cliente", dto);

        var saida = Assert.Single(atualizado!.Saidas);
        Assert.Equal("Frete", saida.Categoria);
        Assert.Equal("CustoVariavel", saida.TipoCusto);
    }

    [Fact]
    public async Task AtualizarCategoriasAsync_ItemEntrada_AtualizaCategoriaELimpaPendencia()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var itemId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _categoriaRepoMock.Setup(r => r.ListarTodasPorClienteAsync(clienteId)).ReturnsAsync(new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Vendas", Tipo = "Receita" },
        });

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new() { new() { Id = itemId, Descricao = "Pix recebido de cliente", Valor = 300m, PendenteCategorizacao = true } },
            Saidas = new(),
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);

        RegistroDiario? atualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizado = r).ReturnsAsync((RegistroDiario r) => r);

        var dto = new AtualizarCategoriaDto
        {
            Itens = new() { new() { Id = itemId, Data = "2026-07-20", Categoria = "Vendas" } },
        };

        await _sut.AtualizarCategoriasAsync(contaId, clienteId, "cliente", dto);

        var entrada = Assert.Single(atualizado!.Entradas);
        Assert.Equal("Vendas", entrada.Categoria);
        Assert.Equal("Receita", entrada.TipoCusto);
        Assert.False(entrada.PendenteCategorizacao);
    }

    [Fact]
    public async Task AtualizarCategoriasAsync_DiaSemRegistro_NaoLancaExcecao()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, It.IsAny<DateOnly>())).ReturnsAsync((RegistroDiario?)null);

        var dto = new AtualizarCategoriaDto
        {
            Itens = new() { new() { Id = Guid.NewGuid(), Data = "2026-07-20", Categoria = "Aluguel" } },
        };

        await _sut.AtualizarCategoriasAsync(contaId, clienteId, "cliente", dto);

        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    // ── ExcluirLancamentoAsync ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExcluirLancamentoAsync_SaidaSimples_RemoveERestituiSaldo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var itemId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(),
            Saidas = new() { new() { Id = itemId, Descricao = "Posto", Valor = 150m, Categoria = "Manutenção" } },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 850m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        RegistroDiario? atualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizado = r).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.ExcluirLancamentoAsync(contaId, clienteId, "cliente",
            new ExcluirLancamentoDto { Id = itemId, Data = "2026-07-20" });

        Assert.Empty(atualizado!.Saidas);
        Assert.Equal(1000m, atualizado.SaldoFinal);
        Assert.False(resultado.TransferenciaExcluida);
        Assert.Null(resultado.TituloReaberto);
    }

    [Fact]
    public async Task ExcluirLancamentoAsync_Entrada_RemoveEReduzSaldo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var itemId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new() { new() { Id = itemId, Descricao = "Venda balcão", Valor = 300m } },
            Saidas = new(),
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 1300m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        RegistroDiario? atualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizado = r).ReturnsAsync((RegistroDiario r) => r);

        await _sut.ExcluirLancamentoAsync(contaId, clienteId, "cliente",
            new ExcluirLancamentoDto { Id = itemId, Data = "2026-07-20" });

        Assert.Empty(atualizado!.Entradas);
        Assert.Equal(1000m, atualizado.SaldoFinal);
    }

    [Fact]
    public async Task ExcluirLancamentoAsync_LancamentoNaoEncontrado_LancaExcecao()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirLancamentoAsync(contaId, clienteId, "cliente",
            new ExcluirLancamentoDto { Id = Guid.NewGuid(), Data = "2026-07-20" }));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ExcluirLancamentoAsync_PontaDeTransferencia_DelegaParaTransferenciaServiceEExcluiAmbas()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var itemId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(),
            Saidas = new() { new() { Id = itemId, Descricao = "Transferência enviada", Valor = 500m, TransferenciaId = transferenciaId, Categoria = "Transferência" } },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 500m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);
        _transferenciaServiceMock.Setup(t => t.ExcluirAsync(transferenciaId, clienteId, "cliente")).Returns(Task.CompletedTask);

        var resultado = await _sut.ExcluirLancamentoAsync(contaId, clienteId, "cliente",
            new ExcluirLancamentoDto { Id = itemId, Data = "2026-07-20" });

        Assert.True(resultado.TransferenciaExcluida);
        _transferenciaServiceMock.Verify(t => t.ExcluirAsync(transferenciaId, clienteId, "cliente"), Times.Once);
        // A remoção física das pontas é responsabilidade do TransferenciaService, não deste método.
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task ExcluirLancamentoAsync_VinculadoABaixaDeTitulo_ReabreTitulo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var itemId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new() { new() { Id = itemId, Descricao = "Pix recebido de Cliente X", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new() { new() { Descricao = "Fatura Cliente X", Valor = 500m, Pago = true, DataBaixa = data, ContaBancariaId = contaId, LancamentoVinculadoId = itemId } },
            ContasPagar = new(),
            SaldoFinal = 1500m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var atualizados = new List<RegistroDiario>();
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizados.Add(r)).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.ExcluirLancamentoAsync(contaId, clienteId, "cliente",
            new ExcluirLancamentoDto { Id = itemId, Data = "2026-07-20" });

        Assert.Equal("Fatura Cliente X", resultado.TituloReaberto);
        var titulo = Assert.Single(atualizados.Last().ContasReceber);
        Assert.False(titulo.Pago);
        Assert.Null(titulo.DataBaixa);
        Assert.Null(titulo.LancamentoVinculadoId);
        Assert.Empty(atualizados.Last().Entradas);
    }

    [Fact]
    public async Task ExcluirLancamentoAsync_ItemImportadoComFitId_MarcaTransacaoImportadaComoIgnorada()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var itemId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(),
            Saidas = new() { new() { Id = itemId, Descricao = "Posto", Valor = 150m, FitId = "FIT-123" } },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 850m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var transacaoOriginal = new TransacaoImportada
        {
            Id = Guid.NewGuid(), ContaBancariaId = contaId, ClienteId = clienteId, Data = data,
            Valor = 150m, Descricao = "Posto", FitId = "FIT-123", Tipo = "Saida", Status = "Confirmada",
        };
        _importRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<TransacaoImportada> { transacaoOriginal });

        TransacaoImportada? atualizada = null;
        _importRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<TransacaoImportada>()))
            .Callback<TransacaoImportada>(t => atualizada = t).Returns(Task.CompletedTask);

        await _sut.ExcluirLancamentoAsync(contaId, clienteId, "cliente",
            new ExcluirLancamentoDto { Id = itemId, Data = "2026-07-20" });

        Assert.NotNull(atualizada);
        Assert.Equal("Ignorada", atualizada!.Status);
    }

    // ── ExcluirLoteAsync: "excluir em massa" (rodada de ajustes Money Vision) ────────────────────

    [Fact]
    public async Task ExcluirLoteAsync_DoisItensEmDatasDiferentes_RemoveOsDoisERecalculaUmaVezSoAPartirDaMaisAntiga()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var dataAntiga = new DateOnly(2026, 7, 20);
        var dataNova = new DateOnly(2026, 7, 25);
        var itemAntigoId = Guid.NewGuid();
        var itemNovoId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        // Dados internamente consistentes (Inicio do dia seguinte = SaldoFinal do dia anterior) —
        // é essa consistência que deixa claro o que a cascata de recálculo realmente faz: excluir o
        // item de 20/07 libera +150 que se propaga pro Inicio/SaldoFinal de 25/07 também, além do
        // próprio ajuste direto de 25/07 (-300, pela exclusão do item de lá).
        var registroAntigo = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dataAntiga, Inicio = 1000m,
            Entradas = new(), Saidas = new() { new() { Id = itemAntigoId, Descricao = "Posto", Valor = 150m } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 850m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        var registroNovo = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dataNova, Inicio = 850m,
            Entradas = new() { new() { Id = itemNovoId, Descricao = "Venda", Valor = 300m } }, Saidas = new(),
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 1150m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        var todosOsRegistros = new List<RegistroDiario> { registroAntigo, registroNovo };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, dataAntiga)).ReturnsAsync(registroAntigo);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, dataNova)).ReturnsAsync(registroNovo);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(todosOsRegistros);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(todosOsRegistros);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.ExcluirLoteAsync(contaId, clienteId, "cliente", new ExcluirLoteDto
        {
            Itens = new()
            {
                new() { Id = itemAntigoId, Data = "2026-07-20" },
                new() { Id = itemNovoId, Data = "2026-07-25" },
            },
        });

        Assert.Equal(2, resultado.TotalExcluidos);
        Assert.Equal(0, resultado.TransferenciasExcluidas);
        Assert.Empty(registroAntigo.Saidas);
        Assert.Empty(registroNovo.Entradas);
        Assert.Equal(1000m, registroAntigo.SaldoFinal); // 850 + 150 restituído
        // 1150 (original) - 300 (removido direto) + 150 (cascata propagada do dia anterior) = 1000.
        Assert.Equal(1000m, registroNovo.Inicio);
        Assert.Equal(1000m, registroNovo.SaldoFinal);
        Assert.Equal(new List<Guid> { contaId }, resultado.ContasRecalculadas);
    }

    [Fact]
    public async Task ExcluirLoteAsync_IncluiPontaDeTransferencia_DelegaEContaNoResultado()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var itemTransfId = Guid.NewGuid();
        var itemComumId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(),
            Saidas = new()
            {
                new() { Id = itemTransfId, Descricao = "Transferência enviada", Valor = 500m, TransferenciaId = transferenciaId, Categoria = "Transferência" },
                new() { Id = itemComumId, Descricao = "Posto", Valor = 150m },
            },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 350m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _transferenciaServiceMock.Setup(t => t.ExcluirAsync(transferenciaId, clienteId, "cliente")).Returns(Task.CompletedTask);

        var resultado = await _sut.ExcluirLoteAsync(contaId, clienteId, "cliente", new ExcluirLoteDto
        {
            Itens = new()
            {
                new() { Id = itemTransfId, Data = "2026-07-20" },
                new() { Id = itemComumId, Data = "2026-07-20" },
            },
        });

        Assert.Equal(2, resultado.TotalExcluidos);
        Assert.Equal(1, resultado.TransferenciasExcluidas);
        _transferenciaServiceMock.Verify(t => t.ExcluirAsync(transferenciaId, clienteId, "cliente"), Times.Once);
        Assert.Single(registro.Saidas); // só o comum foi removido daqui — a transferência é responsabilidade do TransferenciaService
    }

    [Fact]
    public async Task ExcluirLoteAsync_ListaVazia_LancaDadosInvalidos()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ExcluirLoteAsync(contaId, clienteId, "cliente", new ExcluirLoteDto { Itens = new() }));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ExcluirLoteAsync_IdNaoEncontradoNestaConta_LancaNaoEncontradoENaoAplicaMais()
    {
        // Garante a regra de segurança: um id que não existe NESTA conta (ex.: de outro cliente, ou
        // só inválido) derruba o lote inteiro em vez de ignorar silenciosamente — nunca é possível
        // excluir algo fora da conta validada.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var itemValidoId = Guid.NewGuid();
        var itemInexistenteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(), Saidas = new() { new() { Id = itemValidoId, Descricao = "Posto", Valor = 150m } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 850m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync(registro);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirLoteAsync(contaId, clienteId, "cliente", new ExcluirLoteDto
        {
            Itens = new()
            {
                new() { Id = itemInexistenteId, Data = "2026-07-20" },
                new() { Id = itemValidoId, Data = "2026-07-20" },
            },
        }));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ExcluirLoteAsync_ClienteDeOutraConta_LancaAcessoNegado()
    {
        var contaId = Guid.NewGuid();
        var donoId = Guid.NewGuid();
        var outroClienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, donoId));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirLoteAsync(contaId, outroClienteId, "cliente", new ExcluirLoteDto
        {
            Itens = new() { new() { Id = Guid.NewGuid(), Data = "2026-07-20" } },
        }));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task PreviewAsync_TransacaoMarcadaComoIgnorada_ContaComoNovaDeNovo()
    {
        // Bug reportado: reimportar o mesmo arquivo depois que o usuário excluiu o lançamento do
        // extrato (ex.: porque entrou com o sinal errado) ficava bloqueado pra sempre — o histórico
        // "Ignorada" (ver ExcluirLancamentoAsync) não pode mais contar como "já importada" aqui,
        // só serve de auditoria. "Já importada" é decidido só contra o lançamento ATIVO.
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());
        _importRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<TransacaoImportada>
        {
            new() { Id = Guid.NewGuid(), ContaBancariaId = contaId, ClienteId = clienteId, Data = new DateOnly(2026, 7, 26),
                Valor = 50m, Descricao = "Posto", FitId = null, Tipo = "Saida", Status = "Ignorada" },
        });

        var csv = "Data;Descricao;Valor\n26/07/2026;Posto;-50,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(1, resultado.TotalEncontradas);
        Assert.Equal(0, resultado.TotalJaImportadas);
        Assert.Equal(1, resultado.TotalNovas);
    }

    // ── Conciliação com contrapartida provisória de Transferência (Bloco 3C: bug do Pix duplicado) ──
    // Cenário real: Pix de R$480 classificado como Transferência no Nubank cria uma ENTRADA
    // provisória de R$480 no C6 (TransferenciaService.ConverterLancamentoAsync). Antes desta
    // correção, importar o extrato real do C6 trazia "Pix recebido" de R$480 de novo e duplicava —
    // 2 entradas, saldo inflado em R$480.

    [Fact]
    public async Task ImportarArquivoAsync_UmaProvisoriaCandidata_ConciliaEmVezDeDuplicarSaldo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var provisoriaId = Guid.NewGuid();
        var data = new DateOnly(2026, 9, 26);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, saldoInicial: 0m));

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data, Inicio = 0m,
            Entradas = new() { new ItemFinanceiro
            {
                Id = provisoriaId, Descricao = "Transferência de Conta Corrente Nubank", Valor = 480m,
                Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId, Provisoria = true,
            } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 480m,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/09/2026;Pix recebido - Matheus Vidal Lopes;480,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        // Sem duplicata: continua 1 única entrada, e é a MESMA (mesmo Id/TransferenciaId) — só os
        // dados "ganharam realidade" (descrição do banco, deixou de ser provisória).
        var entrada = Assert.Single(registro.Entradas);
        Assert.Equal(provisoriaId, entrada.Id);
        Assert.Equal(transferenciaId, entrada.TransferenciaId);
        Assert.Equal("Pix recebido - Matheus Vidal Lopes", entrada.Descricao);
        Assert.False(entrada.Provisoria);
        Assert.Equal(480m, registro.SaldoFinal); // inalterado — já tinha sido contado quando a provisória nasceu

        Assert.Equal(1, resultado.TotalConciliadasTransferencia);
        Assert.Equal(0, resultado.TotalAmbiguasTransferencia);
        Assert.Equal(0, resultado.TotalImportadas); // nada "novo" de verdade entrou
        Assert.Equal(0, resultado.TotalPendentesCategorizacao);
    }

    [Fact]
    public async Task ImportarArquivoAsync_DuasProvisoriasCandidatas_NaoDecideSozinhoEImportaComoPendente()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 9, 26);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, saldoInicial: 0m));

        var registroA = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data, Inicio = 0m,
            Entradas = new() { new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Transferência de Conta A", Valor = 480m, TransferenciaId = Guid.NewGuid(), Provisoria = true } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 480m,
        };
        var registroB = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data.AddDays(1), Inicio = 480m,
            Entradas = new() { new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Transferência de Conta B", Valor = 480m, TransferenciaId = Guid.NewGuid(), Provisoria = true } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 960m,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroA, registroB });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/09/2026;Pix recebido - Fulano;480,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        // Nenhuma das duas provisórias é tocada — a transação entra como lançamento novo normal
        // (pendente), no dia correspondente, pro usuário conciliar manualmente.
        Assert.Equal(2, registroA.Entradas.Count);
        Assert.True(registroA.Entradas[0].Provisoria);
        var novaEntrada = registroA.Entradas[1];
        Assert.False(novaEntrada.Provisoria);
        Assert.True(novaEntrada.PendenteCategorizacao);
        Assert.True(Assert.Single(registroB.Entradas).Provisoria);

        Assert.Equal(0, resultado.TotalConciliadasTransferencia);
        Assert.Equal(1, resultado.TotalAmbiguasTransferencia);
        Assert.Equal(1, resultado.TotalImportadas);
        Assert.Equal(1, resultado.TotalPendentesCategorizacao);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ProvisoriaForaDaJanelaDeDiasUteis_NaoConcilia()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var dataProvisoria = new DateOnly(2026, 9, 10); // bem fora da janela de ±2 dias úteis
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, saldoInicial: 0m));

        var registroAntigo = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dataProvisoria, Inicio = 0m,
            Entradas = new() { new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Transferência de Conta A", Valor = 480m, TransferenciaId = Guid.NewGuid(), Provisoria = true } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 480m,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroAntigo });
        RegistroDiario? criado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n26/09/2026;Pix recebido - Fulano;480,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.True(Assert.Single(registroAntigo.Entradas).Provisoria); // não tocada
        Assert.NotNull(criado);
        Assert.Single(criado!.Entradas);
        Assert.Equal(0, resultado.TotalConciliadasTransferencia);
        Assert.Equal(0, resultado.TotalAmbiguasTransferencia);
        Assert.Equal(1, resultado.TotalImportadas);
    }

    // ── Fase 1.7: deduplicação contra lançamento manual / entre arquivos ────────────────────────

    [Fact]
    public async Task PreviewAsync_TransacaoParecidaComLancamentoManual_ApareceEmDuplicatasManuais()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var dataManual = new DateOnly(2026, 7, 10);
        var itemManualId = Guid.NewGuid();
        var registroManual = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dataManual,
            Entradas = new(),
            Saidas = new() { new() { Id = itemManualId, Descricao = "Consulta medica", Valor = 150m, Categoria = "Saude" } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = -150m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroManual });

        var csv = "Data;Descricao;Valor\n11/07/2026;CONSULTA MEDICA LTDA;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var preview = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        var dup = Assert.Single(preview.DuplicatasManuais);
        Assert.Equal(itemManualId, dup.LancamentoManualId);
        Assert.Equal("Consulta medica", dup.DescricaoManual);
        Assert.Equal(dataManual.ToString("yyyy-MM-dd"), dup.DataManual);
        Assert.Equal("CONSULTA MEDICA LTDA", dup.DescricaoBanco);
        Assert.Empty(preview.DuplicatasEntreArquivos);
    }

    [Fact]
    public async Task PreviewAsync_CandidatoComHistoricoDeImportacaoAnterior_ApareceEmDuplicatasEntreArquivos()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var data = new DateOnly(2026, 7, 10);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(),
            Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Fornecedor XYZ", Valor = 200m } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = -200m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });
        // Esse item já veio de uma importação anterior (CSV sem FitId, igual a si mesmo) — não é
        // genuinamente manual, então uma transação parecida de OUTRO arquivo não deve virar "Mesclar".
        _importRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<TransacaoImportada>
        {
            new() { Id = Guid.NewGuid(), ContaBancariaId = contaId, ClienteId = clienteId, Data = data, Valor = 200m,
                Descricao = "Fornecedor XYZ", FitId = null, Tipo = "Saida", Status = "Confirmada", ImportadoEm = DateTime.UtcNow },
        });

        var csv = "Data;Descricao;Valor\n11/07/2026;FORNECEDOR XYZ SA;-200,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var preview = await _sut.PreviewAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Empty(preview.DuplicatasManuais);
        var dup = Assert.Single(preview.DuplicatasEntreArquivos);
        Assert.Equal("Fornecedor XYZ", dup.DescricaoJaImportada);
        Assert.Equal("FORNECEDOR XYZ SA", dup.DescricaoBanco);
    }

    [Fact]
    public async Task ImportarArquivoAsync_DuplicataManualSemResolucao_MescladefaultMantemCategoriaEGravaFitIdDescricaoData()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);

        var dataManual = new DateOnly(2026, 7, 10);
        var itemManualId = Guid.NewGuid();
        var registroManual = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dataManual,
            Entradas = new(),
            Saidas = new() { new() { Id = itemManualId, Descricao = "Consulta medica", Valor = 150m, Categoria = "Saude", TipoCusto = "CustoVariavel" } },
            ContasReceber = new(), ContasPagar = new(), Inicio = 0m, SaldoFinal = -150m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroManual });
        var atualizados = new List<RegistroDiario>();
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizados.Add(r)).ReturnsAsync((RegistroDiario r) => r);

        // Mesma data — merge não precisa mover entre dias.
        var csv = "Data;Descricao;Valor\n10/07/2026;CONSULTA MEDICA LTDA;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(1, resultado.TotalMescladasComManual);
        Assert.Equal(0, resultado.TotalImportadas);
        var item = Assert.Single(registroManual.Saidas);
        Assert.Equal(itemManualId, item.Id);
        Assert.Equal("CONSULTA MEDICA LTDA", item.Descricao); // veio do banco
        Assert.Equal("Saude", item.Categoria); // preservado do manual
        Assert.Equal("CustoVariavel", item.TipoCusto); // preservado do manual
        Assert.Equal(150m, item.Valor); // preservado do manual, não sobrescrito pelo banco
    }

    [Fact]
    public async Task ImportarArquivoAsync_MesclaComDataDiferente_MoveItemERecalculaSaldoEmCascata()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId, saldoInicial: 1000m));
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);

        var itemManualId = Guid.NewGuid();
        var dataManual = new DateOnly(2026, 7, 10);   // dia em que o usuário digitou manualmente
        var dataBanco = new DateOnly(2026, 7, 12);    // dia em que o banco efetivamente compensou (±2 dias)

        var registroManual = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dataManual,
            Entradas = new(), Saidas = new() { new() { Id = itemManualId, Descricao = "Aluguel", Valor = 100m, Categoria = "Aluguel" } },
            ContasReceber = new(), ContasPagar = new(), Inicio = 1000m, SaldoFinal = 900m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        var registroSeguinte = new RegistroDiario
        {
            // Dia depois de dataBanco, já existia com Inicio/SaldoFinal calculados a partir do saldo
            // (errado) de antes da mescla — tem que ser corrigido pela cascata, mesmo sem nenhum
            // lançamento próprio.
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dataBanco.AddDays(1),
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            Inicio = 900m, SaldoFinal = 900m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroManual, registroSeguinte });

        RegistroDiario? criadoNoDiaDoBanco = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criadoNoDiaDoBanco = r).ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var csv = $"Data;Descricao;Valor\n{dataBanco:dd/MM/yyyy};ALUGUEL IMOB;-100,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(1, resultado.TotalMescladasComManual);
        Assert.Equal(0, resultado.TotalImportadas);

        Assert.Empty(registroManual.Saidas); // item saiu do dia manual
        Assert.Equal(1000m, registroManual.SaldoFinal); // sem a saída, o dia volta a bater com o Inicio (1000+0-0)

        Assert.NotNull(criadoNoDiaDoBanco);
        var itemMovido = Assert.Single(criadoNoDiaDoBanco!.Saidas);
        Assert.Equal(itemManualId, itemMovido.Id);
        Assert.Equal(100m, itemMovido.Valor); // preservado do manual
        Assert.Equal("Aluguel", itemMovido.Categoria); // preservado do manual
        Assert.Equal("ALUGUEL IMOB", itemMovido.Descricao); // veio do banco
        Assert.Equal(1000m, criadoNoDiaDoBanco.Inicio); // herdado do dia manual já corrigido
        Assert.Equal(900m, criadoNoDiaDoBanco.SaldoFinal); // 1000 - 100

        Assert.Equal(900m, registroSeguinte.Inicio); // cascata: sem lançamento próprio, só repete o saldo
        Assert.Equal(900m, registroSeguinte.SaldoFinal);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ResolucaoImportarComoNovo_NaoTocaNoManualECriaLancamentoNovo()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);

        var dataManual = new DateOnly(2026, 7, 10);
        var itemManualId = Guid.NewGuid();
        var registroManual = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dataManual,
            Entradas = new(), Saidas = new() { new() { Id = itemManualId, Descricao = "Consulta medica", Valor = 150m, Categoria = "Saude" } },
            ContasReceber = new(), ContasPagar = new(), Inicio = 0m, SaldoFinal = -150m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroManual });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var csv = "Data;Descricao;Valor\n10/07/2026;CONSULTA MEDICA LTDA;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resolucoes = new List<ResolucaoDuplicataDto> { new() { TransacaoIndice = 0, Acao = "ImportarComoNovo" } };
        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null, resolucoes);

        Assert.Equal(0, resultado.TotalMescladasComManual);
        Assert.Equal(1, resultado.TotalImportadas);
        Assert.Equal(2, registroManual.Saidas.Count); // manual original intacto + o novo da importação
        var manualIntacto = registroManual.Saidas.Single(s => s.Id == itemManualId);
        Assert.Null(manualIntacto.FitId);
        Assert.Equal("Consulta medica", manualIntacto.Descricao);
    }

    [Fact]
    public async Task ImportarArquivoAsync_DuplicataEntreArquivos_PorPadraoNaoImportaSoSinaliza()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);

        var data = new DateOnly(2026, 7, 10);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Fornecedor XYZ", Valor = 200m } },
            ContasReceber = new(), ContasPagar = new(), Inicio = 0m, SaldoFinal = -200m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        RegistroDiario? criadoParaNovaTransacao = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criadoParaNovaTransacao = r).ReturnsAsync((RegistroDiario r) => r);
        _importRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<TransacaoImportada>
        {
            new() { Id = Guid.NewGuid(), ContaBancariaId = contaId, ClienteId = clienteId, Data = data, Valor = 200m,
                Descricao = "Fornecedor XYZ", FitId = null, Tipo = "Saida", Status = "Confirmada", ImportadoEm = DateTime.UtcNow },
        });

        // Data diferente da do lançamento existente (±2 dias) — vira um RegistroDiario novo, não
        // é mesclado nem some: só fica sinalizado para revisão.
        var csv = "Data;Descricao;Valor\n11/07/2026;FORNECEDOR XYZ SA;-200,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(0, resultado.TotalMescladasComManual);
        Assert.Equal(1, resultado.TotalDuplicatasEntreArquivosSinalizadas);
        // Item 3.1: por padrão NÃO importa — critério de aceite "nenhuma linha duplicada".
        Assert.Equal(1, resultado.TotalDuplicatasEntreArquivosIgnoradas);
        Assert.Equal(0, resultado.TotalImportadas);
        Assert.Single(registro.Saidas); // o lançamento existente não foi tocado
        // O dia 11/07 ainda vira um RegistroDiario (o loop agrupa por data antes de decidir o que
        // fazer com cada item), mas vazio — nenhuma saída foi de fato criada, ficou de fora mesmo.
        Assert.Empty(criadoParaNovaTransacao!.Saidas);
    }

    [Fact]
    public async Task ImportarArquivoAsync_DuplicataEntreArquivosResolvidaComoImportar_ImportaMesmoAssim()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);

        var data = new DateOnly(2026, 7, 10);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
            Entradas = new(), Saidas = new() { new() { Id = Guid.NewGuid(), Descricao = "Fornecedor XYZ", Valor = 200m } },
            ContasReceber = new(), ContasPagar = new(), Inicio = 0m, SaldoFinal = -200m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        RegistroDiario? criadoParaNovaTransacao = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criadoParaNovaTransacao = r).ReturnsAsync((RegistroDiario r) => r);
        _importRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<TransacaoImportada>
        {
            new() { Id = Guid.NewGuid(), ContaBancariaId = contaId, ClienteId = clienteId, Data = data, Valor = 200m,
                Descricao = "Fornecedor XYZ", FitId = null, Tipo = "Saida", Status = "Confirmada", ImportadoEm = DateTime.UtcNow },
        });

        var csv = "Data;Descricao;Valor\n11/07/2026;FORNECEDOR XYZ SA;-200,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);
        var resolucoes = new List<ResolucaoDuplicataDto> { new() { TransacaoIndice = 0, Acao = "Importar" } };

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null, resolucoes);

        Assert.Equal(0, resultado.TotalDuplicatasEntreArquivosIgnoradas);
        Assert.Equal(1, resultado.TotalImportadas);
        Assert.NotNull(criadoParaNovaTransacao);
        Assert.Equal("FORNECEDOR XYZ SA", Assert.Single(criadoParaNovaTransacao!.Saidas).Descricao);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ResultadoTraz_TotalSugestoesVinculoDoMotorDeConciliacao()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemHistoricoOuRegistros(contaId);
        _conciliacaoServiceMock.Setup(s => s.ListarSugestoesAsync(
            clienteId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), clienteId, "cliente", contaId))
            .ReturnsAsync(new List<CaixaDiario.API.DTOs.Conciliacao.SugestaoVinculoDto>
            {
                new() { ContaProvisionadaId = Guid.NewGuid() },
                new() { ContaProvisionadaId = Guid.NewGuid() },
            });

        var csv = "Data;Descricao;Valor\n10/07/2026;Pagamento;-50,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo, null, null);

        Assert.Equal(2, resultado.TotalSugestoesVinculo);
    }
}
