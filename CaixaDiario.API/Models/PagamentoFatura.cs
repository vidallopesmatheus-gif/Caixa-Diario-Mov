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
    // Mantido por compatibilidade com o código que ainda agrupa por competência (Blocos 2+ passam a
    // usar FaturaId); NÃO remover até FaturaCartaoService ser migrado por completo.
    public string Competencia { get; set; } = string.Empty;
    // Cartão (Bloco 1): liga o pagamento à Fatura real (ver Models/Fatura.cs). Nullable só durante a
    // transição — todo pagamento novo, a partir do Bloco 2, grava isso.
    public Guid? FaturaId { get; set; }
    public decimal ValorPago { get; set; }
    public DateOnly Data { get; set; }
    public DateTime CriadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ContaBancaria ContaCartao { get; set; } = null!;
    public ContaBancaria ContaOrigem { get; set; } = null!;
    public Fatura? Fatura { get; set; }
}
