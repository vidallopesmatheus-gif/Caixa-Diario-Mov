using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Metas;

public class SalvarMetaAnualDto
{
    [Required] public Guid ClienteId { get; set; }
    [Required, Range(2000, 2100)] public int Ano { get; set; }
    [Range(0, double.MaxValue)] public decimal MetaReceita { get; set; }
    [Range(0, double.MaxValue)] public decimal MetaLucro { get; set; }
    [Range(1, 12)] public int MesInicio { get; set; } = 1;
    [Range(1, 12)] public int PeriodoMeses { get; set; } = 12;
    public string? Sonho { get; set; }
    public string ModoMeta { get; set; } = "simples";
    [Range(0, double.MaxValue)] public decimal ValorSonho { get; set; }
    [Range(0, 100)] public int PrazoAnos { get; set; }
    [Range(0, 100)] public decimal TaxaRetorno { get; set; }
    [Range(0, double.MaxValue)] public decimal TotalInvestido { get; set; }
    [Range(0, 100)] public decimal? MargemPJ { get; set; }
    public string? IconeSonho { get; set; }
}
