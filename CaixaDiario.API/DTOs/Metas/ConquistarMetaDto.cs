using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Metas;

/// <summary>Fase 5: "🏆 Conquistei" — registra a compra de verdade por trás de uma meta.</summary>
public class ConquistarMetaDto
{
    // "LancarAgora" (padrão): o app lança a compra. "JaLancado": a compra já está no app (ou vai
    // vir na fatura) — só vincula ParcelamentoIds/Lancamentos à meta, nunca cria lançamento.
    public string Modo { get; set; } = "LancarAgora";
    public decimal ValorPago { get; set; }
    [Required] public DateOnly Data { get; set; } // data da compra
    // "AVista" | "Cartao" | "Boleto" | "Financiado" (Financiado só habilita após a Fase 6).
    [Required] public string FormaPagamento { get; set; } = string.Empty;
    // 1–48; só relevante para Cartao/Boleto.
    public int? Parcelas { get; set; }
    public DateOnly? DataPrimeiraParcela { get; set; }
    // Guid.Empty é aceito só no modo "JaLancado" (a conta vem dos próprios itens vinculados).
    public Guid ContaPagamentoId { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    // Só tem efeito quando a meta tem ContaInvestimentoId e a forma é "AVista" — confirma se o
    // usuário quer resgatar o valor pago do investimento pra conta de pagamento (fora do DRE).
    public bool ResgatarDoInvestimento { get; set; }
    // Cartão / "LancarAgora": banco da fatura (Nubank, C6...) e valor real de cada parcela.
    public string? Banco { get; set; }
    public decimal? ValorParcela { get; set; }
    // Modo "JaLancado".
    public List<Guid> ParcelamentoIds { get; set; } = new();
    public List<VincularLancamentoMetaDto> Lancamentos { get; set; } = new();
}

/// <summary>Lançamento já existente vinculado à meta. NumeroParcela/TotalParcelas só quando o
/// usuário diz "é a parcela N de M" de um lançamento que a detecção ainda não reconheceu — aí o
/// Parcelamento é criado a partir dele.</summary>
public class VincularLancamentoMetaDto
{
    public Guid LancamentoId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public DateOnly Data { get; set; }
    public int? NumeroParcela { get; set; }
    public int? TotalParcelas { get; set; }
}
