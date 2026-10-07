namespace CaixaDiario.API.DTOs.Importacao;

// Fase 1.7: decisão do usuário sobre uma DuplicataManualDto do preview. Enviada de volta em
// ImportarArquivoAsync — índice ausente na lista equivale a "Mesclar" (o padrão).
public class ResolucaoDuplicataDto
{
    public int TransacaoIndice { get; set; }
    public string Acao { get; set; } = "Mesclar"; // "Mesclar" | "ImportarComoNovo"
}
