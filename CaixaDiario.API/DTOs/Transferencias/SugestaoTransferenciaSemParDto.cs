namespace CaixaDiario.API.DTOs.Transferencias;

// Money Vision — Bloco 4, item 4 da Parte 2 ("Transferências sem par"): lançamento que a
// importação (dicionário de palavras-chave) já suspeita ser transferência ("Aplicação RDB",
// "Resgate", "Caixinha"...) mas que ainda não foi confirmado como uma Transferencia de verdade —
// fica pendente, com Categoria "Transferência" só como sugestão (ver ImportacaoService).
public class SugestaoTransferenciaSemParDto
{
    public Guid ContaBancariaId { get; set; }
    public string ContaBancariaNome { get; set; } = string.Empty;
    public Guid LancamentoId { get; set; }
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; } // positivo = entrada, negativo = saída
    public string Tipo { get; set; } = string.Empty; // "Entrada" | "Saida"
    // Preenchidos só quando dá pra sugerir uma conta destino com confiança (hoje: cliente tem
    // exatamente 1 conta de Investimento ativa, diferente da conta de origem do lançamento).
    public Guid? ContaDestinoSugeridaId { get; set; }
    public string? ContaDestinoSugeridaNome { get; set; }
}
