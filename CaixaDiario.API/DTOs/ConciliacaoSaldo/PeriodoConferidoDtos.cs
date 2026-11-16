namespace CaixaDiario.API.DTOs.ConciliacaoSaldo;

public class ReabrirPeriodoDto
{
    // Null = reabrir tudo. Senão a trava passa a ser o dia anterior a esta data.
    public DateOnly? APartirDe { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Uma linha do histórico de conferências e reaberturas da conta.</summary>
public class ConferenciaSaldoDto
{
    public string Tipo { get; set; } = string.Empty;
    public DateOnly? Data { get; set; }
    public decimal? SaldoBanco { get; set; }
    public DateOnly? TravadoAteAntes { get; set; }
    public DateOnly? TravadoAteDepois { get; set; }
    // "Consultor" | "Cliente"
    public string Quem { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime OcorridoEm { get; set; }
}

/// <summary>Trava vigente da conta, devolvida depois de reabrir.</summary>
public class TravaPeriodoDto
{
    public DateOnly? TravadoAte { get; set; }
    public decimal? TravaSaldoBanco { get; set; }
    public bool TravadoPorConsultor { get; set; }
}

/// <summary>Lançamento criado, editado ou excluído no período desde a última conferência.</summary>
public class MudancaDesdeConferenciaDto
{
    // "Criado" | "Editado" | "Excluido"
    public string Tipo { get; set; } = string.Empty;
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    // Com sinal (entrada +, saída −).
    public decimal? ValorAntes { get; set; }
    public decimal? ValorDepois { get; set; }
}
