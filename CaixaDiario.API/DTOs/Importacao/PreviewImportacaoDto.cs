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
    // Das "novas", quantas vão conciliar com uma contrapartida provisória de Transferência em vez
    // de virar lançamento novo de verdade (já estão incluídas em TotalNovas, não somar de novo).
    public int TotalConciliarAoImportar { get; set; }
    public decimal TotalEntradas { get; set; }
    public decimal TotalSaidas { get; set; }
    // Menor/maior data do arquivo INTEIRO (sem aplicar dataInicio/dataFim) — usado só pra
    // pré-preencher o seletor de intervalo no frontend.
    public string DataInicioArquivo { get; set; } = string.Empty;
    public string DataFimArquivo { get; set; } = string.Empty;

    // Fase 1.7: únicas transações que o usuário efetivamente revisa linha a linha — o resto do
    // arquivo continua "tudo ou nada". Cada uma aqui precisa de uma decisão Mesclar/ImportarComoNovo
    // (padrão Mesclar) enviada de volta em ImportarArquivoAsync.
    public List<DuplicataManualDto> DuplicatasManuais { get; set; } = new();
    // Só informativo — sempre importa, mesmo sem decisão do usuário (ver ConciliacaoService).
    public List<DuplicataEntreArquivosDto> DuplicatasEntreArquivos { get; set; } = new();
}
