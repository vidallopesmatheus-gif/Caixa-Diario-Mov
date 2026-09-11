namespace CaixaDiario.API.DTOs.ContasBancarias;

public class ContaBancariaDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public decimal SaldoInicial { get; set; }
    public decimal SaldoAtual { get; set; }
    public decimal EntradasMes { get; set; }
    public decimal SaidasMes { get; set; }
    public int PendentesCategorizacao { get; set; }
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }

    // Só preenchidos quando Tipo == "Investimento"
    public decimal? TotalAportado { get; set; }
    public decimal? RendimentoAcumulado { get; set; }
    public decimal? RentabilidadePercentual { get; set; }
    public List<MetaVinculadaDto>? MetasVinculadas { get; set; }
    public decimal? ProgressoCombinadoPercentual { get; set; }

    // Só preenchidos quando Tipo == "CartaoCredito". SaldoDevedor é sempre >= 0 (valor absoluto da
    // dívida) mesmo que SaldoAtual seja negativo — mais fácil de exibir direto no card.
    public decimal? Limite { get; set; }
    public int? DiaFechamento { get; set; }
    public int? DiaVencimento { get; set; }
    public decimal? SaldoDevedor { get; set; }
    public decimal? LimiteDisponivel { get; set; }
}

public class MetaVinculadaDto
{
    public Guid Id { get; set; }
    public int Ano { get; set; }
    public string? Sonho { get; set; }
    public decimal ValorSonho { get; set; }
}
