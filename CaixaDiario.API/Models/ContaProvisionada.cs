namespace CaixaDiario.API.Models;

public class ContaProvisionada
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public bool Pago { get; set; } = false;
    public string? Categoria { get; set; }
    public Guid? RecorrenciaId { get; set; }
    public DateOnly? DataBaixa { get; set; }
    public Guid? ContaBancariaId { get; set; }
}
