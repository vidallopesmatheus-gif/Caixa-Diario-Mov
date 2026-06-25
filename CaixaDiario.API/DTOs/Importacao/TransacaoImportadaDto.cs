namespace CaixaDiario.API.DTOs.Importacao;

public class TransacaoImportadaDto
{
    public Guid Id { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string Data { get; set; } = string.Empty;      // ISO "yyyy-MM-dd"
    public decimal Valor { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;       // "Entrada" | "Saida"
    public string Status { get; set; } = "Pendente";
    public string? Categoria { get; set; }
    public string? FitId { get; set; }
    public bool Duplicada { get; set; }                    // alerta de possível duplicata
}
