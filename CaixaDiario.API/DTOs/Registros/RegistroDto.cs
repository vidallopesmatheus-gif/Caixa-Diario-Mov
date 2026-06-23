namespace CaixaDiario.API.DTOs.Registros;

public class RegistroDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public List<ItemFinanceiroDto> Entradas { get; set; } = new();
    public List<ItemFinanceiroSaidaDto> Saidas { get; set; } = new();
    public List<ContaProvisionadaDto> ContasReceber { get; set; } = new();
    public List<ContaProvisionadaDto> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
    public DateTime SalvoEm { get; set; }
}
