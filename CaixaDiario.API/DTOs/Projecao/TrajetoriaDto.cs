namespace CaixaDiario.API.DTOs.Projecao;

public class TrajetoriaPontoDto
{
    public string Mes { get; set; } = string.Empty; // "yyyy-MM"
    public decimal Saldo { get; set; }
}

public class TrajetoriaDto
{
    // Meses já fechados (ou o mês corrente, até hoje) com saldo realizado — nunca mais que
    // MesesHistoricoDisponiveis pontos, mesmo que mesesPassado peça mais.
    public List<TrajetoriaPontoDto> Historico { get; set; } = new();
    // Saldo projetado no fim de cada um dos próximos meses (mesma lógica de simulação da
    // Projeção de 30/60/90 dias, só que amostrada mês a mês em vez de dia a dia).
    public List<TrajetoriaPontoDto> Projetado { get; set; } = new();
    // Quantos meses de histórico de verdade existem (pode ser menor que o pedido) — o front usa
    // isso pra decidir se mostra aviso de "histórico curto".
    public int MesesHistoricoDisponiveis { get; set; }
    public decimal SaldoAtual { get; set; }
    // Variação de saldo no período — realizado (últimos N meses) e projetado (próximos N meses) —
    // pronta pra comparação direta nos cards de resumo.
    public decimal VariacaoRealizada { get; set; }
    public decimal VariacaoProjetada { get; set; }
}
