namespace CaixaDiario.API.DTOs.Admin;

public class ContrapartidaOrfaItemDto
{
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string ContaBancariaNome { get; set; } = string.Empty;
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Tipo { get; set; } = string.Empty; // "Entrada" | "Saida"
}

public class LimpezaContrapartidasOrfasResultDto
{
    // false = só relatório, nada foi gravado; true = os itens abaixo já foram removidos e o
    // saldo das contas afetadas já foi recalculado do zero (Inicio + Σentradas − Σsaídas, em
    // cadeia, dia a dia).
    public bool Confirmado { get; set; }
    public int ItensEncontrados { get; set; }
    public int ContasRecalculadas { get; set; }
    public List<ContrapartidaOrfaItemDto> Itens { get; set; } = new();
}
