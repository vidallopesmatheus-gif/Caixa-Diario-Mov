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

    // Money Vision — Bloco 5, item 3 da Parte 3: resumo do MÊS (aportes/resgates/aporte líquido/
    // rendimento) e rentabilidade mês/ano — diferente de TotalAportado/RendimentoAcumulado acima,
    // que são desde o início da conta. Também só preenchidos quando Tipo == "Investimento".
    public decimal? AporteMesAtual { get; set; }
    public decimal? ResgateMesAtual { get; set; }
    public decimal? AporteLiquidoMesAtual { get; set; }
    public decimal? RendimentoMesAtual { get; set; }
    public decimal? RentabilidadeMesAtualPercentual { get; set; }
    public decimal? RentabilidadeAnoAtualPercentual { get; set; }
    // Data do lançamento de Rendimento mais recente — null quando nunca foi atualizado. O front
    // usa isso pro lembrete "atualize o saldo" quando passar de 30 dias.
    public DateOnly? UltimaAtualizacaoRendimento { get; set; }

    // Só preenchidos quando Tipo == "CartaoCredito". SaldoDevedor é sempre >= 0 (valor absoluto da
    // dívida) mesmo que SaldoAtual seja negativo — mais fácil de exibir direto no card.
    public decimal? Limite { get; set; }
    public int? DiaFechamento { get; set; }
    public int? DiaVencimento { get; set; }
    public decimal? SaldoDevedor { get; set; }
    public decimal? LimiteDisponivel { get; set; }
    // Money Vision: "card mostra R$ 0,00, mas a API e as faturas dizem outra coisa" (item 4) — a
    // soma do saldoDevedor de cada Fatura real não paga, a mesma fonte do botão "Pagar" na tela de
    // Faturas. Diferente de SaldoDevedor (ledger inteiro da conta): aqui é só o que ainda falta
    // pagar nas faturas já emitidas, nunca negativo por um pagamento que ultrapassou o devido.
    public decimal? FaturasEmAberto { get; set; }
    // "Este mês" do cartão: compras (saídas, exceto pagamento de fatura) e pagamentos (entradas
    // criadas por vincular/pagar fatura) — EntradasMes/SaidasMes genéricos excluem ambos por serem
    // "não operacionais" pro DRE, e sempre mostravam R$ 0,00 no card do cartão.
    public decimal? ComprasMes { get; set; }
    public decimal? PagamentosMes { get; set; }
}

public class MetaVinculadaDto
{
    public Guid Id { get; set; }
    public int Ano { get; set; }
    public string? Sonho { get; set; }
    public decimal ValorSonho { get; set; }
    // Money Vision — Bloco 5, item 5 da Parte 3: "no seu ritmo de aporte você chega lá em
    // <mês/ano>" — null quando não há ritmo de aporte positivo pra projetar (parado ou resgatando).
    public string? PrevisaoAtingimentoMesAno { get; set; }
}
