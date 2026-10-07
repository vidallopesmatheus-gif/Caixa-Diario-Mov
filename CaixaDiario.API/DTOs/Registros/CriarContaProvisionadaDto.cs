using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Registros;

public class CriarContaProvisionadaDto
{
    [Required] public Guid ClienteId { get; set; }
    [Required] public Guid ContaBancariaId { get; set; }
    [Required] public string Tipo { get; set; } = string.Empty; // "Receber" | "Pagar"
    [Required] public string Descricao { get; set; } = string.Empty;
    [Required] public decimal Valor { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public string? Categoria { get; set; }
}
