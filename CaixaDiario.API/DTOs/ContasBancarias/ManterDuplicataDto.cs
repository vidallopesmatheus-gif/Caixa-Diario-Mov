namespace CaixaDiario.API.DTOs.ContasBancarias;

// Item 3.2: corpo do POST "manter os dois" — registra que o par não é duplicata de verdade.
public class ManterDuplicataDto
{
    public Guid LancamentoAId { get; set; }
    public Guid LancamentoBId { get; set; }
}
