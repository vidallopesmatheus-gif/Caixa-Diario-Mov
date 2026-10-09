namespace CaixaDiario.API.Models;

// Item 3.3: registra que o usuário clicou "Ignorar" numa sugestão de vínculo (ConciliacaoService)
// — sem isso, a mesma sugestão (título pendente × lançamento) reaparecia em toda nova busca,
// porque o motor não tinha memória de decisões anteriores (só recalculava do zero a cada chamada).
public class SugestaoVinculoIgnorada
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ContaProvisionadaId { get; set; }
    public Guid LancamentoId { get; set; }
    public DateTime CriadoEm { get; set; }
}
