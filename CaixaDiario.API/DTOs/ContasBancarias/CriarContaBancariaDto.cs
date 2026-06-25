using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.ContasBancarias;

public class CriarContaBancariaDto
{
    [Required] public Guid ClienteId { get; set; }
    [Required, MaxLength(100)] public string Nome { get; set; } = string.Empty;
    [Required] public string Tipo { get; set; } = "Caixa";
    public decimal SaldoInicial { get; set; } = 0m;
}
