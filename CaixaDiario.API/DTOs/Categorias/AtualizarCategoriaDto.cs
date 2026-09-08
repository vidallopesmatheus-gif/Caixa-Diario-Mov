using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Categorias;

public class AtualizarCategoriaDto
{
    [Required, MaxLength(100)] public string Nome { get; set; } = string.Empty;
    [Required] public Guid GrupoId { get; set; }
    public bool Ativa { get; set; } = true;
}
