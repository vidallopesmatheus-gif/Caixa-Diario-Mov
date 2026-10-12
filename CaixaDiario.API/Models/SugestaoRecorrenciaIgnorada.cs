namespace CaixaDiario.API.Models;

// Item 2 do Bloco 4 ("Visão de Futuro"): registra que o cliente clicou "Ignorar" num PADRÃO
// detectado no histórico (não uma ocorrência específica) — sem isso, a mesma sugestão reapareceria
// em toda nova chamada, porque o detector não tinha memória de decisões anteriores (mesmo espírito
// de SugestaoVinculoIgnorada, da Conciliação).
public class SugestaoRecorrenciaIgnorada
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    // "Receber:DESCRICAO NORMALIZADA" ou "Pagar:DESCRICAO NORMALIZADA" — ver
    // DetectorRecorrenciaService.MontarChave.
    public string ChaveNormalizada { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}
