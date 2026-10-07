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
}
