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

// Bloco 3 da rodada "Visão de Futuro": uma linha por mês da tela "Meu Futuro".
public class MeuFuturoMesDto
{
    public string Mes { get; set; } = string.Empty; // "yyyy-MM"
    public decimal SaldoInicial { get; set; }
    public decimal AReceber { get; set; }
    public decimal APagar { get; set; }
    public decimal SaldoFinal { get; set; }
    // Quanto falta receber, ALÉM do que já está projetado, pra não ficar abaixo da reserva mínima.
    public decimal PrecisaReceber { get; set; }
    /// <summary>"Tranquilo" (≥ reserva mínima) | "Atencao" (0 ≤ saldo &lt; reserva) | "Risco" (saldo &lt; 0)</summary>
    public string Status { get; set; } = "Tranquilo";
}

public class MeuFuturoDto
{
    public List<MeuFuturoMesDto> Meses { get; set; } = new();
    // Regra-based, nunca IA — no máximo 3 frases (ver ProjecaoService.GerarConselhoFuturo).
    public List<string> Conselhos { get; set; } = new();
}
