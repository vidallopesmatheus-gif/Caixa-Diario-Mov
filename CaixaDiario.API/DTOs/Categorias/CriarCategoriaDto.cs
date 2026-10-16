using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Categorias;

public class CriarCategoriaDto
{
    [Required, MaxLength(100)] public string Nome { get; set; } = string.Empty;
    [Required] public Guid GrupoId { get; set; }
    // "Entrada" | "Saida" | "Ambos" — opcional; se omitido, usa o padrão derivado do Bloco do grupo.
    public string? TipoMovimento { get; set; }
}
