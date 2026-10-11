using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.FaturasCartao;

/// <summary>
/// Item F4 do bundle D6JWEAJa: paga uma fatura direto da tela de Faturas, sem precisar de um
/// lançamento já existente pra reclassificar (diferente de VincularPagamentoFaturaDto) — cria a
/// saída na conta de origem e a entrada no cartão do zero, como ConverterLancamentoAsync faz com
/// transferências "do zero" (ver TransferenciaService.CriarAsync).
/// </summary>
public class PagarFaturaDto
{
    [Required] public Guid ContaOrigemId { get; set; }
    [Required] public DateOnly Data { get; set; }
    [Required] public string Competencia { get; set; } = string.Empty; // "yyyy-MM"
    // Se omitido, usa o saldo devedor atual da fatura (pagamento integral).
    public decimal? Valor { get; set; }
}
