namespace CaixaDiario.API.DTOs.Importacao;

// Fase 1.7: transação do arquivo que bate (mesma conta, mesmo sentido, valor ±R$0,01, data ±2
// dias) com um lançamento real já existente sem FitId. Duas situações diferentes:
// - Candidato SEM histórico em TransacaoImportada => é um lançamento MANUAL de verdade
//   (ver DuplicatasManuais/DuplicataManualDto) — oferece Mesclar/Importar como novo.
// - Candidato COM histórico em TransacaoImportada => veio de uma importação anterior (ex.: CSV
//   de um banco + OFX do mesmo banco) — só sinaliza para revisão, sempre importa (ver
//   DuplicatasEntreArquivos).
public class DuplicataManualDto
{
    public int TransacaoIndice { get; set; }
    public string DescricaoBanco { get; set; } = string.Empty;
    public string DataBanco { get; set; } = string.Empty; // ISO "yyyy-MM-dd"
    public decimal Valor { get; set; }
    public string Tipo { get; set; } = string.Empty; // "Entrada" | "Saida"
    public Guid LancamentoManualId { get; set; }
    public string DescricaoManual { get; set; } = string.Empty;
    public string DataManual { get; set; } = string.Empty; // ISO "yyyy-MM-dd"
}

public class DuplicataEntreArquivosDto
{
    public int TransacaoIndice { get; set; }
    public string DescricaoBanco { get; set; } = string.Empty;
    public string DataBanco { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string DescricaoJaImportada { get; set; } = string.Empty;
    public string DataJaImportada { get; set; } = string.Empty;
}
