namespace CaixaDiario.API.DTOs.Projecao;

public class ProjecaoItemDto
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    /// <summary>"Provisionado" = ContaReceber/ContaPagar manual | "Recorrente" = ContaRecorrente</summary>
    public string Origem { get; set; } = "Provisionado";
}

public class ProjecaoDiaDto
{
    public DateOnly Data { get; set; }
    public decimal SaldoInicio { get; set; }
    public List<ProjecaoItemDto> Entradas { get; set; } = new();
    public List<ProjecaoItemDto> Saidas { get; set; } = new();
    public decimal TotalEntradas { get; set; }
    public decimal TotalSaidas { get; set; }
    public decimal SaldoFim { get; set; }
    public bool SaldoNegativo { get; set; }
}

public class ProjecaoDto
{
    public decimal SaldoAtual { get; set; }
    public int TotalDias { get; set; }
    public List<ProjecaoDiaDto> Dias { get; set; } = new();
}
