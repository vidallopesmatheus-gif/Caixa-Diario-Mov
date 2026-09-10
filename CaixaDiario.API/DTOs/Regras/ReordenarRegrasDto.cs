using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Regras;

public class ReordenarRegrasDto
{
    [Required] public List<Guid> Ids { get; set; } = new();
}
