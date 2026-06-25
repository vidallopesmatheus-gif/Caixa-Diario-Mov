namespace CaixaDiario.API.DTOs.ContasBancarias;

public class ContaBancariaDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public decimal SaldoInicial { get; set; }
    public decimal SaldoAtual { get; set; }
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
}
