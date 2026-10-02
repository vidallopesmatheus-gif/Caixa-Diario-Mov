namespace CaixaDiario.API.DTOs.Importacao;

public class ExcluirLancamentoResultDto
{
    // true quando o lançamento era uma ponta de Transferência — nesse caso a contrapartida na
    // outra conta também foi excluída junto (as duas pontas são o mesmo evento financeiro).
    public bool TransferenciaExcluida { get; set; }
    // Preenchido com a descrição do título, quando excluir este lançamento reabre uma conta a
    // pagar/receber que tinha sido baixada vinculada a ele.
    public string? TituloReaberto { get; set; }
}
