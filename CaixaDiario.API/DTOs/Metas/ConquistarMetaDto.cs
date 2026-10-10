using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Metas;

/// <summary>Fase 5: "🏆 Conquistei" — registra a compra de verdade por trás de uma meta.</summary>
public class ConquistarMetaDto
{
    [Required] public decimal ValorPago { get; set; }
    [Required] public DateOnly Data { get; set; } // data da compra
    // "AVista" | "Cartao" | "Boleto" | "Financiado" (Financiado só habilita após a Fase 6).
    [Required] public string FormaPagamento { get; set; } = string.Empty;
    // 1–48; só relevante para Cartao/Boleto.
    public int? Parcelas { get; set; }
    public DateOnly? DataPrimeiraParcela { get; set; }
    [Required] public Guid ContaPagamentoId { get; set; }
    [Required] public string Categoria { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    // Só tem efeito quando a meta tem ContaInvestimentoId e a forma é "AVista" — confirma se o
    // usuário quer resgatar o valor pago do investimento pra conta de pagamento (fora do DRE).
    public bool ResgatarDoInvestimento { get; set; }
}
