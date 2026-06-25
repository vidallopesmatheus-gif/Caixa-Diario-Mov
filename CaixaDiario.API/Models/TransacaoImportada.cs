namespace CaixaDiario.API.Models;

public class TransacaoImportada
{
    public Guid Id { get; set; }
    public Guid ContaBancariaId { get; set; }
    public Guid ClienteId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Valor { get; set; }          // positivo = entrada, negativo = saída
    public string Descricao { get; set; } = string.Empty;
    public string? FitId { get; set; }           // ID único OFX (para dedup)
    public string Tipo { get; set; } = "Entrada"; // "Entrada" | "Saida"
    public string Status { get; set; } = "Pendente"; // "Pendente" | "Confirmada" | "Ignorada"
    public string? Categoria { get; set; }
    public DateTime ImportadoEm { get; set; }

    public ContaBancaria ContaBancaria { get; set; } = null!;
}
