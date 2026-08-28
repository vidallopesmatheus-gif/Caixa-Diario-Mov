namespace CaixaDiario.API.DTOs.Importacao;

/// <summary>Uma transação encontrada no arquivo antes de qualquer persistência — só pré-visualização.</summary>
public class PreviewTransacaoDto
{
    public int Indice { get; set; }                    // posição no arquivo — usado pra "forçar inclusão" na confirmação
    public string Data { get; set; } = string.Empty;   // ISO "yyyy-MM-dd"
    public decimal Valor { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;   // "Entrada" | "Saida"
    public string? FitId { get; set; }
    // OFX: mesmo FITID já importado antes nesta conta. CSV/XLSX: mesma heurística de sempre
    // (conta + data + valor + descrição similar) contra o histórico de importações da conta.
    public bool JaImportada { get; set; }
}

public class PreviewImportacaoDto
{
    public List<PreviewTransacaoDto> Transacoes { get; set; } = new();
}
