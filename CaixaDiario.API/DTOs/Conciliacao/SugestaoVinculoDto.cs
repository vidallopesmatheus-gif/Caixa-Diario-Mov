namespace CaixaDiario.API.DTOs.Conciliacao;

// Fase 1.2: candidato de vínculo entre um título pendente (ContaProvisionada) e um lançamento já
// existente no extrato (Entrada/Saída) — a tela só mostra quando Score >= 50 (ver ConciliacaoService).
public class SugestaoVinculoDto
{
    public Guid ContaProvisionadaId { get; set; }
    public string Tipo { get; set; } = string.Empty; // "Receber" ou "Pagar"
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public Guid ContaBancariaId { get; set; }

    public Guid LancamentoId { get; set; }
    public string LancamentoDescricao { get; set; } = string.Empty;
    public decimal LancamentoValor { get; set; }
    public DateOnly LancamentoData { get; set; }

    public int Score { get; set; }
}
