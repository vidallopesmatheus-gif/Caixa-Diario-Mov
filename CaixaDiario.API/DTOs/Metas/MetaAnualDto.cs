namespace CaixaDiario.API.DTOs.Metas;

public class MetaAnualDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public int Ano { get; set; }
    public decimal MetaReceita { get; set; }
    public decimal MetaLucro { get; set; }
    public int MesInicio { get; set; }
    public int PeriodoMeses { get; set; }
    public DateTime SalvoEm { get; set; }
    public DateTime CriadoEm { get; set; }
    public string? Sonho { get; set; }
    public string ModoMeta { get; set; } = "simples";
    public decimal ValorSonho { get; set; }
    public int PrazoAnos { get; set; }
    public decimal TaxaRetorno { get; set; }
    public decimal TotalInvestido { get; set; }
    public decimal? MargemPJ { get; set; }
    public string? IconeSonho { get; set; }
    public Guid? ContaInvestimentoId { get; set; }
    public DateOnly? DataAlvo { get; set; }

    // Fase 5: "🏆 Conquistei"
    public string Status { get; set; } = "EmAndamento";
    public DateOnly? DataConquista { get; set; }
    public decimal? ValorPago { get; set; }
    public string? FormaPagamento { get; set; }
    public int? Parcelas { get; set; }
    public DateOnly? DataPrimeiraParcela { get; set; }
    public Guid? ContaPagamentoId { get; set; }
    public string? Categoria { get; set; }
    public string? Observacao { get; set; }
    public Guid? TransferenciaResgateId { get; set; }
    public Guid? FinanciamentoId { get; set; }
    // Item 5 (Money Vision): "Ativo" | "Quitado" do contrato (Models/Financiamento.cs), só
    // preenchido quando FinanciamentoId existe — permite o front mostrar "Quitar <bem>" em vez de
    // só "Conquistada" enquanto o financiamento ainda está em aberto.
    public string? FinanciamentoStatus { get; set; }
}
