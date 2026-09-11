using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.FaturasCartao;

/// <summary>
/// Reclassifica uma saída já existente na conta corrente como pagamento de uma fatura de cartão —
/// nunca cria o lançamento do zero (o dinheiro já saiu do banco). ValorPago não é informado: é
/// sempre o Valor do próprio lançamento original.
/// </summary>
public class VincularPagamentoFaturaDto
{
    [Required] public Guid ContaOrigemId { get; set; }   // conta corrente onde a saída está hoje
    [Required] public Guid LancamentoId { get; set; }
    [Required] public DateOnly Data { get; set; }
    [Required] public Guid ContaCartaoId { get; set; }
    [Required] public string Competencia { get; set; } = string.Empty; // "yyyy-MM"
}
