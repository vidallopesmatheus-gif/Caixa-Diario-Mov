namespace CaixaDiario.API.DTOs.Financiamentos;

public class AdiantarFinanciamentoDto
{
    public decimal Valor { get; set; }
    public DateOnly Data { get; set; }
    // "ReduzirPrazo" (remove parcelas do fim) | "ReduzirParcela" (mantém o nº de parcelas, reduz o valor de cada uma)
    public string Modo { get; set; } = "ReduzirPrazo";
    // true: só calcula e devolve o resultado, sem persistir nada (simulador da tela 6.6).
    public bool Simular { get; set; }
}

public class AdiantamentoResultDto
{
    public decimal SaldoAntes { get; set; }
    public decimal SaldoDepois { get; set; }
    public int ParcelasAntes { get; set; }
    public int ParcelasDepois { get; set; }
    public decimal JurosRestantesAntes { get; set; }
    public decimal JurosRestantesDepois { get; set; }
    public decimal EconomiaJuros { get; set; }
}
