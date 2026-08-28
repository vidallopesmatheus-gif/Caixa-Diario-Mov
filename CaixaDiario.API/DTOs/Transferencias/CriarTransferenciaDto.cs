using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Transferencias;

public class CriarTransferenciaDto
{
    [Required] public Guid ClienteId { get; set; }
    [Required] public Guid ContaOrigemId { get; set; }
    [Required] public Guid ContaDestinoId { get; set; }
    [Required] public DateOnly Data { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Valor { get; set; }
    public string? Descricao { get; set; }
}
