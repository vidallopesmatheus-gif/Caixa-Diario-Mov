using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Registros;

public class ItemFinanceiroSaidaDto
{
    [Required]
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    [Required, MinLength(1)]
    public string Categoria { get; set; } = "Administrativas";
    public string? Subcategoria { get; set; }
}
