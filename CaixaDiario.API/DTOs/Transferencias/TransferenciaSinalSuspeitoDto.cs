namespace CaixaDiario.API.DTOs.Transferencias;

// Money Vision: "Pix recebido entrando como saída" — script de verificação (item 3). Nunca corrige
// sozinho (mudar de lista Entrada/Saída exigiria recalcular o saldo do dia inteiro em cadeia) — só
// lista pra revisão manual via as ferramentas que já existem (desfazer transferência + reclassificar).
public class TransferenciaSinalSuspeitoDto
{
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string ContaBancariaNome { get; set; } = string.Empty;
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    // "Saida (descricao sugere entrada)" | "Entrada (descricao sugere saida)"
    public string Motivo { get; set; } = string.Empty;
}
