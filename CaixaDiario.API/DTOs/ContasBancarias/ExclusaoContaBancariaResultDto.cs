namespace CaixaDiario.API.DTOs.ContasBancarias;

public class ExclusaoContaBancariaResultDto
{
    public bool Excluida { get; set; }
    // Dias de registro (RegistroDiario) vinculados a esta conta — inclui dias soft-deletados
    // (Excluido=true), que ainda têm a FK apontando pra conta mesmo fora da visualização normal.
    public int DiasComLancamento { get; set; }
    public int ContasProvisionadas { get; set; }
    public int Transferencias { get; set; }
    public int TransacoesImportadas { get; set; }
    public int MetasVinculadas { get; set; }
    public int TotalVinculos =>
        DiasComLancamento + ContasProvisionadas + Transferencias + TransacoesImportadas + MetasVinculadas;
}
