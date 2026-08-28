namespace CaixaDiario.API.Models;

public class Transferencia
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ContaOrigemId { get; set; }
    public Guid ContaDestinoId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Valor { get; set; }
    public string? Descricao { get; set; }
    public DateTime CriadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ContaBancaria ContaOrigem { get; set; } = null!;
    public ContaBancaria ContaDestino { get; set; } = null!;
}
