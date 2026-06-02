using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Registros;

public class CriarRegistroDto
{
    [Required] public Guid ClienteId { get; set; }
    [Required] public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public List<ItemFinanceiroDto> Entradas { get; set; } = new();
    public List<ItemFinanceiroSaidaDto> Saidas { get; set; } = new();
    public List<ContaProvisionadaDto> ContasReceber { get; set; } = new();
    public List<ContaProvisionadaDto> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
}
