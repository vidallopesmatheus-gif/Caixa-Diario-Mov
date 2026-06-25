namespace CaixaDiario.API.DTOs.OrcamentoDinamico;

public class OrcamentoDinamicoDto
{
    public decimal ReceitaEsperada { get; set; }
    public decimal CompromissosFixos { get; set; }
    public decimal AporteNecessario { get; set; }
    public decimal SaldoLivre { get; set; }
    public decimal GastoVariavelAtual { get; set; }
    public bool Ultrapassado { get; set; }
    public decimal PercentualUtilizado { get; set; }
}
