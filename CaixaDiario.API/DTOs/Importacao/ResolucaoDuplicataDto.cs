namespace CaixaDiario.API.DTOs.Importacao;

// Fase 1.7: decisão do usuário sobre uma DuplicataManualDto do preview — índice ausente equivale
// a "Mesclar" (o padrão). Fase 3.1: o MESMO mecanismo também carrega a decisão sobre uma
// DuplicataEntreArquivosDto — índice ausente ali equivale a "Ignorar" (o padrão é NÃO importar
// uma provável duplicata entre arquivos, diferente da duplicata manual).
public class ResolucaoDuplicataDto
{
    public int TransacaoIndice { get; set; }
    public string Acao { get; set; } = "Mesclar"; // DuplicataManualDto: "Mesclar" | "ImportarComoNovo"
                                                   // DuplicataEntreArquivosDto: "Ignorar" | "Importar"
}
