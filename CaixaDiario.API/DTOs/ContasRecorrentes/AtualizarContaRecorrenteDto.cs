namespace CaixaDiario.API.DTOs.ContasRecorrentes;

public class AtualizarContaRecorrenteDto
{
    public string? Descricao { get; set; }
    public decimal? Valor { get; set; }
    public string? Categoria { get; set; }
    public DateOnly? DataFim { get; set; }
}
