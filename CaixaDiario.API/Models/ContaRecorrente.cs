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
    // Opcional — sem ela, a conta bancária só é definida na hora da baixa (comportamento
    // histórico). Quando definida, é propagada pras ocorrências materializadas.
    public Guid? ContaBancariaId { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ContaBancaria? ContaBancaria { get; set; }
}
