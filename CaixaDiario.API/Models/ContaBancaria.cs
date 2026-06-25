namespace CaixaDiario.API.Models;

public class ContaBancaria
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    // "Caixa" | "ContaCorrente" | "Investimento"
    public string Tipo { get; set; } = "Caixa";
    public decimal SaldoInicial { get; set; } = 0m;
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ICollection<RegistroDiario> Registros { get; set; } = new List<RegistroDiario>();
}
