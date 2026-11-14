using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

// Money Vision ("Outras receitas R$ 8.659,99 no DRE de nov/2026, detalhe vazio"): cada número do
// DRE tem que ser rastreável — o detalhe de qualquer linha soma exatamente o valor da linha.
public class DreClassificacaoTests
{
    private readonly MetricasService _sut = new();
    private static readonly Guid Nubank = Guid.NewGuid();
    private static readonly Guid C6 = Guid.NewGuid();
    private static readonly Guid Caixinha = Guid.NewGuid();

    private static Categoria Cat(string nome, string tipo, string grupo, string bloco, int ordem = 0) =>
        new() { Id = Guid.NewGuid(), Nome = nome, Tipo = tipo, GrupoId = Guid.NewGuid(), Grupo = new Grupo { Nome = grupo, Bloco = bloco }, Ordem = ordem, Ativa = true };

    private static readonly List<Categoria> _plano = new()
    {
        Cat("Vendas e Serviços", "Receita", "Vendas e Serviços", Blocos.ReceitasOperacionais, 1),
        Cat("Outras receitas", "Receita", "Outras receitas", Blocos.ReceitasOperacionais, 2),
        Cat("Devolução e Estorno", "Receita", "Devolução e Estorno", Blocos.ReceitasOperacionais, 3),
        Cat("Simples/DAS", "CustoFixo", "Pagamento de Impostos", Blocos.DeducoesDaReceita, 4),
        Cat("Insumos/Mercadoria", "CustoVariavel", "Custos Diretos", Blocos.CustosOperacionais, 5),
        Cat("Aluguel", "CustoFixo", "Despesas com Ocupação", Blocos.DespesasOperacionais, 6),
        Cat("Equipamentos", "Investimento", "Imobilizado", Blocos.AtividadesDeInvestimento, 7),
        Cat("Empréstimo", "Financiamento", "Empréstimos", Blocos.AtividadesDeFinanciamento, 8),
        Cat("Pró-labore", "RetiradaSocios", "Pró-Labore", Blocos.RetiradasSocios, 9),
    };

    private static ItemFinanceiro Entrada(string desc, decimal valor, string? categoria, string? tipoCusto) =>
        new() { Id = Guid.NewGuid(), Descricao = desc, Valor = valor, Categoria = categoria, TipoCusto = tipoCusto };

    private static ItemFinanceiroSaida Saida(string desc, decimal valor, string? categoria, string? tipoCusto) =>
        new() { Id = Guid.NewGuid(), Descricao = desc, Valor = valor, Categoria = categoria, TipoCusto = tipoCusto };

    private static RegistroDiario Dia(Guid conta, DateOnly data, List<ItemFinanceiro> entradas, List<ItemFinanceiroSaida>? saidas = null) =>
        new() { Id = Guid.NewGuid(), ContaBancariaId = conta, Data = data, Entradas = entradas, Saidas = saidas ?? new() };

    // Novembro do cliente teste: vendas, estorno, o ajuste do Nubank (+11.809,52), um ajuste antigo
    // com TipoCusto sobrescrito, a "atualização de saldo" da caixinha (Rendimento), uma entrada com
    // categoria que não existe no plano ("saldo") e uma baixa de conta a receber sem categoria.
    private static List<RegistroDiario> Novembro() => new()
    {
        Dia(Nubank, new DateOnly(2026, 11, 3), new()
        {
            Entrada("Pix cliente", 9525.74m, "Vendas e Serviços", "Receita"),
            Entrada("Estorno compra", 852.26m, "Devolução e Estorno", "Receita"),
        }, new() { Saida("Aluguel", 3000m, "Aluguel", "CustoFixo") }),
        Dia(Nubank, new DateOnly(2026, 11, 7), new()
        {
            Entrada("Ajuste de conciliação em 07/11 — banco R$ 20.000,00, app R$ 8.190,48", 11809.52m, "Ajuste de saldo", "AjusteConciliacao"),
            Entrada("saldo", 500m, "Ajuste de saldo", "Receita"),
            Entrada("Acerto", 120m, "saldo", "Receita"),
        }),
        Dia(C6, new DateOnly(2026, 11, 10), new() { Entrada("Baixa conta a receber", 300m, null, "Receita") }),
        Dia(Caixinha, new DateOnly(2026, 11, 12), new() { Entrada("Rendimento", 8659.99m, "Rendimento", "Rendimento") }),
    };

    [Fact]
    public void CalcularDre_AjusteDeSaldo_NuncaEntraEmReceitasOperacionais()
    {
        var dre = _sut.CalcularDre(Novembro(), _plano);

        var receitas = dre.Blocos.Single(b => b.Bloco == Blocos.ReceitasOperacionais);
        Assert.DoesNotContain(receitas.Grupos.SelectMany(g => g.Categorias), c => c.Nome == "Ajuste de saldo");
        Assert.Equal(11809.52m + 500m, dre.AjustesConciliacao);
        Assert.Equal(2, dre.QuantidadeAjustesConciliacao);
        Assert.Equal(9525.74m + 852.26m + 120m + 300m + 8659.99m, dre.ReceitaBruta);
    }

    [Fact]
    public void CalcularDre_CategoriaInexistenteOuNula_VaiParaReceitasAClassificarComAviso()
    {
        var dre = _sut.CalcularDre(Novembro(), _plano);

        var receitas = dre.Blocos.Single(b => b.Bloco == Blocos.ReceitasOperacionais);
        var aClassificar = Assert.Single(receitas.Grupos, g => g.Nome == DreClassificacao.ReceitasAClassificar);
        Assert.Equal(420m, aClassificar.Total);
        Assert.Equal(new[] { "saldo", DreClassificacao.SemCategoria }, aClassificar.Categorias.Select(c => c.Nome).OrderBy(n => n));
        Assert.Equal(2, dre.QuantidadeNaoClassificados);
    }

    [Fact]
    public void CalcularDre_RendimentoENuncaOutrasReceitas_OutrasReceitasSoComLancamentosDaCategoria()
    {
        var registros = Novembro();
        registros.Add(Dia(C6, new DateOnly(2026, 11, 20), new() { Entrada("Reembolso", 75m, "Outras receitas", "Receita") }));

        var dre = _sut.CalcularDre(registros, _plano);

        var receitas = dre.Blocos.Single(b => b.Bloco == Blocos.ReceitasOperacionais);
        Assert.Equal(75m, Assert.Single(receitas.Grupos, g => g.Nome == "Outras receitas").Total);
        Assert.Equal(8659.99m, Assert.Single(receitas.Grupos, g => g.Nome == DreClassificacao.GrupoRendimentos).Total);
        var detalhe = _sut.ListarLancamentosDre(registros, _plano, Blocos.ReceitasOperacionais, "Outras receitas", "Outras receitas");
        Assert.Equal("Reembolso", Assert.Single(detalhe).Descricao);
    }

    [Fact]
    public void ListarLancamentosDre_LinhaDeRendimento_MostraOLancamentoComContaEData()
    {
        var detalhe = _sut.ListarLancamentosDre(Novembro(), _plano, Blocos.ReceitasOperacionais, DreClassificacao.GrupoRendimentos, "Rendimento");

        var unico = Assert.Single(detalhe);
        Assert.Equal((Caixinha, new DateOnly(2026, 11, 12), 8659.99m), (unico.ContaBancariaId!.Value, unico.Data, unico.Valor));
    }

    [Fact]
    public void ListarLancamentosDre_BlocoDeAjustes_SomaOValorDosAjustesDeConciliacao()
    {
        var registros = Novembro();
        var dre = _sut.CalcularDre(registros, _plano);

        var detalhe = _sut.ListarLancamentosDre(registros, _plano, DreClassificacao.BlocoAjustes);

        Assert.Equal(dre.AjustesConciliacao, detalhe.Sum(l => l.Valor));
    }

    // Critério de aceite: para todos os meses de 2026, soma do drill-down = valor exibido, em todas
    // as linhas (bloco, grupo e categoria). Dados pseudo-aleatórios com semente fixa.
    [Fact]
    public void ListarLancamentosDre_TodasAsLinhasDeTodosOsMesesDe2026_SomaDoDetalheIgualAoValorDaLinha()
    {
        var registros = Ano2026(new Random(2026));

        for (var mes = 1; mes <= 12; mes++)
        {
            var doMes = registros.Where(r => r.Data.Month == mes).ToList();
            var dre = _sut.CalcularDre(doMes, _plano);

            foreach (var bloco in dre.Blocos)
            {
                Assert.Equal(bloco.Total, _sut.ListarLancamentosDre(doMes, _plano, bloco.Bloco).Sum(l => l.Valor));
                foreach (var grupo in bloco.Grupos)
                {
                    Assert.Equal(grupo.Total, _sut.ListarLancamentosDre(doMes, _plano, bloco.Bloco, grupo.Nome).Sum(l => l.Valor));
                    foreach (var cat in grupo.Categorias)
                        Assert.Equal(cat.Total, _sut.ListarLancamentosDre(doMes, _plano, bloco.Bloco, grupo.Nome, cat.Nome).Sum(l => l.Valor));
                }
            }
            Assert.Equal(dre.AjustesConciliacao, _sut.ListarLancamentosDre(doMes, _plano, DreClassificacao.BlocoAjustes).Sum(l => l.Valor));
            Assert.Equal(dre.ReceitaBruta, dre.Blocos.Single(b => b.Bloco == Blocos.ReceitasOperacionais).Total);
        }
    }

    private static List<RegistroDiario> Ano2026(Random rnd)
    {
        (string? Categoria, string? Tipo)[] entradas =
        [
            ("Vendas e Serviços", "Receita"), ("Outras receitas", "Receita"), ("Devolução e Estorno", "Receita"),
            (null, "Receita"), ("saldo", "Receita"), ("Ajuste de saldo", "Receita"), ("Ajuste de saldo", "AjusteConciliacao"),
            ("Rendimento", "Rendimento"), ("Equipamentos", "Investimento"), ("Empréstimo", "Financiamento"),
            ("Transferência", "Transferencia"),
        ];
        (string? Categoria, string? Tipo)[] saidas =
        [
            ("Simples/DAS", "CustoFixo"), ("Insumos/Mercadoria", "CustoVariavel"), ("Aluguel", "CustoFixo"),
            ("Pró-labore", "RetiradaSocios"), (null, null), ("Categoria Removida", "CustoFixo"), ("Ajuste de saldo", "AjusteSaldo"),
            ("Rendimento", "Rendimento"), ("Equipamentos", "Investimento"), ("Empréstimo", "Financiamento"),
            ("Pagamento de fatura", "PagamentoFatura"), ("Transferência", "Transferencia"),
        ];
        decimal Valor() => Math.Round((decimal)(rnd.NextDouble() * 5000) + 0.01m, 2);
        var contas = new[] { Nubank, C6, Caixinha };

        var registros = new List<RegistroDiario>();
        for (var mes = 1; mes <= 12; mes++)
            for (var dia = 1; dia <= 28; dia += 3)
            {
                var e = Enumerable.Range(0, rnd.Next(0, 4)).Select(_ => entradas[rnd.Next(entradas.Length)])
                    .Select(t => Entrada("e", Valor(), t.Categoria, t.Tipo)).ToList();
                var s = Enumerable.Range(0, rnd.Next(0, 4)).Select(_ => saidas[rnd.Next(saidas.Length)])
                    .Select(t => Saida("s", Valor(), t.Categoria, t.Tipo)).ToList();
                registros.Add(Dia(contas[rnd.Next(contas.Length)], new DateOnly(2026, mes, dia), e, s));
            }
        return registros;
    }
}
