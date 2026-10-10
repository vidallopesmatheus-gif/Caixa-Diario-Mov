using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Financiamentos;

public class CriarFinanciamentoDto
{
    [Required] public string Descricao { get; set; } = string.Empty;
    // "Financiamento" | "Emprestimo" | "Consorcio" | "Parcelamento"
    [Required] public string Tipo { get; set; } = "Financiamento";
    public decimal ValorBem { get; set; }
    public decimal ValorEntrada { get; set; }
    public int NumeroParcelas { get; set; }
    // Se informado e maior que a parcela pura calculada, a diferença por parcela entra como
    // Encargos (seguro/tarifa embutidos no contrato) — ver CalculadoraAmortizacao.
    public decimal? ValorParcela { get; set; }
    // Se não informado (e Sistema == "Price"), é resolvido via taxa implícita.
    public decimal? TaxaJurosMensal { get; set; }
    // "Price" | "SAC" | "SemJuros"
    [Required] public string Sistema { get; set; } = "Price";
    [Required] public DateOnly DataPrimeiraParcela { get; set; }
    [Required] public Guid ContaBancariaId { get; set; }
    public string? BemVinculado { get; set; }
    public Guid? OrigemMetaId { get; set; }
}
