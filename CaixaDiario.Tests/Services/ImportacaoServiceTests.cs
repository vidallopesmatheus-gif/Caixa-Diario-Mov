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
    private readonly ImportacaoService _sut;

    public ImportacaoServiceTests()
    {
        _sut = new ImportacaoService(_contaRepoMock.Object, _importRepoMock.Object, _registroRepoMock.Object);
    }

    private static ContaBancaria CriarConta(Guid contaId, Guid clienteId) => new()
    {
        Id = contaId,
        ClienteId = clienteId,
        Nome = "Conta Teste",
        Tipo = "ContaCorrente",
        SaldoInicial = 0m,
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

    private void ConfigurarSemPendentesOuRegistros(Guid contaId)
    {
        _importRepoMock.Setup(r => r.ListarPendentesPorContaAsync(contaId)).ReturnsAsync(new List<TransacaoImportada>());
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ImportarArquivoAsync_CsvComPalavraChaveNaDescricao_SugereCategoriaAutomaticamente()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemPendentesOuRegistros(contaId);

        var csv = "Data;Descricao;Valor\n26/07/2026;Posto Ipiranga Combustivel;-150,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo);

        var transacao = Assert.Single(resultado);
        Assert.Equal("Saida", transacao.Tipo);
        Assert.Equal("Manutenção", transacao.Categoria);
        Assert.False(transacao.Duplicada);
    }

    [Fact]
    public async Task ImportarArquivoAsync_SemPalavraChaveConhecida_NaoSugereCategoria()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemPendentesOuRegistros(contaId);

        var csv = "Data;Descricao;Valor\n26/07/2026;Transferencia recebida joao;500,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo);

        var transacao = Assert.Single(resultado);
        Assert.Equal("Entrada", transacao.Tipo);
        Assert.Null(transacao.Categoria);
    }

    [Fact]
    public async Task ImportarArquivoAsync_TransacaoJaLancadaNoRegistroDiario_MarcaComoDuplicada()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 26);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _importRepoMock.Setup(r => r.ListarPendentesPorContaAsync(contaId)).ReturnsAsync(new List<TransacaoImportada>());
        _importRepoMock.Setup(r => r.AdicionarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>())).Returns(Task.CompletedTask);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
                Entradas = new(),
                Saidas = new List<ItemFinanceiroSaida> { new() { Descricao = "Aluguel escritorio", Valor = 300m, Categoria = "Aluguel" } },
                ContasReceber = new(), ContasPagar = new(),
                CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        var csv = "Data;Descricao;Valor\n26/07/2026;Aluguel escritorio;-300,00\n";
        var arquivo = CriarArquivoTexto("extrato.csv", csv);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo);

        var transacao = Assert.Single(resultado);
        Assert.True(transacao.Duplicada);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ArquivoXlsx_ParseiaTransacoesCorretamente()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemPendentesOuRegistros(contaId);

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

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", mock.Object);

        var transacao = Assert.Single(resultado);
        Assert.Equal("Entrada", transacao.Tipo);
        Assert.Equal(500m, transacao.Valor);
        Assert.Equal("Recebimento Cliente", transacao.Descricao);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ExtensaoInvalida_LancaExcecao()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var arquivo = CriarArquivoTexto("extrato.pdf", "conteudo qualquer");

        await Assert.ThrowsAsync<API.Exceptions.ApiException>(() =>
            _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo));
    }

    [Fact]
    public async Task ImportarArquivoAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        var arquivo = CriarArquivoTexto("extrato.csv", "Data;Descricao;Valor\n");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ImportarArquivoAsync(contaId, Guid.NewGuid(), "cliente", arquivo));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ComContaInexistente_LancaNaoEncontrado()
    {
        var contaId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync((ContaBancaria?)null);
        var arquivo = CriarArquivoTexto("extrato.csv", "Data;Descricao;Valor\n");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ImportarArquivoAsync(contaId, Guid.NewGuid(), "admin", arquivo));

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
            _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ImportarArquivoAsync_ArquivoOfx_ParseiaEDedupPorFitId()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        ConfigurarSemPendentesOuRegistros(contaId);
        _importRepoMock.Setup(r => r.ExisteFitIdAsync(contaId, "1")).ReturnsAsync(false);
        _importRepoMock.Setup(r => r.ExisteFitIdAsync(contaId, "2")).ReturnsAsync(true);

        var ofx = "<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>" +
            "<STMTTRN><TRNTYPE>DEBIT<TRNAMT>-150.00<DTPOSTED>20260710<FITID>1<MEMO>Posto Ipiranga</STMTTRN>" +
            "<STMTTRN><TRNTYPE>DEBIT<TRNAMT>-50.00<DTPOSTED>20260711<FITID>2<MEMO>Ja importada</STMTTRN>" +
            "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
        var arquivo = CriarArquivoTexto("extrato.ofx", ofx);

        var resultado = await _sut.ImportarArquivoAsync(contaId, clienteId, "cliente", arquivo);

        var transacao = Assert.Single(resultado);
        Assert.Equal("Saida", transacao.Tipo);
        Assert.Equal("Manutenção", transacao.Categoria);
        Assert.Equal(150m, transacao.Valor);
        Assert.Equal("1", transacao.FitId);
    }

    [Fact]
    public async Task ListarPendentesAsync_RetornaTransacoesPendentesDaConta()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        var pendente = new TransacaoImportada
        {
            Id = Guid.NewGuid(), ContaBancariaId = contaId, ClienteId = clienteId,
            Data = new DateOnly(2026, 7, 20), Valor = 100m, Descricao = "Venda", Tipo = "Entrada",
            Status = "Pendente", ImportadoEm = DateTime.UtcNow,
        };
        _importRepoMock.Setup(r => r.ListarPendentesPorContaAsync(contaId)).ReturnsAsync(new List<TransacaoImportada> { pendente });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());

        var resultado = await _sut.ListarPendentesAsync(contaId, clienteId, "cliente");

        var dto = Assert.Single(resultado);
        Assert.Equal(pendente.Id, dto.Id);
    }

    [Fact]
    public async Task ConfirmarTransacoesAsync_ComTransacaoDeOutraConta_LancaAcessoNegado()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var transacaoId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _importRepoMock.Setup(r => r.ObterPorIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new List<TransacaoImportada>
            {
                new() { Id = transacaoId, ContaBancariaId = Guid.NewGuid(), ClienteId = clienteId, Tipo = "Entrada", Status = "Pendente" },
            });

        var dto = new ConfirmarTransacoesDto { Transacoes = new() { new() { Id = transacaoId } } };

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ConfirmarTransacoesAsync(contaId, clienteId, "cliente", dto));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ConfirmarTransacoesAsync_ConfirmaEmDiaExistente_AtualizaSaldoERegistro()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var entradaId = Guid.NewGuid();
        var saidaId = Guid.NewGuid();

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data, Inicio = 1000m,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 1000m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroExistente });

        var transacoes = new List<TransacaoImportada>
        {
            new() { Id = entradaId, ContaBancariaId = contaId, ClienteId = clienteId, Data = data, Valor = 200m, Descricao = "Venda", Tipo = "Entrada", Status = "Pendente" },
            new() { Id = saidaId, ContaBancariaId = contaId, ClienteId = clienteId, Data = data, Valor = 80m, Descricao = "Compra", Tipo = "Saida", Status = "Pendente" },
        };
        _importRepoMock.Setup(r => r.ObterPorIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(transacoes);

        RegistroDiario? registroAtualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => registroAtualizado = r)
            .ReturnsAsync((RegistroDiario r) => r);

        List<TransacaoImportada>? loteAtualizado = null;
        _importRepoMock.Setup(r => r.AtualizarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>()))
            .Callback<IEnumerable<TransacaoImportada>>(l => loteAtualizado = l.ToList())
            .Returns(Task.CompletedTask);

        var dto = new ConfirmarTransacoesDto
        {
            Transacoes = new()
            {
                new() { Id = entradaId, Categoria = "Vendas" },
                new() { Id = saidaId, Categoria = "Insumos" },
            },
        };

        await _sut.ConfirmarTransacoesAsync(contaId, clienteId, "cliente", dto);

        Assert.NotNull(registroAtualizado);
        Assert.Equal(1120m, registroAtualizado!.SaldoFinal);
        Assert.Single(registroAtualizado.Entradas);
        Assert.Single(registroAtualizado.Saidas);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);

        Assert.NotNull(loteAtualizado);
        Assert.All(loteAtualizado!, t => Assert.Equal("Confirmada", t.Status));
    }

    [Fact]
    public async Task ConfirmarTransacoesAsync_ComDiaNovoEIgnoradas_CriaRegistroEMarcaIgnorada()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var diaAnterior = new DateOnly(2026, 7, 19);
        var diaNovo = new DateOnly(2026, 7, 20);
        var confirmarId = Guid.NewGuid();
        var ignorarId = Guid.NewGuid();

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));

        var registroAnterior = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = diaAnterior, Inicio = 500m,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 700m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroAnterior });

        var transacoes = new List<TransacaoImportada>
        {
            new() { Id = confirmarId, ContaBancariaId = contaId, ClienteId = clienteId, Data = diaNovo, Valor = 300m, Descricao = "Venda", Tipo = "Entrada", Status = "Pendente" },
            new() { Id = ignorarId, ContaBancariaId = contaId, ClienteId = clienteId, Data = diaNovo, Valor = 40m, Descricao = "Duplicata", Tipo = "Saida", Status = "Pendente" },
        };
        _importRepoMock.Setup(r => r.ObterPorIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(transacoes);

        RegistroDiario? registroCriado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => registroCriado = r)
            .ReturnsAsync((RegistroDiario r) => r);

        List<TransacaoImportada>? loteAtualizado = null;
        _importRepoMock.Setup(r => r.AtualizarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>()))
            .Callback<IEnumerable<TransacaoImportada>>(l => loteAtualizado = l.ToList())
            .Returns(Task.CompletedTask);

        var dto = new ConfirmarTransacoesDto
        {
            Transacoes = new()
            {
                new() { Id = confirmarId },
                new() { Id = ignorarId, Ignorar = true },
            },
        };

        await _sut.ConfirmarTransacoesAsync(contaId, clienteId, "cliente", dto);

        Assert.NotNull(registroCriado);
        Assert.Equal(clienteId, registroCriado!.ClienteId);
        Assert.Equal(700m, registroCriado.Inicio);
        Assert.Equal(1000m, registroCriado.SaldoFinal);
        Assert.Single(registroCriado.Entradas);

        Assert.NotNull(loteAtualizado);
        Assert.Equal("Confirmada", loteAtualizado!.Single(t => t.Id == confirmarId).Status);
        Assert.Equal("Ignorada", loteAtualizado.Single(t => t.Id == ignorarId).Status);
    }

    [Fact]
    public async Task ConfirmarTransacoesAsync_IgnoraTransacoesJaConfirmadas()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 7, 20);
        var jaConfirmadaId = Guid.NewGuid();

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(CriarConta(contaId, clienteId));
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());
        _importRepoMock.Setup(r => r.ObterPorIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(new List<TransacaoImportada>
        {
            new() { Id = jaConfirmadaId, ContaBancariaId = contaId, ClienteId = clienteId, Data = data, Valor = 100m, Descricao = "X", Tipo = "Entrada", Status = "Confirmada" },
        });

        List<TransacaoImportada>? loteAtualizado = null;
        _importRepoMock.Setup(r => r.AtualizarLoteAsync(It.IsAny<IEnumerable<TransacaoImportada>>()))
            .Callback<IEnumerable<TransacaoImportada>>(l => loteAtualizado = l.ToList())
            .Returns(Task.CompletedTask);

        var dto = new ConfirmarTransacoesDto { Transacoes = new() { new() { Id = jaConfirmadaId } } };

        await _sut.ConfirmarTransacoesAsync(contaId, clienteId, "cliente", dto);

        Assert.Empty(loteAtualizado!);
        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }
}
