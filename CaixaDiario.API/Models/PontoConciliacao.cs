namespace CaixaDiario.API.Models;

// Money Vision ("conciliação de saldo sem ajuste toda hora"): saldo que o BANCO informou numa data
// (OFX <LEDGERBAL>, coluna "Saldo" da planilha, ou digitado na prévia/ajuste). Um por conta + data.
// O status (bate ou não com o app) é sempre recalculado na leitura contra a cadeia atual — SaldoApp
// guarda só o que o app tinha no momento do registro, pra histórico.
public class PontoConciliacao
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public DateOnly Data { get; set; }
    public decimal SaldoBanco { get; set; }
    public decimal SaldoApp { get; set; }
    // "Ofx" | "Planilha" | "Informado" | "Ajuste"
    public string Origem { get; set; } = "Informado";
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
