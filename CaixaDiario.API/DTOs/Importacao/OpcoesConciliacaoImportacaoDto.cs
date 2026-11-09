namespace CaixaDiario.API.DTOs.Importacao;

/// <summary>Money Vision: decisões da prévia sobre conciliação — lançamentos que sumiram do extrato
/// novo e devem ser excluídos, e o saldo do banco digitado quando o arquivo não traz um.</summary>
public class OpcoesConciliacaoImportacaoDto
{
    public List<ItemExcluirLoteDto> ExcluirAusentes { get; set; } = new();
    public decimal? SaldoBancoInformado { get; set; }
    public DateOnly? DataSaldoBanco { get; set; }
}
