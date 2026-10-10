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
    public decimal? MargemPJ { get; set; }
    public string? IconeSonho { get; set; }
    // Data real escolhida pelo usuário pra atingir o objetivo (modo "metodo"). Substitui o
    // PrazoAnos relativo como fonte de verdade — PrazoAnos fica só como fallback de leitura
    // pra metas antigas que ainda não passaram pelo backfill.
    public DateOnly? DataAlvo { get; set; }
    // Conta de investimento cujo saldo alimenta automaticamente o progresso desta meta.
    public Guid? ContaInvestimentoId { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    // ── Fase 5: "🏆 Conquistei" ──────────────────────────────────────────────────────────────
    // "EmAndamento" | "Conquistada" | "Cancelada". Metas antigas nascem "EmAndamento" (migration).
    public string Status { get; set; } = "EmAndamento";
    public DateOnly? DataConquista { get; set; }
    public decimal? ValorPago { get; set; }
    // "AVista" | "Cartao" | "Boleto" | "Financiado" — só preenchido quando Status == "Conquistada".
    public string? FormaPagamento { get; set; }
    public int? Parcelas { get; set; }
    public DateOnly? DataPrimeiraParcela { get; set; }
    // Conta (ou cartão) de ONDE SAI o dinheiro da conquista — diferente de ContaInvestimentoId,
    // que é de ONDE o progresso é lido/de onde vem o resgate.
    public Guid? ContaPagamentoId { get; set; }
    public string? Categoria { get; set; }
    public string? Observacao { get; set; }
    // "Desfazer conquista" encontra os lançamentos/contas a pagar gerados pela tag OrigemMetaId
    // (ver ItemFinanceiro/ItemFinanceiroSaida/ContaProvisionada) — mesmo padrão de TransferenciaId/
    // PagamentoFaturaId/ParcelamentoId, nunca uma lista de Ids guardada aqui (que ficaria
    // desatualizada se algum item fosse editado/excluído por fora). Só a Transferência de resgate
    // (singular, não tem um "tipo" próprio de tag hoje) e o Financiamento ficam guardados direto.
    public Guid? TransferenciaResgateId { get; set; }
    public Guid? FinanciamentoId { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ContaBancaria? ContaInvestimento { get; set; }
    public ContaBancaria? ContaPagamento { get; set; }
}
