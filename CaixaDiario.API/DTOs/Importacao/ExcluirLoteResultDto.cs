namespace CaixaDiario.API.DTOs.Importacao;

public class ExcluirLoteResultDto
{
    public int TotalExcluidos { get; set; }
    // Subconjunto de TotalExcluidos que eram ponta de Transferência — a contrapartida na outra
    // conta foi excluída junto (não soma separado, já está incluída em TotalExcluidos).
    public int TransferenciasExcluidas { get; set; }
    // Uma entrada por título de conta a pagar/receber que voltou a ficar em aberto.
    public List<string> TitulosReabertos { get; set; } = new();
    // Contas cujo saldo (dias seguintes à exclusão) foi recalculado.
    public List<Guid> ContasRecalculadas { get; set; } = new();
}
