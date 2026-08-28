namespace CaixaDiario.API.DTOs.ContasBancarias;

public class RegistrarRendimentoDto
{
    public DateOnly Data { get; set; }
    // Pode ser negativo (perda/desvalorização).
    public decimal Valor { get; set; }
    public string? Descricao { get; set; }
}
