namespace CaixaDiario.API.DTOs.ContasBancarias;

public class LancamentoExtratoDto
{
    public string Data { get; set; } = string.Empty; // "yyyy-MM-dd"
    public string Descricao { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public decimal Valor { get; set; } // positivo = entrada, negativo = saída
    public decimal SaldoAcumulado { get; set; }
    public bool PendenteCategorizacao { get; set; }
}
