namespace CaixaDiario.API.Models;

public class RegistroDiario
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public decimal Entrada { get; set; }
    public List<ItemFinanceiro> Saidas { get; set; } = new();
    public List<ItemFinanceiro> ContasReceber { get; set; } = new();
    public List<ItemFinanceiro> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
    public bool Excluido { get; set; } = false;
    public string? MotivoExclusao { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime SalvoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public string? UsuarioAtualizacao { get; set; }

    public Usuario Cliente { get; set; } = null!;
}
