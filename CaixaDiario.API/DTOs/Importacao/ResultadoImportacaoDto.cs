namespace CaixaDiario.API.DTOs.Importacao;

/// <summary>Resumo do que foi efetivamente lançado ao confirmar uma importação.</summary>
public class ResultadoImportacaoDto
{
    public int TotalImportadas { get; set; }
    public int TotalPendentesCategorizacao { get; set; }
    public decimal TotalEntradas { get; set; }
    public decimal TotalSaidas { get; set; }
}
