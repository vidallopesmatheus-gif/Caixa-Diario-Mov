namespace CaixaDiario.API.DTOs.Importacao;

/// <summary>
/// Resumo agregado do arquivo antes de importar — não lista transação por transação (o usuário
/// confirma "importar tudo", não escolhe linha a linha). A deduplicação (FITID ou heurística
/// data+valor+descrição) roda por baixo e só aparece aqui como contagem.
/// </summary>
public class PreviewImportacaoDto
{
    public int TotalEncontradas { get; set; }
    public int TotalJaImportadas { get; set; }
    public int TotalNovas { get; set; }
    public decimal TotalEntradas { get; set; }
    public decimal TotalSaidas { get; set; }
    // Menor/maior data do arquivo INTEIRO (sem aplicar dataInicio/dataFim) — usado só pra
    // pré-preencher o seletor de intervalo no frontend.
    public string DataInicioArquivo { get; set; } = string.Empty;
    public string DataFimArquivo { get; set; } = string.Empty;
}
