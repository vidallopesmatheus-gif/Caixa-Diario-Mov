using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Registros;

public class ExcluirRegistroDto
{
    [Required] public string MotivoExclusao { get; set; } = string.Empty;
}
