using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class LancamentoFiltroTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("Receita", true)]
    [InlineData("CustoFixo", true)]
    [InlineData("CustoVariavel", true)]
    [InlineData("Transferencia", false)]
    [InlineData("Rendimento", false)]
    [InlineData("Investimento", false)]
    [InlineData("Financiamento", false)]
    [InlineData("PagamentoFatura", false)]
    // Money Vision — Bloco 3: ajuste de saldo nunca é receita/despesa real, mesmo categorizado
    // como algo normal (ex.: "Devoluções") antes da correção — nunca entra no DRE/médias.
    [InlineData("AjusteSaldo", false)]
    public void EhOperacional_ClassificaCorretamenteCadaTipoCusto(string? tipoCusto, bool esperado)
    {
        Assert.Equal(esperado, LancamentoFiltro.EhOperacional(tipoCusto));
    }
}
