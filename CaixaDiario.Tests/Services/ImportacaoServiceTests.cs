using System.Text;
using ClosedXML.Excel;
using CaixaDiario.API.DTOs.Importacao;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ImportacaoServiceTests
{
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<ITransacaoImportadaRepository> _importRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<ICategoriaRepository> _categoriaRepoMock = new();
    private readonly ImportacaoService _sut;

    public ImportacaoServiceTests()
    {
        // Espelha o Plano de Contas real (nome -> tipo) só com as categorias usadas nos testes,
        // pra AtualizarCategoriasAsync/ImportarArquivoAsync resolverem o TipoCusto sem precisar
        // configurar isso em todo teste individualmente.
        _categoriaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Manutenção", Tipo = "CustoFixo" },
            new() { Id = Guid.NewGuid(), Nome = "Material de Escritório", Tipo = "CustoFixo" },
            new() { Id = Guid.NewGuid(), Nome = "Aluguel", Tipo = "CustoFixo" },
        });
        _sut = new ImportacaoService(_contaRepoMock.Object, _importRepoMock.Object, _registroRepoMock.Object, _categoriaRepoMock.Object);
    }

    private static ContaBancaria CriarConta(Guid contaId, Guid clienteId, decimal saldoInicial = 0m) => new()
    {
        Id = contaId,
        ClienteId = clienteId,
        Nome = "Conta Teste",
        Tipo = "ContaCorrente",
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

    [Fact]
    public async Task ImportarArquivoAsync_SaidaComPalavraChave_JaSaiCategorizadaSemPendencia()
    {
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
        Assert.False(saida.PendenteCategorizacao);
        Assert.Equal(0, resultado.TotalPendentesCategorizacao);
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
    public async Task ImportarArquivoAsync_EntradaSemCategoria_NuncaFicaPendente()
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
        Assert.False(entrada.PendenteCategorizacao);
        Assert.Equal(0, resultado.TotalPendentesCategorizacao);
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
    public async Task ListarPendentesCategorizacaoAsync_RetornaApenasSaidasPendentes()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var idPendente = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 7, 20),
            Entradas = new() { new() { Id = Guid.NewGuid(), Descricao = "Venda", Valor = 100m } },
            Saidas = new()
            {
                new() { Id = idPendente, Descricao = "Pagamento diverso", Valor = 80m, Categoria = "", PendenteCategorizacao = true },
                new() { Id = Guid.NewGuid(), Descricao = "Aluguel", Valor = 300m, Categoria = "Aluguel", PendenteCategorizacao = false },
            },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 0m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var pendentes = await _sut.ListarPendentesCategorizacaoAsync(contaId, clienteId, "cliente");

        var p = Assert.Single(pendentes);
        Assert.Equal(idPendente, p.Id);
        Assert.Equal("Pagamento diverso", p.Descricao);
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
        _categoriaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria>
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
}
