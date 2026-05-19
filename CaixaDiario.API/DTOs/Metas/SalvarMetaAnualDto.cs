using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Metas;

public class SalvarMetaAnualDto
{
    [Required] public Guid ClienteId { get; set; }
    [Required, Range(2000, 2100)] public int Ano { get; set; }
    [Range(0, double.MaxValue)] public decimal MetaReceita { get; set; }
    [Range(0, double.MaxValue)] public decimal MetaLucro { get; set; }
}
