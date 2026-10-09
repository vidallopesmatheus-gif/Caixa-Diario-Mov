namespace CaixaDiario.API.DTOs.Importacao;

/// <summary>Um lançamento já real (afeta saldo) que ainda não tem categoria — só a saída, hoje.</summary>
public class PendenteCategorizacaoDto
{
    public Guid Id { get; set; }
    public string Data { get; set; } = string.Empty; // ISO "yyyy-MM-dd" — identifica o RegistroDiario do item
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Tipo { get; set; } = "Saida";
    // Item 3.4: já vem com uma sugestão (histórico do cliente ou dicionário) — o frontend pré-preenche
    // o combobox e mostra o selo "sugerida", mas o item continua pendente até o usuário confirmar.
    public string? Categoria { get; set; }
    public bool CategoriaSugerida { get; set; }
    // Item 3.5: achou, numa OUTRA conta ativa do mesmo cliente, um único lançamento de sentido
    // oposto com valor (±1 centavo) e data (±1 dia) compatíveis — provável transferência entre
    // contas próprias. Só sugere: nunca converte nada sozinho (ver TransferenciaService).
    public Guid? SugestaoTransferenciaContaId { get; set; }
    public string? SugestaoTransferenciaContaNome { get; set; }
    public Guid? SugestaoTransferenciaLancamentoId { get; set; }
    public string? SugestaoTransferenciaData { get; set; }
}
