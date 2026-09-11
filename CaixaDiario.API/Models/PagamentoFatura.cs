namespace CaixaDiario.API.Models;

// Registra o vínculo entre uma saída já existente na conta corrente e a fatura de cartão que ela
// paga (total ou parcialmente) — nunca cria a saída do zero, só reclassifica uma que já está no
// extrato (mesmo espírito de Transferencia/ConverterLancamentoAsync). ValorPago vem sempre do
// Valor do próprio lançamento original: o que saiu do banco É o que foi pago.
//
// Uma fatura pode ter mais de um pagamento (pagamento parcial em duas vezes, por exemplo) — por
// isso não há um campo "fatura paga por inteiro"; o saldo devedor da competência é sempre
// recalculado como ValorTotal das compras da competência menos a soma de ValorPago vinculados a
// ela (ver FaturaCartaoService.ListarFaturasAsync).
public class PagamentoFatura
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ContaCartaoId { get; set; }
    public Guid ContaOrigemId { get; set; }
    // "yyyy-MM" — mês de fechamento da fatura sendo paga (ver FaturaCartaoService.ObterCompetencia).
    public string Competencia { get; set; } = string.Empty;
    public decimal ValorPago { get; set; }
    public DateOnly Data { get; set; }
    public DateTime CriadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ContaBancaria ContaCartao { get; set; } = null!;
    public ContaBancaria ContaOrigem { get; set; } = null!;
}
