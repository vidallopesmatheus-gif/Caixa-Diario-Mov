namespace CaixaDiario.API.Models;

public class RegistroDiario
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public List<ItemFinanceiro> Entradas { get; set; } = new();
    public List<ItemFinanceiroSaida> Saidas { get; set; } = new();
    public List<ContaProvisionada> ContasReceber { get; set; } = new();
    public List<ContaProvisionada> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
    public bool Excluido { get; set; } = false;
    public string? MotivoExclusao { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime SalvoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public string? UsuarioAtualizacao { get; set; }

    public Usuario Cliente { get; set; } = null!;
}
