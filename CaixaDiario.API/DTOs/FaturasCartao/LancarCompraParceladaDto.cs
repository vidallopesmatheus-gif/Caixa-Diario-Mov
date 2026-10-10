using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.FaturasCartao;

/// <summary>Item 9: compra parcelada lançada direto numa conta CartaoCredito pelo Caixa.</summary>
public class LancarCompraParceladaDto
{
    [Required] public string Descricao { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    [Required] public string Categoria { get; set; } = string.Empty;
    public int QuantidadeParcelas { get; set; }
    // Data da compra = vencimento da 1ª parcela; as demais caem um mês depois, cada uma.
    [Required] public DateOnly Data { get; set; }
}
