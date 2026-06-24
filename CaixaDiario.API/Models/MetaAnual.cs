namespace CaixaDiario.API.Models;

public class MetaAnual
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public int Ano { get; set; }
    public decimal MetaReceita { get; set; }
    public decimal MetaLucro { get; set; }
    public int MesInicio { get; set; } = 1;
    public int PeriodoMeses { get; set; } = 12;
    // Metas & Investimentos
    public string? Sonho { get; set; }
    public string ModoMeta { get; set; } = "simples";
    public decimal ValorSonho { get; set; }
    public int PrazoAnos { get; set; }
    public decimal TaxaRetorno { get; set; }
    public decimal TotalInvestido { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
}
