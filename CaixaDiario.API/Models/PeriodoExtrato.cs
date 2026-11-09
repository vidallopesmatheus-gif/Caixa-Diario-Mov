namespace CaixaDiario.API.Models;

// Money Vision ("até onde está lançado"): período que cada extrato importado cobre (de/até do
// arquivo, já com o filtro de datas aplicado). Dia sem transação dentro do período (fim de semana)
// continua "coberto" — lacuna é só o que nenhum extrato cobriu. Importações anteriores a esta tabela
// caem no fallback das datas das próprias transações importadas.
public class PeriodoExtrato
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public DateOnly De { get; set; }
    public DateOnly Ate { get; set; }
    public DateTime ImportadoEm { get; set; }
}
