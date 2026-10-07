namespace CaixaDiario.API.DTOs.Admin;

public class MigracaoRecorrenciasResultDto
{
    // false = só relatório, nada foi gravado; true = as mudanças abaixo já foram persistidas.
    public bool Confirmado { get; set; }
    public int ItensMovidos { get; set; }
    public int RegistrosCriados { get; set; }
    public int RegistrosExcluidosVazios { get; set; }
    // Itens já pagos e presos num registro da conta errada — mover isso implicaria recalcular
    // saldo histórico, então a migration nunca toca nesses automaticamente. Precisam de revisão
    // manual (ex.: estornar a baixa, corrigir, dar baixa de novo na conta certa).
    public List<string> ItensQuePrecisamRevisaoManual { get; set; } = new();
    public List<string> Detalhes { get; set; } = new();
}
