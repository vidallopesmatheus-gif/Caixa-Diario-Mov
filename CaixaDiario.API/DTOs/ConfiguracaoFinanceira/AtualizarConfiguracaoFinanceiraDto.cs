namespace CaixaDiario.API.DTOs.ConfiguracaoFinanceira;

public class AtualizarConfiguracaoFinanceiraDto
{
    // Nulo = modo automático (ver ConfiguracaoFinanceiraService / frontend/src/utils/fire.ts).
    public decimal? CustoVidaMensalManual { get; set; }
    public decimal TaxaRetiradaFire { get; set; } = 4m;
}
