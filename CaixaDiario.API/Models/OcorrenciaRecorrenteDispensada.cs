namespace CaixaDiario.API.Models;

// Registra que o cliente excluiu explicitamente UMA ocorrência específica de uma conta
// recorrente (ex.: a parcela de setembro de uma assinatura mensal), sem desativar a recorrência
// inteira. Sem isso, RecorrenciaService.MaterializarMesAtualAsync recria a ocorrência na próxima
// vez que a lista de contas é carregada, porque só verifica "existe nos registros atuais?" — e a
// exclusão, por definição, remove ela de lá.
public class OcorrenciaRecorrenteDispensada
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid RecorrenciaId { get; set; }
    public DateOnly DataVencimento { get; set; }
    public DateTime CriadoEm { get; set; }
}
