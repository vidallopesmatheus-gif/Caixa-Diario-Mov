namespace CaixaDiario.API.Models;

public class ContaRecorrente
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public string Tipo { get; set; } = string.Empty;  // "Receber" | "Pagar"
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
}
