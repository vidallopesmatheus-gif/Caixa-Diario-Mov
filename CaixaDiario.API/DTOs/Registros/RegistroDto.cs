namespace CaixaDiario.API.DTOs.Registros;

public class RegistroDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public decimal Entrada { get; set; }
    public List<ItemFinanceiroDto> Saidas { get; set; } = new();
    public List<ItemFinanceiroDto> ContasReceber { get; set; } = new();
    public List<ItemFinanceiroDto> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
    public DateTime SalvoEm { get; set; }
}
