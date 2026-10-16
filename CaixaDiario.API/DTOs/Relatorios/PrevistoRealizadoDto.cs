namespace CaixaDiario.API.DTOs.Relatorios;

// Item 7 da rodada de ajustes Money Vision: "Previsto × Realizado" refeito por ITEM (favorecido),
// não mais por categoria — o objetivo é descobrir furos ("eu previa receber 600 e recebi 500"), e
// uma categoria como "Salários/Folha" mistura vários favorecidos diferentes, cada um com seu
// próprio padrão. Por padrão só títulos ligados a uma recorrência (RecorrenciaId != null) entram,
// porque só eles têm um "previsto" de verdade (o Valor materializado já é o valor do extrato
// recalculado — ver RecorrenciaService.CalcularValorPrevisto); títulos avulsos vinculados a um
// lançamento existente também entram quando incluirAvulsosVinculados=true.
public class PrevistoRealizadoDto
{
    public List<ItemPrevistoRealizadoDto> Itens { get; set; } = new();
    // Acumulado dia a dia (quando meses=1, "mês atual") ou mês a mês (quando meses>1) — um array
    // pra Receber, outro pra Pagar, pro gráfico "previsto acumulado × realizado acumulado" trocar
    // de tipo sem precisar de uma nova chamada à API.
    public List<PontoAcumuladoPrevistoRealizadoDto> AcumuladoReceber { get; set; } = new();
    public List<PontoAcumuladoPrevistoRealizadoDto> AcumuladoPagar { get; set; } = new();
}

public class ItemPrevistoRealizadoDto
{
    // Tipo + descrição normalizada — identifica o favorecido de forma estável, usado por
    // ObterHistoricoItemAsync ("tocar num item mostra o histórico dele").
    public string Chave { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty; // "Receber" | "Pagar"
    public string? Categoria { get; set; }
    public decimal Previsto { get; set; }
    public decimal Realizado { get; set; }
    public decimal Diferenca { get; set; } // Realizado - Previsto
    public decimal? DiferencaPercentual { get; set; }
    // "Realizado" | "Parcial" | "NaoAconteceu" | "Atrasado"
    public string Status { get; set; } = string.Empty;
    public DateOnly? DataVencimento { get; set; }
}

public class PontoAcumuladoPrevistoRealizadoDto
{
    // "yyyy-MM-dd" (mês atual, meses=1) ou "yyyy-MM" (3/6/12 meses).
    public string Data { get; set; } = string.Empty;
    public decimal PrevistoAcumulado { get; set; }
    public decimal RealizadoAcumulado { get; set; }
}

// Histórico de um único favorecido nos últimos meses — "o furo é recorrente, ou foi só este mês?".
public class HistoricoItemPrevistoRealizadoDto
{
    public string Nome { get; set; } = string.Empty;
    public List<PontoPrevistoRealizadoDto> Meses { get; set; } = new();
}

public class PontoPrevistoRealizadoDto
{
    public string Mes { get; set; } = string.Empty; // "yyyy-MM"
    public decimal Previsto { get; set; }
    public decimal Realizado { get; set; }
    public decimal? VariacaoPercentual { get; set; }
}
