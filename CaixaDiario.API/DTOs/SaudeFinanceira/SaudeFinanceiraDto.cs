namespace CaixaDiario.API.DTOs.SaudeFinanceira;

public class GaugeIndicadorDto
{
    public string Titulo { get; set; } = "";
    /// <summary>Valor bruto (pode ser &gt;100 para RitmoMeta quando adiantado).</summary>
    public decimal Valor { get; set; }
    /// <summary>Valor normalizado 0-100 para o arco do gauge.</summary>
    public decimal ValorNormalizado { get; set; }
    /// <summary>"verde" | "amarelo" | "vermelho" | "cinza"</summary>
    public string Semaforo { get; set; } = "cinza";
    public string Descricao { get; set; } = "";
    public string Calculo { get; set; } = "";
    public bool Disponivel { get; set; }
    /// <summary>Só para RitmoMeta: "Atingida" | "Adiantado" | "No ritmo" | "Atrasado".</summary>
    public string? StatusRitmo { get; set; }
    /// <summary>Só para RitmoMeta: investido − esperado linear até hoje (negativo = atrasado).</summary>
    public decimal? DiferencaReais { get; set; }
}

public class SaudeFinanceiraDto
{
    /// <summary>Mês/ano usados no cálculo (ex.: "Setembro/2026 · último mês fechado") — Taxa de
    /// Poupança e Comprometimento Fixo são indicadores mensais, calculados sobre o último mês
    /// INTEIRO já fechado (não o mês corrente, que ainda não tem todos os lançamentos), e
    /// independente do período escolhido no seletor do Dashboard. Exibido explicitamente pra não
    /// parecer "zerado" nem divergir sem explicação da Margem do período logo ao lado.</summary>
    public string Periodo { get; set; } = "";
    public GaugeIndicadorDto TaxaPoupanca { get; set; } = new();
    public GaugeIndicadorDto ComprometimentoFixos { get; set; } = new();
    public GaugeIndicadorDto RitmoMeta { get; set; } = new();
}
