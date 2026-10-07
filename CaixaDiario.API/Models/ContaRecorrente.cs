namespace CaixaDiario.API.Models;

public class ContaRecorrente
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public string Tipo { get; set; } = string.Empty;  // "Receber" | "Pagar"
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    // "Semanal" | "Quinzenal" | "Mensal" | "Trimestral" | "Semestral" | "Anual"
    public string Periodicidade { get; set; } = "Mensal";
    // Limita o nº total de ocorrências; se null, recorre até DataFim (ou indefinidamente).
    public int? QuantidadeParcelas { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    // Obrigatória — toda ocorrência materializada é gravada no registro DAQUELA conta. (Até a
    // Fase 0.2 era opcional; migration backfill atribuiu a conta padrão do cliente a quem não
    // tinha.)
    public Guid ContaBancariaId { get; set; }
    // Fase 1.1: quando true, o valor previsto de cada ocorrência nova é a média do valor efetivo
    // das últimas 3 ocorrências pagas (não o Valor cadastrado, que aqui só vale como fallback sem
    // histórico) — ver RecorrenciaService.CalcularValorPrevisto.
    public bool ValorVariavel { get; set; }
    // Override explícito do dia de vencimento (1-31), independente de DataInicio (que é só quando
    // a recorrência COMEÇOU). Null = comportamento histórico, deriva de DataInicio.Day.
    public int? DiaVencimento { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ContaBancaria ContaBancaria { get; set; } = null!;
}
