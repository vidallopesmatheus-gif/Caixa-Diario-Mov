using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Registros;

public class CriarRegistroDto
{
    [Required] public Guid ClienteId { get; set; }
    [Required] public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public decimal Entrada { get; set; }
    public List<ItemFinanceiroDto> Saidas { get; set; } = new();
    public List<ItemFinanceiroDto> ContasReceber { get; set; } = new();
    public List<ItemFinanceiroDto> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
}
