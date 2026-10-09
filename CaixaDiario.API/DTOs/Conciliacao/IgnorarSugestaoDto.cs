namespace CaixaDiario.API.DTOs.Conciliacao;

// Item 3.3: corpo do POST "ignorar sugestão de vínculo".
public class IgnorarSugestaoDto
{
    public Guid ContaProvisionadaId { get; set; }
    public Guid LancamentoId { get; set; }
}
