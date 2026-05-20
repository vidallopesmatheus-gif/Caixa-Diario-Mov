namespace CaixaDiario.API.DTOs.Registros;

public class ContaProvisionadaDto
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public bool Pago { get; set; } = false;
}
