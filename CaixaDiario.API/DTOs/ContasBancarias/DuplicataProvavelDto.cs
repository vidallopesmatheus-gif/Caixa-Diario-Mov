namespace CaixaDiario.API.DTOs.ContasBancarias;

// Fase 0.5: par de lançamentos reais (mesma conta, mesmo sentido) que provavelmente são a mesma
// transação importada duas vezes em formatos/arquivos diferentes — só sugere, nunca exclui
// sozinho. Confirmar a exclusão usa o mesmo endpoint de sempre (excluir-lancamento).
public class DuplicataProvavelDto
{
    public string Tipo { get; set; } = string.Empty; // "Entrada" | "Saida"
    public int Score { get; set; } // 0-100, confiança da descrição ser a mesma transação

    public Guid LancamentoAId { get; set; }
    public string DescricaoA { get; set; } = string.Empty;
    public string DataA { get; set; } = string.Empty; // ISO "yyyy-MM-dd"
    public decimal ValorA { get; set; }

    public Guid LancamentoBId { get; set; }
    public string DescricaoB { get; set; } = string.Empty;
    public string DataB { get; set; } = string.Empty;
    public decimal ValorB { get; set; }
}
