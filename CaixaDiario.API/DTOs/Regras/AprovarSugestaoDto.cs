namespace CaixaDiario.API.DTOs.Regras;

public class AprovarSugestaoDto
{
    // Confirmação explícita pra aprovar mesmo havendo outra regra ativa com o mesmo critério e
    // ação diferente — ver RegraCategorizacaoService.AprovarSugestaoAsync.
    public bool ForcarApesarDeConflito { get; set; }
}
