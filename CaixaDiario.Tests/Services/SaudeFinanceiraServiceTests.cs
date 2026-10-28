using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class SaudeFinanceiraServiceTests
{
    private readonly SaudeFinanceiraService _sut = new();
    private static readonly DateOnly Hoje = DataLocalHelper.Hoje();

    private static RegistroDiario CriarRegistro(DateOnly data, decimal entradas = 0m, decimal saidas = 0m) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        ContaBancariaId = Guid.NewGuid(),
        Data = data,
        Entradas = entradas > 0 ? new List<ItemFinanceiro> { new() { Descricao = "Receita", Valor = entradas } } : new(),
        Saidas = saidas > 0 ? new List<ItemFinanceiroSaida> { new() { Descricao = "Despesa", Valor = saidas, Categoria = "Geral" } } : new(),
        ContasReceber = new(),
        ContasPagar = new(),
        CriadoEm = DateTime.UtcNow,
        SalvoEm = DateTime.UtcNow,
    };

    // Money Vision: "taxa de poupança negativa com lucro positivo" (item 3) — taxa de poupança
    // agora é (receita − gastos) ÷ receita, igual "Para onde foi meu dinheiro". Um resgate da
    // reserva de investimento NUNCA entra nesta conta (é "Guardou", um indicador separado) — por
    // isso estes testes não precisam mais de uma conta de investimento, só de receita/gasto reais.
    [Fact]
    public void Calcular_ComReceitaEGastoNoMes_CalculaTaxaPoupancaVerde()
    {
        var registros = new List<RegistroDiario> { CriarRegistro(Hoje, entradas: 1000m, saidas: 700m) };

        var resultado = _sut.Calcular(registros, new List<MetaAnual>(), new HashSet<Guid>());

        Assert.True(resultado.TaxaPoupanca.Disponivel);
        Assert.Equal(30m, resultado.TaxaPoupanca.Valor); // (1000-700)/1000
        Assert.Equal("verde", resultado.TaxaPoupanca.Semaforo);
    }

    // Um resgate da reserva pra cobrir despesa (saída de Transferencia numa conta de Investimento)
    // nunca torna a taxa de poupança negativa, mesmo com lucro operacional real no mês.
    [Fact]
    public void Calcular_ComResgateDeInvestimentoNoMes_TaxaPoupancaContinuaPositiva()
    {
        var contaInvestimentoId = Guid.NewGuid();
        var resgate = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ContaBancariaId = contaInvestimentoId, Data = Hoje,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new List<ItemFinanceiroSaida> { new() { Descricao = "Resgate", Valor = 1325.82m, Categoria = "Transferência", TipoCusto = "Transferencia" } },
        };
        var registros = new List<RegistroDiario> { CriarRegistro(Hoje, entradas: 15895.54m, saidas: 13495.10m), resgate };

        var resultado = _sut.Calcular(registros, new List<MetaAnual>(), new HashSet<Guid> { contaInvestimentoId });

        Assert.True(resultado.TaxaPoupanca.Disponivel);
        Assert.Equal(15.1m, resultado.TaxaPoupanca.Valor); // (15895.54-13495.10)/15895.54, nunca -8.3%
        Assert.Equal("amarelo", resultado.TaxaPoupanca.Semaforo); // 15.1% está entre 5% e 20%
    }

    [Fact]
    public void Calcular_SemReceitaNoMes_TaxaPoupancaIndisponivel()
    {
        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<MetaAnual>(), new HashSet<Guid>());

        Assert.False(resultado.TaxaPoupanca.Disponivel);
        Assert.Equal("cinza", resultado.TaxaPoupanca.Semaforo);
    }

    [Fact]
    public void Calcular_IgnoraRegistrosExcluidos()
    {
        var excluido = CriarRegistro(Hoje, entradas: 1000m, saidas: 100m);
        excluido.Excluido = true;

        var resultado = _sut.Calcular(new List<RegistroDiario> { excluido }, new List<MetaAnual>(), new HashSet<Guid>());

        Assert.False(resultado.TaxaPoupanca.Disponivel);
    }

    [Theory]
    [InlineData(1000, 850, "amarelo")] // sobra 15%
    [InlineData(1000, 980, "vermelho")] // sobra 2%
    public void Calcular_TaxaPoupanca_ClassificaSemaforoPorFaixa(decimal receita, decimal gastos, string semaforoEsperado)
    {
        var registros = new List<RegistroDiario> { CriarRegistro(Hoje, entradas: receita, saidas: gastos) };

        var resultado = _sut.Calcular(registros, new List<MetaAnual>(), new HashSet<Guid>());

        Assert.Equal(semaforoEsperado, resultado.TaxaPoupanca.Semaforo);
    }

    [Fact]
    public void Calcular_SemHistoricoDeReceita_ComprometimentoFixoIndisponivel()
    {
        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<MetaAnual>(), new HashSet<Guid>());

        Assert.False(resultado.ComprometimentoFixos.Disponivel);
    }

    [Fact]
    public void Calcular_ComDespesasFixasRealizadasNosUltimosMeses_CalculaComprometimentoFixoVerde()
    {
        // Numerador e denominador usam o mesmo critério: média dos últimos 3 meses de valores
        // REALIZADOS. Um cliente sem nenhuma Conta a Pagar/Recorrência cadastrada (caso real que
        // zerava o indicador antes desse fix) ainda tem o comprometimento calculado corretamente
        // a partir das Saídas normais classificadas CustoFixo.
        var mesPassado1 = Hoje.AddMonths(-1);
        var mesPassado2 = Hoje.AddMonths(-2);

        RegistroDiario RegistroComCustoFixo(DateOnly data, decimal entrada, decimal saidaFixa) => new()
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ContaBancariaId = Guid.NewGuid(), Data = data,
            Entradas = new List<ItemFinanceiro> { new() { Descricao = "Receita", Valor = entrada } },
            Saidas = new List<ItemFinanceiroSaida> { new() { Descricao = "Aluguel", Valor = saidaFixa, Categoria = "Aluguel", TipoCusto = "CustoFixo" } },
            ContasReceber = new(), ContasPagar = new(),
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        var registros = new List<RegistroDiario>
        {
            RegistroComCustoFixo(mesPassado1, 5000m, 1000m),
            RegistroComCustoFixo(mesPassado2, 5000m, 1000m),
        };

        var resultado = _sut.Calcular(registros, new List<MetaAnual>(), new HashSet<Guid>());

        Assert.True(resultado.ComprometimentoFixos.Disponivel);
        Assert.Equal(20m, resultado.ComprometimentoFixos.Valor);
        Assert.Equal("verde", resultado.ComprometimentoFixos.Semaforo);
    }

    [Fact]
    public void Calcular_SaidaSemTipoCustoCustoFixo_NaoEntraNoComprometimento()
    {
        var mesPassado1 = Hoje.AddMonths(-1);
        var registro = CriarRegistro(mesPassado1, entradas: 5000m, saidas: 2000m); // Categoria "Geral", sem TipoCusto

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<MetaAnual>(), new HashSet<Guid>());

        Assert.True(resultado.ComprometimentoFixos.Disponivel);
        Assert.Equal(0m, resultado.ComprometimentoFixos.Valor);
    }

    [Fact]
    public void Calcular_SemMetaElegivel_RitmoMetaIndisponivel()
    {
        var metaSimples = new MetaAnual { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "simples", AtualizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<MetaAnual> { metaSimples }, new HashSet<Guid>());

        Assert.False(resultado.RitmoMeta.Disponivel);
    }

    [Fact]
    public void Calcular_ComMenosDeUmMesDecorrido_RitmoMetaIndisponivel()
    {
        var meta = new MetaAnual
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "metodo",
            ValorSonho = 100000m, PrazoAnos = 5, TaxaRetorno = 10m, TotalInvestido = 0m,
            AtualizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<MetaAnual> { meta }, new HashSet<Guid>());

        Assert.False(resultado.RitmoMeta.Disponivel);
    }

    [Fact]
    public void Calcular_ComMetaElegivelEProgressoParcial_CalculaRitmoAmarelo()
    {
        var meta = new MetaAnual
        {
            Id = Guid.NewGuid(),
            ClienteId = Guid.NewGuid(),
            Sonho = "Aposentadoria",
            ModoMeta = "metodo",
            ValorSonho = 120000m,
            PrazoAnos = 5,
            TaxaRetorno = 12m,
            TotalInvestido = 20000m,
            AtualizadoEm = DateTime.UtcNow.AddMonths(-6),
            CriadoEm = DateTime.UtcNow.AddMonths(-6),
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<MetaAnual> { meta }, new HashSet<Guid>());

        Assert.True(resultado.RitmoMeta.Disponivel);
        Assert.Equal(85.3m, resultado.RitmoMeta.Valor);
        Assert.Equal("amarelo", resultado.RitmoMeta.Semaforo);
        Assert.Contains("Aposentadoria", resultado.RitmoMeta.Calculo);
    }

    [Fact]
    public void Calcular_PreencheOMesEAnoDoPeriodo()
    {
        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<MetaAnual>(), new HashSet<Guid>());

        Assert.Contains(Hoje.Year.ToString(), resultado.Periodo);
        Assert.NotEmpty(resultado.Periodo);
    }

    [Fact]
    public void Calcular_ComMetaTaxaZeradaEProgressoParcial_CalculaRitmoLinear()
    {
        // TaxaRetorno = 0 é uma meta legítima (guardar sem render) — antes desse fix, o filtro de
        // elegibilidade (TaxaRetorno > 0) descartava essa meta e o card dizia "nenhuma meta
        // configurada", mesmo com uma meta "metodo" real cadastrada.
        var meta = new MetaAnual
        {
            Id = Guid.NewGuid(),
            ClienteId = Guid.NewGuid(),
            Sonho = "Reserva de emergência",
            ModoMeta = "metodo",
            ValorSonho = 12000m,
            PrazoAnos = 1,
            TaxaRetorno = 0m,
            TotalInvestido = 2000m,
            AtualizadoEm = DateTime.UtcNow.AddMonths(-6),
            CriadoEm = DateTime.UtcNow.AddMonths(-6),
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<MetaAnual> { meta }, new HashSet<Guid>());

        Assert.True(resultado.RitmoMeta.Disponivel);
        Assert.Contains("Reserva de emergência", resultado.RitmoMeta.Calculo);
    }

    [Fact]
    public void Calcular_ComMetaTaxaZeradaEMenosDeUmMesDecorrido_MensagemDizAguardarNaoNenhumaMeta()
    {
        var meta = new MetaAnual
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "metodo",
            ValorSonho = 4000m, PrazoAnos = 1, TaxaRetorno = 0m, TotalInvestido = 2000m,
            AtualizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<MetaAnual> { meta }, new HashSet<Guid>());

        Assert.False(resultado.RitmoMeta.Disponivel);
        Assert.Contains("aguarde", resultado.RitmoMeta.Calculo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Nenhuma meta", resultado.RitmoMeta.Calculo);
    }

    [Fact]
    public void Calcular_IgnoraTransferenciaERendimento_NaTaxaDePoupanca()
    {
        var registro = CriarRegistro(Hoje, entradas: 1000m, saidas: 700m);
        // Transferência/Rendimento nunca contam como receita/gasto real (LancamentoFiltro.
        // EhOperacional) — nem um resgate "disfarçado" de entrada nem um aporte "disfarçado" de
        // saída podem inflar ou reduzir a taxa de poupança.
        registro.Entradas.Add(new ItemFinanceiro { Descricao = "Resgate em outra conta", Valor = 5000m, Categoria = "Transferência", TipoCusto = "Transferencia" });
        registro.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Aporte em outra conta", Valor = 2000m, Categoria = "Transferência", TipoCusto = "Transferencia" });
        registro.Entradas.Add(new ItemFinanceiro { Descricao = "Rendimento", Valor = 50m, Categoria = "Rendimento", TipoCusto = "Rendimento" });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<MetaAnual>(), new HashSet<Guid>());

        Assert.Equal(30m, resultado.TaxaPoupanca.Valor); // (1000-700)/1000, ignorando o ruído
    }
}
