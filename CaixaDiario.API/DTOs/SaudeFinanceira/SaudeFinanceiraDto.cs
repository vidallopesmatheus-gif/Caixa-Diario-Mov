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
}

public class SaudeFinanceiraDto
{
    /// <summary>Mês/ano usados no cálculo (ex.: "Setembro/2026") — Taxa de Poupança e
    /// Comprometimento Fixo são sempre calculados sobre o mês corrente do servidor, não sobre o
    /// período escolhido no seletor do Dashboard. Exibido explicitamente pra não parecer "zerado".</summary>
    public string Periodo { get; set; } = "";
    public GaugeIndicadorDto TaxaPoupanca { get; set; } = new();
    public GaugeIndicadorDto ComprometimentoFixos { get; set; } = new();
    public GaugeIndicadorDto RitmoMeta { get; set; } = new();
}
