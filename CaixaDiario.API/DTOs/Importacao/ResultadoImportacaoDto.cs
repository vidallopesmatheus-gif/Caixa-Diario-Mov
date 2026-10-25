namespace CaixaDiario.API.DTOs.Importacao;

/// <summary>Resumo do que foi efetivamente lançado ao confirmar uma importação.</summary>
public class ResultadoImportacaoDto
{
    public int TotalImportadas { get; set; }
    public int TotalPendentesCategorizacao { get; set; }
    public int TotalCategorizadasPorRegra { get; set; }
    // Transações do arquivo que casaram com uma contrapartida PROVISÓRIA de Transferência (criada
    // ao classificar o lado de cá antes do extrato de lá chegar) — concilia em vez de duplicar.
    public int TotalConciliadasTransferencia { get; set; }
    // Mais de uma provisória candidata pro mesmo valor/sentido/janela — não decide sozinho, a
    // transação entra como lançamento novo normal (pendente), pro usuário conciliar manualmente.
    public int TotalAmbiguasTransferencia { get; set; }
    public decimal TotalEntradas { get; set; }
    public decimal TotalSaidas { get; set; }
    // Fase 1.7: quantas transações do arquivo foram mescladas a um lançamento manual existente em
    // vez de criar um novo (já estão incluídas em TotalImportadas, não somar de novo).
    public int TotalMescladasComManual { get; set; }
    // Prováveis duplicatas entre dois arquivos importados (ex.: CSV + OFX do mesmo banco)
    // sinalizadas na revisão — Item 3.1: por padrão NÃO são importadas (diferente da duplicata
    // manual, que por padrão mescla); TotalDuplicatasEntreArquivosIgnoradas é o subconjunto que
    // de fato ficou de fora (as que o usuário resolveu como "Importar" entram em TotalImportadas).
    public int TotalDuplicatasEntreArquivosSinalizadas { get; set; }
    public int TotalDuplicatasEntreArquivosIgnoradas { get; set; }
    // Fase 1.2: quantas sugestões de vínculo (título pendente × lançamento real) o motor encontrou
    // no intervalo deste arquivo, depois da deduplicação — a tela mostra "confirmar todas / revisar".
    public int TotalSugestoesVinculo { get; set; }
    // Cartão (Bloco 2): preenchido só quando a conta é CartaoCredito — a fatura criada por esta
    // importação, pra tela redirecionar pra "Ver faturas" ou pra abrir ela direto.
    public Guid? FaturaId { get; set; }
    // Rodada de "parcelamentos": um item por Parcelamento resolvido/criado nesta importação — a
    // tela mostra "Encontrei N parcelamentos: Trocafone 1/10 (faltam 9 · R$ 1.792,44)...".
    public List<ParcelamentoDetectadoResumoDto> ParcelamentosDetectados { get; set; } = new();
}

public class ParcelamentoDetectadoResumoDto
{
    public Guid ParcelamentoId { get; set; }
    public string Comerciante { get; set; } = string.Empty;
    public int NumeroParcela { get; set; }
    public int TotalParcelas { get; set; }
    public int ParcelasRestantes { get; set; }
    public decimal ValorRestante { get; set; }
    public string Status { get; set; } = string.Empty;
}
