using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Categorias;

public class MigrarCategoriaDto
{
    [Required] public Guid ParaCategoriaId { get; set; }
}
