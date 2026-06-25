namespace CaixaDiario.API.DTOs.Metas;

public class MetaAnualDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public int Ano { get; set; }
    public decimal MetaReceita { get; set; }
    public decimal MetaLucro { get; set; }
    public int MesInicio { get; set; }
    public int PeriodoMeses { get; set; }
    public DateTime SalvoEm { get; set; }
    public string? Sonho { get; set; }
    public string ModoMeta { get; set; } = "simples";
    public decimal ValorSonho { get; set; }
    public int PrazoAnos { get; set; }
    public decimal TaxaRetorno { get; set; }
    public decimal TotalInvestido { get; set; }
    public decimal? MargemPJ { get; set; }
    public string? IconeSonho { get; set; }
}
