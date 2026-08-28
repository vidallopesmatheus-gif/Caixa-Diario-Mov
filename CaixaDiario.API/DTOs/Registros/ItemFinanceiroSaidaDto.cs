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
    public string? TipoCusto { get; set; }
    public Guid? TransferenciaId { get; set; }
    public Guid Id { get; set; }
    public string? FitId { get; set; }
    public bool PendenteCategorizacao { get; set; }
}
