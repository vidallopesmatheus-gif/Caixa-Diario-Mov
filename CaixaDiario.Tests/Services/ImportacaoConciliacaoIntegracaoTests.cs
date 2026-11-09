using System.Text;
using CaixaDiario.API.Data;
using CaixaDiario.API.DTOs.Importacao;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CaixaDiario.Tests.Services;

// Money Vision ("toda vez preciso lançar ajuste de saldo"): importação com período sobreposto e
// conciliação automática com o saldo do banco — ImportacaoService real + repositórios reais (EF
// InMemory), pra exercitar a dedup, a cobertura e o ponto de conciliação juntos, de ponta a ponta.
public class ImportacaoConciliacaoIntegracaoTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly RegistroRepository _registroRepo;
    private readonly PontoConciliacaoRepository _pontoRepo;
    private readonly ContaBancaria _conta;
    private readonly ImportacaoService _sut;

    public ImportacaoConciliacaoIntegracaoTests()
    {
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _registroRepo = new RegistroRepository(_context);
        _pontoRepo = new PontoConciliacaoRepository(_context);
        _conta = new ContaBancaria
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Nome = "Nubank", Tipo = "ContaCorrente",
            SaldoInicial = 1000m, Ativa = true, DataCriacao = DateTime.UtcNow,
        };

        var contaRepo = new Mock<IContaBancariaRepository>();
        contaRepo.Setup(r => r.ObterPorIdAsync(_conta.Id)).ReturnsAsync(_conta);
        var importRepo = new TransacaoImportadaRepository(_context);
        var periodoRepo = new PeriodoExtratoRepository(_context);
        var categoriaRepo = new Mock<ICategoriaRepository>();
        categoriaRepo.Setup(r => r.ListarTodasPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Categoria>());
        var regraRepo = new Mock<IRegraCategorizacaoRepository>();
        regraRepo.Setup(r => r.ListarAtivasPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<RegraCategorizacao>());
        var conciliacaoService = new Mock<IConciliacaoService>();
        conciliacaoService.Setup(s => s.ListarSugestoesAsync(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new List<CaixaDiario.API.DTOs.Conciliacao.SugestaoVinculoDto>());

        var conciliacaoSaldo = new ConciliacaoSaldoService(contaRepo.Object, _registroRepo, importRepo, _pontoRepo, periodoRepo);
        _sut = new ImportacaoService(
            contaRepo.Object, importRepo, _registroRepo, categoriaRepo.Object, regraRepo.Object,
            new Mock<ITransferenciaService>().Object, conciliacaoService.Object, new FaturaRepository(_context),
            new Mock<IParcelamentoDeteccaoService>().Object, _context, NullLogger<ImportacaoService>.Instance,
            new Mock<IFaturaCartaoService>().Object, conciliacaoSaldo, periodoRepo);
    }

    public void Dispose() => _context.Dispose();

    private static IFormFile Arquivo(string nome, string conteudo)
    {
        var bytes = Encoding.UTF8.GetBytes(conteudo);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(nome);
        mock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(bytes));
        mock.Setup(f => f.Length).Returns(bytes.Length);
        return mock.Object;
    }

    private static string Ofx(IEnumerable<(string Data, decimal Valor, string Memo, string FitId)> linhas, (string Data, decimal Saldo)? saldo = null)
    {
        var sb = new StringBuilder("OFXHEADER:100\nDATA:OFXSGML\n\n<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>\n");
        foreach (var (data, valor, memo, fitId) in linhas)
            sb.Append($"<STMTTRN><TRNTYPE>OTHER<DTPOSTED>{data}<TRNAMT>{valor.ToString(System.Globalization.CultureInfo.InvariantCulture)}<FITID>{fitId}<MEMO>{memo}</STMTTRN>\n");
        sb.Append("</BANKTRANLIST>\n");
        if (saldo.HasValue)
            sb.Append($"<LEDGERBAL><BALAMT>{saldo.Value.Saldo.ToString(System.Globalization.CultureInfo.InvariantCulture)}<DTASOF>{saldo.Value.Data}</LEDGERBAL>\n");
        sb.Append("</STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>");
        return sb.ToString();
    }

    private static readonly (string, decimal, string, string)[] PrimeiraQuinzena =
    {
        ("20261002", -50m, "Uber", "A1"),
        ("20261002", -50m, "Uber", "A2"), // 2 Ubers iguais no mesmo dia: legítimos, não duplicata
        ("20261010", 2000m, "Pix recebido Cliente X", "A3"),
        ("20261014", -300m, "Aluguel", "A4"),
    };

    private async Task<List<(DateOnly Data, string Descricao, decimal Valor)>> LancamentosAsync() =>
        (await _registroRepo.ListarPorContaAsync(_conta.Id))
            .SelectMany(r => r.Entradas.Select(e => (r.Data, e.Descricao, e.Valor)).Concat(r.Saidas.Select(s => (r.Data, s.Descricao, -s.Valor))))
            .ToList();

    [Fact]
    public async Task Importar_MesInteiroDepoisDeUmaQuinzena_NaoDuplicaNadaENaoCriaDiferenca()
    {
        await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("q1.ofx", Ofx(PrimeiraQuinzena)), null, null);
        var mesInteiro = PrimeiraQuinzena.Concat(new[] { ("20261020", -120m, "Mercado", "A5"), ("20261105", -80m, "Farmácia", "A6") });

        var resultado = await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente",
            Arquivo("mes.ofx", Ofx(mesInteiro, ("20261107", 2400m))), null, null);

        Assert.Equal(2, resultado.TotalImportadas);
        Assert.Equal(6, (await LancamentosAsync()).Count);
        Assert.True(resultado.Conciliacao!.Conciliado); // 1000 − 100 + 2000 − 300 − 120 − 80 = 2400
    }

    [Fact]
    public async Task Importar_BancoRenumerouOsFitIds_AindaReconheceAsJaImportadas()
    {
        await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("q1.ofx", Ofx(PrimeiraQuinzena)), null, null);
        var renumeradas = PrimeiraQuinzena.Select(l => (l.Item1, l.Item2, l.Item3, "B" + l.Item4)).Append(("20261020", -120m, "Mercado", "B5"));

        var resultado = await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("mes.ofx", Ofx(renumeradas)), null, null);

        Assert.Equal(1, resultado.TotalImportadas);
        Assert.Equal(5, (await LancamentosAsync()).Count);
    }

    [Fact]
    public async Task Preview_PeriodoSobreposto_MostraCoberturaJaHaviaENovosDias()
    {
        await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("q1.ofx", Ofx(PrimeiraQuinzena)), null, null);
        var mesInteiro = PrimeiraQuinzena.Append(("20261107", -10m, "Café", "A9"));

        var preview = await _sut.PreviewAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("mes.ofx", Ofx(mesInteiro)), null, null);

        Assert.Equal((new DateOnly(2026, 10, 2), new DateOnly(2026, 11, 7)), (preview.Cobertura.ArquivoDe, preview.Cobertura.ArquivoAte));
        Assert.Equal((new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 14)), (preview.Cobertura.JaHaviaDe, preview.Cobertura.JaHaviaAte));
        Assert.Equal(new DateOnly(2026, 10, 15), preview.Cobertura.NovosDiasDe);
        Assert.Equal(1, preview.TotalNovas);
    }

    [Fact]
    public async Task Preview_LinhaImportadaQueSumiuDoExtratoNovo_ApareceComoAusenteEPodeSerExcluida()
    {
        await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("q1.ofx", Ofx(PrimeiraQuinzena)), null, null);
        var semOAluguel = PrimeiraQuinzena.Where(l => l.Item4 != "A4").Append(("20261020", -120m, "Mercado", "A5")).ToList();

        var preview = await _sut.PreviewAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("mes.ofx", Ofx(semOAluguel)), null, null);
        var ausente = Assert.Single(preview.AusentesNoArquivo);
        Assert.Equal(("Aluguel", -300m), (ausente.Descricao, ausente.Valor));

        await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("mes.ofx", Ofx(semOAluguel)), null, null,
            opcoesConciliacao: new OpcoesConciliacaoImportacaoDto
            {
                ExcluirAusentes = { new ItemExcluirLoteDto { Id = ausente.Id, Data = "2026-10-14" } },
            });

        Assert.DoesNotContain(await LancamentosAsync(), l => l.Descricao == "Aluguel");
    }

    [Fact]
    public async Task Importar_LedgerBalDiferenteDoApp_NaoCriaAjusteEDevolveADiferenca()
    {
        var resultado = await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente",
            Arquivo("q1.ofx", Ofx(PrimeiraQuinzena, ("20261015", 2650m))), null, null);

        Assert.False(resultado.Conciliacao!.Conciliado);
        Assert.Equal((2600m, 50m), (resultado.Conciliacao.SaldoApp, resultado.Conciliacao.Diferenca));
        Assert.DoesNotContain(await LancamentosAsync(), l => l.Descricao.StartsWith("Ajuste"));
    }

    [Fact]
    public async Task Importar_MesmoArquivoDeNovoComSaldo_NaoFalhaEConcilia()
    {
        await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("q1.ofx", Ofx(PrimeiraQuinzena)), null, null);

        var resultado = await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente",
            Arquivo("q1.ofx", Ofx(PrimeiraQuinzena, ("20261015", 2600m))), null, null);

        Assert.Equal(0, resultado.TotalImportadas);
        Assert.True(resultado.Conciliacao!.Conciliado);
    }

    [Fact]
    public async Task Importar_PlanilhaComColunaSaldo_GravaUmPontoPorDia()
    {
        var csv = "Data;Descricao;Valor;Saldo\n02/10/2026;Uber;-50,00;950,00\n02/10/2026;Padaria;-20,00;930,00\n05/10/2026;Pix recebido;100,00;1030,00\n";

        var resultado = await _sut.ImportarArquivoAsync(_conta.Id, _conta.ClienteId, "cliente", Arquivo("extrato.csv", csv), null, null);

        var pontos = await _pontoRepo.ListarPorContaAsync(_conta.Id);
        Assert.Equal(new[] { (new DateOnly(2026, 10, 2), 930m), (new DateOnly(2026, 10, 5), 1030m) }, pontos.Select(p => (p.Data, p.SaldoBanco)));
        Assert.True(resultado.Conciliacao!.Conciliado);
    }
}
