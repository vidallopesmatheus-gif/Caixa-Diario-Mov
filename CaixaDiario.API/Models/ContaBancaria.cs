namespace CaixaDiario.API.Models;

public class ContaBancaria
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    // "Caixa" | "ContaCorrente" | "Investimento" | "CartaoCredito"
    public string Tipo { get; set; } = "Caixa";
    public decimal SaldoInicial { get; set; } = 0m;
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; }

    // Só preenchidos quando Tipo == "CartaoCredito". Saldo do cartão é de natureza passiva (dívida) —
    // ver FaturaCartaoService.ObterCompetencia pra como DiaFechamento decide em qual fatura uma
    // compra cai.
    public decimal? Limite { get; set; }
    public int? DiaFechamento { get; set; }
    public int? DiaVencimento { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ICollection<RegistroDiario> Registros { get; set; } = new List<RegistroDiario>();
}
