using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class ConciliacaoSaldoCalculoTests
{
    private static readonly ContaBancaria Conta = new() { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Nome = "Nubank", Tipo = "ContaCorrente", SaldoInicial = 1000m };
    private static readonly HashSet<(DateOnly, decimal, string)> NenhumaChave = new();

    private static RegistroDiario Dia(DateOnly data, params (string Descricao, decimal Valor, string? FitId)[] itens)
    {
        var r = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Conta.ClienteId, ContaBancariaId = Conta.Id, Data = data,
            Entradas = itens.Where(i => i.Valor > 0).Select(i => new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = i.Descricao, Valor = i.Valor, FitId = i.FitId }).ToList(),
            Saidas = itens.Where(i => i.Valor < 0).Select(i => new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = i.Descricao, Valor = -i.Valor, FitId = i.FitId }).ToList(),
            ContasReceber = new(), ContasPagar = new(),
        };
        return r;
    }

    private static List<RegistroDiario> Encadeados(params RegistroDiario[] dias)
    {
        SaldoEncadeamento.Encadear(Conta.SaldoInicial, dias);
        return dias.ToList();
    }

    private static PontoConciliacao Ponto(DateOnly data, decimal saldoBanco) =>
        new() { Id = Guid.NewGuid(), ContaBancariaId = Conta.Id, Data = data, SaldoBanco = saldoBanco, Origem = "Ofx" };

    [Fact]
    public void Diagnosticar_DuplicataExplicaADiferenca_ApareceNaCombinacao()
    {
        var registros = Encadeados(
            Dia(new DateOnly(2026, 11, 3), ("Pix enviado Joao", -342.03m, "F1"), ("Pix enviado João", -342.03m, null)),
            Dia(new DateOnly(2026, 11, 5), ("Mercado", -100m, "F3")));
        var dias = new SortedSet<DateOnly>(new[] { new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 5) });

        // Banco: 1000 − 342,03 − 100 = 557,97; app conta a duplicata: 215,94.
        var d = ConciliacaoSaldoCalculo.Diagnosticar(Conta, registros, new(), dias, NenhumaChave, new DateOnly(2026, 11, 5), 557.97m);

        Assert.Equal(342.03m, d.Diferenca);
        var grupo = Assert.Single(d.Duplicatas);
        Assert.Equal(-342.03m, grupo.SomaExcedente);
        Assert.Equal("Excluir esta duplicata zera a diferença", d.Combinacao!.Resumo);
        Assert.Equal(grupo.Itens[1].Id, Assert.Single(d.Combinacao.Itens).Id);
    }

    // Money Vision: "Rafael Yan −590 duas vezes em 01/11" aconteceu de verdade — FITIDs diferentes
    // no OFX são a prova do banco, nunca uma duplicata sugerida.
    [Fact]
    public void Diagnosticar_LinhasIguaisComFitIdsDiferentes_NaoSaoSugeridasComoDuplicata()
    {
        var registros = Encadeados(Dia(new DateOnly(2026, 11, 1), ("Pix enviado Rafael Yan", -590m, "F1"), ("Pix enviado Rafael Yan", -590m, "F2")));
        var dias = new SortedSet<DateOnly>(new[] { new DateOnly(2026, 11, 1) });

        var d = ConciliacaoSaldoCalculo.Diagnosticar(Conta, registros, new(), dias, NenhumaChave, new DateOnly(2026, 11, 1), -180m);

        Assert.Empty(d.Duplicatas);
    }

    [Fact]
    public void Diagnosticar_PixRecebidoComoSaida_ApareceComoSinalSuspeito()
    {
        var registros = Encadeados(Dia(new DateOnly(2026, 11, 3), ("Pix recebido Maria", -200m, "F1")));

        var d = ConciliacaoSaldoCalculo.Diagnosticar(Conta, registros, new(), new SortedSet<DateOnly>(), NenhumaChave, new DateOnly(2026, 11, 3), 1200m);

        Assert.Equal(400m, Assert.Single(d.SinalSuspeito).EfeitoSeCorrigir);
        Assert.Equal("Inverter o sinal deste lançamento zera a diferença", d.Combinacao!.Resumo);
    }

    [Fact]
    public void Diagnosticar_SoAnalisaDepoisDoUltimoPontoConciliadoEApontaLacunasEManuais()
    {
        var registros = Encadeados(
            Dia(new DateOnly(2026, 6, 1), ("Antigo manual", -10m, null)),
            Dia(new DateOnly(2026, 8, 2), ("Lançado à mão", -60m, null)),
            Dia(new DateOnly(2026, 8, 5), ("Extrato", -40m, "F1")));
        var pontos = new List<PontoConciliacao> { Ponto(new DateOnly(2026, 6, 1), 990m) };
        var dias = new SortedSet<DateOnly>(new[] { new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5) });

        var d = ConciliacaoSaldoCalculo.Diagnosticar(Conta, registros, pontos, dias, NenhumaChave, new DateOnly(2026, 8, 5), 950m);

        Assert.Equal(new DateOnly(2026, 6, 2), d.AnalisadoDesde);
        var lacuna = Assert.Single(d.Lacunas, l => l.De == new DateOnly(2026, 6, 2));
        Assert.Equal(new DateOnly(2026, 7, 31), lacuna.Ate);
        Assert.Equal("Lançado à mão", Assert.Single(d.Manuais).Descricao);
        Assert.Equal("Excluir este lançamento manual zera a diferença", d.Combinacao!.Resumo);
    }

    [Fact]
    public void Diagnosticar_LinhaDePlanilhaImportada_NaoContaComoManual()
    {
        var registros = Encadeados(Dia(new DateOnly(2026, 11, 3), ("Uber", -30m, null)));
        var chaves = new HashSet<(DateOnly, decimal, string)> { ConciliacaoSaldoCalculo.ChaveImportada(new DateOnly(2026, 11, 3), -30m, "uber") };

        var d = ConciliacaoSaldoCalculo.Diagnosticar(Conta, registros, new(), new SortedSet<DateOnly>(), chaves, new DateOnly(2026, 11, 3), 970m);

        Assert.Empty(d.Manuais);
        Assert.Null(d.Combinacao); // já bate
    }

    [Fact]
    public void Diagnosticar_TransferenciaComSoUmaPonta_ApareceSemPar()
    {
        var dia = Dia(new DateOnly(2026, 11, 3), ("Transferência p/ Reserva", -500m, null));
        dia.Saidas[0].TransferenciaId = Guid.NewGuid();
        var registros = Encadeados(dia);

        var d = ConciliacaoSaldoCalculo.Diagnosticar(Conta, registros, new(), new SortedSet<DateOnly>(), NenhumaChave, new DateOnly(2026, 11, 3), 1000m);

        Assert.Equal(500m, Assert.Single(d.TransferenciasSemPar).EfeitoSeCorrigir);
        Assert.Equal("Excluir esta transferência sem par zera a diferença", d.Combinacao!.Resumo);
    }

    [Fact]
    public void CalcularStatus_UltimoPontoDiverge_InformaDesdeQuandoEOsDiasSemExtrato()
    {
        var registros = Encadeados(
            Dia(new DateOnly(2026, 10, 10), ("Venda", 100m, "F1")),
            Dia(new DateOnly(2026, 10, 18), ("Compra", -50m, "F2")));
        var pontos = new List<PontoConciliacao>
        {
            Ponto(new DateOnly(2026, 10, 10), 1100m), // bate
            Ponto(new DateOnly(2026, 10, 18), 1000m), // app 1050
            Ponto(new DateOnly(2026, 10, 25), 990m),  // app 1050
        };
        var dias = new SortedSet<DateOnly>(new[] { new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 18) });

        var status = ConciliacaoSaldoCalculo.CalcularStatus(Conta, registros, pontos, dias, new DateOnly(2026, 11, 8));

        Assert.Equal((new DateOnly(2026, 10, 18), 21), (status.ExtratoAte!.Value, status.DiasSemExtrato!.Value));
        Assert.Equal(new DateOnly(2026, 10, 18), status.DivergenteDesde);
        Assert.Equal(-60m, status.UltimoPonto!.Diferenca);
        Assert.Equal(1050m, status.SaldoApp);
    }

    [Fact]
    public void CalcularStatus_UltimoPontoBate_NaoTemDivergencia()
    {
        var registros = Encadeados(Dia(new DateOnly(2026, 11, 7), ("Venda", 100m, "F1")));

        var status = ConciliacaoSaldoCalculo.CalcularStatus(
            Conta, registros, new List<PontoConciliacao> { Ponto(new DateOnly(2026, 11, 7), 1100m) }, new SortedSet<DateOnly>(), new DateOnly(2026, 11, 8));

        Assert.True(status.UltimoPonto!.Conciliado);
        Assert.Null(status.DivergenteDesde);
        Assert.Null(status.ExtratoAte);
    }

    [Fact]
    public void DiasComExtrato_PeriodoImportadoCobreOsDiasSemTransacao()
    {
        var periodos = new[] { new PeriodoExtrato { De = new DateOnly(2026, 10, 1), Ate = new DateOnly(2026, 10, 3) } };
        var importadas = new[]
        {
            new TransacaoImportada { Data = new DateOnly(2026, 9, 1), Status = "Confirmada" },
            new TransacaoImportada { Data = new DateOnly(2026, 9, 2), Status = "Ignorada" },
        };

        var dias = ConciliacaoSaldoCalculo.DiasComExtrato(periodos, importadas, new List<RegistroDiario>());

        Assert.Equal(new[] { new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 3) }, dias);
    }

    // Money Vision ("contrapartida falsa", caso B): o app criou +400 no C6 a partir do Nubank, mas
    // o extrato do C6 cobre 14/10 e não tem esse recebimento — é a causa da diferença.
    [Fact]
    public void Diagnosticar_ContrapartidaCriadaSemLancamentoNoExtrato_ApareceComoCausaEZeraADiferenca()
    {
        var outraConta = Guid.NewGuid();
        var transferencia = new Transferencia { Id = Guid.NewGuid(), ClienteId = Conta.ClienteId, ContaOrigemId = outraConta, ContaDestinoId = Conta.Id, Data = new DateOnly(2026, 10, 14), Valor = 400m };
        var diaC6 = Dia(new DateOnly(2026, 10, 14), ("Venda", 100m, "F1"));
        diaC6.Entradas.Add(new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Transferência enviada pelo Pix - Matheus", Valor = 400m, TransferenciaId = transferencia.Id, TipoCusto = "Transferencia" });
        var diaNubank = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Conta.ClienteId, ContaBancariaId = outraConta, Data = new DateOnly(2026, 10, 14), Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Transferência enviada pelo Pix - Matheus", Valor = 400m, TransferenciaId = transferencia.Id, FitId = "N1" } },
        };
        var registros = Encadeados(diaC6).Append(diaNubank).ToList();
        var dias = new SortedSet<DateOnly>(new[] { new DateOnly(2026, 10, 14) });
        var achados = RevisaoContrapartidas.Analisar(registros, new List<Transferencia> { transferencia },
            new Dictionary<Guid, SortedSet<DateOnly>> { [Conta.Id] = dias }, new Dictionary<Guid, HashSet<(DateOnly, decimal, string)>>());

        // App: 1000 + 100 + 400 = 1500; banco: 1100 (o +400 nunca entrou no C6).
        var d = ConciliacaoSaldoCalculo.Diagnosticar(Conta, registros, new(), dias, NenhumaChave, new DateOnly(2026, 10, 14), 1100m, contrapartidas: achados);

        Assert.Equal(-400m, Assert.Single(d.ContrapartidasSemLancamento).EfeitoSeCorrigir);
        Assert.Equal("Corrigir esta contrapartida de transferência zera a diferença", d.Combinacao!.Resumo);
    }
}
