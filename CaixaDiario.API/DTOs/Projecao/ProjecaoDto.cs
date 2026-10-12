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

// Item 3 da rodada "Visão de Futuro": uma linha por categoria de despesa fixa recorrente que o
// cliente nunca cadastrou como ContaRecorrente — cada uma pronta pra virar conta a pagar em 2
// cliques (botão na tela + confirmar no modal).
public class DespesaEstimadaCategoriaDto
{
    public string Categoria { get; set; } = string.Empty;
    public decimal ValorEstimado { get; set; }
}
