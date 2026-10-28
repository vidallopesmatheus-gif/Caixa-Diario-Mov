namespace CaixaDiario.API.DTOs.Admin;

// Money Vision: "data da parcela vs. competência" (item 2) — repara parcelas já importadas ANTES
// da reancoragem existir, cuja Data ainda é a da compra original em vez da cobrança da fatura.
public class ItemDataParcelaCorrigidoDto
{
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string ContaBancariaNome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateOnly DataAntiga { get; set; }
    public DateOnly DataNova { get; set; }
}

public class CorrigirDataParcelasResultDto
{
    // false = só relatório, nada foi gravado; true = os itens abaixo já foram movidos pra sua
    // fatura (a conta inteira já teve o saldo recalculado do zero).
    public bool Confirmado { get; set; }
    public int ItensEncontrados { get; set; }
    public int ContasRecalculadas { get; set; }
    public List<ItemDataParcelaCorrigidoDto> Itens { get; set; } = new();
}
