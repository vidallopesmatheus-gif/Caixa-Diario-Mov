using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.ContasBancarias;

public class AtualizarContaBancariaDto
{
    [Required, MaxLength(100)] public string Nome { get; set; } = string.Empty;
    [Required] public string Tipo { get; set; } = "Caixa";
    public decimal SaldoInicial { get; set; }
    public bool Ativa { get; set; } = true;
}
