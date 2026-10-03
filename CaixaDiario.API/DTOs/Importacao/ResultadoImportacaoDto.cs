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
}
