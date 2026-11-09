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
    public int QuantidadeEntradas { get; set; }
    public int QuantidadeSaidas { get; set; }
    // Só relevante pra conta CartaoCredito: true quando o sinal bruto do arquivo foi invertido
    // (automaticamente, por detecção de maioria, ou forçado via inverterSinaisManualmente) pra
    // chegar na convenção "compra = saída" — ver ImportacaoService.NormalizarSinalParaCartao.
    public bool SinalInvertidoParaCartao { get; set; }
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

    // Money Vision ("importação com período sobreposto"): "Arquivo: 01/10 a 07/11 · já havia no
    // app: 01/10 a 15/10 · novos dias: 16/10 a 07/11".
    public CoberturaImportacaoDto Cobertura { get; set; } = new();
    // Lançamentos importados antes, dentro do período do arquivo, que NÃO aparecem no extrato novo
    // (estornados ou duplicados) — o usuário decide manter ou excluir (ImportarArquivoAsync).
    public List<CaixaDiario.API.DTOs.ConciliacaoSaldo.LinhaDiagnosticoDto> AusentesNoArquivo { get; set; } = new();
    // Saldo que o próprio arquivo informa (OFX <LEDGERBAL>, ou coluna "Saldo") — sem ele, a tela
    // pergunta "Saldo no banco em <data> (opcional)".
    public SaldoBancoArquivoDto? SaldoBancoArquivo { get; set; }
}

public class CoberturaImportacaoDto
{
    public DateOnly ArquivoDe { get; set; }
    public DateOnly ArquivoAte { get; set; }
    public DateOnly? JaHaviaDe { get; set; }
    public DateOnly? JaHaviaAte { get; set; }
    public DateOnly? NovosDiasDe { get; set; }
    public DateOnly? NovosDiasAte { get; set; }
    public int QuantidadeDiasNovos { get; set; }
}

public class SaldoBancoArquivoDto
{
    public DateOnly Data { get; set; }
    public decimal Saldo { get; set; }
}
