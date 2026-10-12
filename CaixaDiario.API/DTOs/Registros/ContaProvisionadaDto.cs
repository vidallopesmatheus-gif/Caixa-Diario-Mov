namespace CaixaDiario.API.DTOs.Registros;

public class ContaProvisionadaDto
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public bool Pago { get; set; } = false;
    public string? Categoria { get; set; }
    public Guid? RecorrenciaId { get; set; }
    public DateOnly? DataBaixa { get; set; }
    public decimal? ValorRealizado { get; set; }
    public Guid? ContaBancariaId { get; set; }
    public Guid? LancamentoVinculadoId { get; set; }
    // Rodada "Visão de Futuro" item 6: permite o front agrupar as parcelas de um mesmo contrato
    // de financiamento na lista de Contas a Pagar, em vez de uma a uma.
    public Guid? OrigemFinanciamentoId { get; set; }
}
