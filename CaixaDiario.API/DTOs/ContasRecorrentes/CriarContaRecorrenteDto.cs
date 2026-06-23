using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.ContasRecorrentes;

public class CriarContaRecorrenteDto
{
    [Required] public Guid ClienteId { get; set; }
    [Required] public string Descricao { get; set; } = string.Empty;
    [Required] public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    [Required] public string Tipo { get; set; } = string.Empty;
    [Required] public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string Periodicidade { get; set; } = "Mensal";
    public int? QuantidadeParcelas { get; set; }
}
