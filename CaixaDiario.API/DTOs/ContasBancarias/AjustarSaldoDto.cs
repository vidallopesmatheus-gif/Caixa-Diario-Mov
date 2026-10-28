namespace CaixaDiario.API.DTOs.ContasBancarias;

// Money Vision: "saldo não fecha com o extrato" — item 4 do pedido. Nunca troca o Inicio em
// silêncio: o usuário informa o saldo real do banco numa data, e a diferença pro saldo calculado
// vira UM lançamento visível (categoria "Ajuste de saldo"), nunca uma reâncora escondida.
public class AjustarSaldoDto
{
    public DateOnly Data { get; set; }
    public decimal SaldoReal { get; set; }
    public string? Descricao { get; set; }
}
