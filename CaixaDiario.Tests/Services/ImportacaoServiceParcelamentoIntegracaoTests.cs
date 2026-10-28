using System.Text;
using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CaixaDiario.Tests.Services;

// Bug real em produção: a migração da tabela "parcelamentos" não tinha sido aplicada, e qualquer
// linha com parcela detectada derrubava a importação inteira com 500 (os testes de ImportacaoService
// sempre mockam IParcelamentoDeteccaoService, e os de ParcelamentoDeteccaoService sempre mockam os
// repositórios — nenhum dos dois exercitava a integração real entre o serviço e o EF). Esta classe
// fecha esse buraco: ImportacaoService real + ParcelamentoDeteccaoService real + repositórios reais
// (EF InMemory) juntos.
public class ImportacaoServiceParcelamentoIntegracaoTests
{
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

    private static ContaBancaria CriarCartao(Guid contaId, Guid clienteId) => new()
    {
        Id = contaId, ClienteId = clienteId, Nome = "Cartões", Tipo = "CartaoCredito",
        SaldoInicial = 0m, Ativa = true, DataCriacao = DateTime.UtcNow,
    };

    private static ImportacaoService CriarSut(
        AppDbContext context, IRegistroRepository registroRepo, IFaturaRepository faturaRepo,
        ContaBancaria conta, IParcelamentoDeteccaoService parcelamentoDeteccaoService)
    {
        var contaRepoMock = new Mock<IContaBancariaRepository>();
        contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        var importRepoMock = new Mock<ITransacaoImportadaRepository>();
        importRepoMock.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<TransacaoImportada>());
        var categoriaRepoMock = new Mock<ICategoriaRepository>();
        categoriaRepoMock.Setup(r => r.ListarTodasPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Categoria>());
        var regraRepoMock = new Mock<IRegraCategorizacaoRepository>();
        regraRepoMock.Setup(r => r.ListarAtivasPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<RegraCategorizacao>());
        var transferenciaServiceMock = new Mock<ITransferenciaService>();
        var conciliacaoServiceMock = new Mock<IConciliacaoService>();
        conciliacaoServiceMock.Setup(s => s.ListarSugestoesAsync(
            It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new List<CaixaDiario.API.DTOs.Conciliacao.SugestaoVinculoDto>());
        var faturaCartaoServiceMock = new Mock<IFaturaCartaoService>();
        faturaCartaoServiceMock.Setup(s => s.AutoVincularPagamentoPorTransferenciaAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(false);

        return new ImportacaoService(
            contaRepoMock.Object, importRepoMock.Object, registroRepo, categoriaRepoMock.Object,
            regraRepoMock.Object, transferenciaServiceMock.Object, conciliacaoServiceMock.Object,
            faturaRepo, parcelamentoDeteccaoService, context, NullLogger<ImportacaoService>.Instance, faturaCartaoServiceMock.Object);
    }

    [Fact]
    public async Task ImportarArquivoAsync_UltimaParcelaComRepositoriosReais_NaoLancaEGravaNormal()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new AppDbContext(options);
        var registroRepo = new RegistroRepository(context);
        var faturaRepo = new FaturaRepository(context);
        var parcelamentoDeteccaoService = new ParcelamentoDeteccaoService(new ParcelamentoRepository(context), registroRepo);

        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var conta = CriarCartao(contaId, clienteId);
        var sut = CriarSut(context, registroRepo, faturaRepo, conta, parcelamentoDeteccaoService);

        // Estado real do cliente teste: fatura C6 10/2026 já tem Anthropic + IOF de uma importação anterior.
        await registroRepo.AdicionarAsync(new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 9, 23),
            Inicio = 0m, SaldoFinal = -119.58m, Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new()
            {
                new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Anthropic", Valor = 115.54m, Categoria = "Assinaturas" },
                new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "IOF", Valor = 4.04m, Categoria = "Taxas" },
            },
        });

        var csv = "Data;Descricao;Valor\n01/04/2026;MercadoLivre - parcela 7/7;100,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        var resultado = await sut.ImportarArquivoAsync(
            contaId, clienteId, "cliente", arquivo, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 1),
            banco: "C6", dataVencimentoFatura: new DateOnly(2026, 10, 20), dataFechamentoFatura: new DateOnly(2026, 10, 13),
            competenciaFatura: "2026-10");

        Assert.Equal(1, resultado.TotalImportadas);
        Assert.Empty(resultado.AvisosParcelamento);
        var parcelamento = Assert.Single(resultado.ParcelamentosDetectados);
        Assert.Equal("Quitado", parcelamento.Status);
    }

    [Fact]
    public async Task ImportarArquivoAsync_DeteccaoDeParcelamentoFalha_ImportaACompraNormalComAviso()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new AppDbContext(options);
        var registroRepo = new RegistroRepository(context);
        var faturaRepo = new FaturaRepository(context);

        var parcelamentoDeteccaoServiceMock = new Mock<IParcelamentoDeteccaoService>();
        parcelamentoDeteccaoServiceMock
            .Setup(s => s.ProcessarParcelaDetectadaAsync(
                It.IsAny<ContaBancaria>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
            .ThrowsAsync(new InvalidOperationException("relation \"parcelamentos\" does not exist"));

        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var conta = CriarCartao(contaId, clienteId);
        var sut = CriarSut(context, registroRepo, faturaRepo, conta, parcelamentoDeteccaoServiceMock.Object);

        var csv = "Data;Descricao;Valor\n01/04/2026;MercadoLivre - parcela 7/7;100,00\n";
        var arquivo = CriarArquivoTexto("fatura.csv", csv);

        var resultado = await sut.ImportarArquivoAsync(
            contaId, clienteId, "cliente", arquivo, null, null,
            banco: "C6", dataVencimentoFatura: new DateOnly(2026, 10, 20));

        // A compra entrou normal, só sem o vínculo de parcelamento — a importação nunca lança.
        Assert.Equal(1, resultado.TotalImportadas);
        Assert.Empty(resultado.ParcelamentosDetectados);
        var aviso = Assert.Single(resultado.AvisosParcelamento);
        Assert.Contains("MercadoLivre - parcela 7/7", aviso);

        var registros = await registroRepo.ListarPorContaAsync(contaId);
        var saida = Assert.Single(registros.SelectMany(r => r.Saidas));
        Assert.Null(saida.ParcelamentoId);
    }

    [Fact]
    public async Task ImportarArquivoAsync_MesmoBancoECompetenciaEmDuasImportacoes_ReaproveitaAFaturaExistente()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new AppDbContext(options);
        var registroRepo = new RegistroRepository(context);
        var faturaRepo = new FaturaRepository(context);
        var parcelamentoDeteccaoService = new ParcelamentoDeteccaoService(new ParcelamentoRepository(context), registroRepo);

        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var conta = CriarCartao(contaId, clienteId);
        var sut = CriarSut(context, registroRepo, faturaRepo, conta, parcelamentoDeteccaoService);

        var primeira = await sut.ImportarArquivoAsync(
            contaId, clienteId, "cliente",
            CriarArquivoTexto("fatura1.csv", "Data;Descricao;Valor\n23/09/2026;Anthropic;115,54\n"),
            null, null, banco: "C6", dataVencimentoFatura: new DateOnly(2026, 10, 20), competenciaFatura: "2026-10");

        var segunda = await sut.ImportarArquivoAsync(
            contaId, clienteId, "cliente",
            CriarArquivoTexto("fatura2.csv", "Data;Descricao;Valor\n01/04/2026;MercadoLivre - parcela 7/7;100,00\n"),
            null, null, banco: "C6", dataVencimentoFatura: new DateOnly(2026, 10, 20), competenciaFatura: "2026-10");

        Assert.Equal(primeira.FaturaId, segunda.FaturaId);
        Assert.Single(await faturaRepo.ListarPorContaAsync(contaId));
    }
}
