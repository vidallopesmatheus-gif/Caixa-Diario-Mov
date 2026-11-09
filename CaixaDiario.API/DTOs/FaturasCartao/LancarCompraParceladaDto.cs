using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.FaturasCartao;

/// <summary>Item 9: compra parcelada lançada direto numa conta CartaoCredito pelo Caixa.</summary>
public class LancarCompraParceladaDto
{
    [Required] public string Descricao { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    [Required] public string Categoria { get; set; } = string.Empty;
    public int QuantidadeParcelas { get; set; }
    // Data da compra — é onde a parcela 1 entra (igual vem na fatura real do banco).
    [Required] public DateOnly Data { get; set; }
    // Vencimento da fatura da 1ª parcela; as demais vencem um mês depois, cada uma. Sem ele, usa o
    // próximo vencimento calculado pelo DiaFechamento/DiaVencimento do cartão.
    public DateOnly? DataPrimeiraParcela { get; set; }
    // Emissor da fatura (Nubank, C6...) — a parcela entra na fatura real desse banco, nunca numa
    // fatura sem banco. Opcional só quando o cartão tem um único banco já usado.
    public string? Banco { get; set; }
    // Valor real de cada parcela, quando o usuário informa — senão ValorTotal ÷ N, com o ajuste de
    // centavos na última.
    public decimal? ValorParcela { get; set; }
}
