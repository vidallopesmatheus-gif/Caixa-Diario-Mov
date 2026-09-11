namespace CaixaDiario.API.DTOs.FaturasCartao;

public class PagamentoFaturaDto
{
    public Guid Id { get; set; }
    public Guid ContaCartaoId { get; set; }
    public string ContaCartaoNome { get; set; } = string.Empty;
    public Guid ContaOrigemId { get; set; }
    public string ContaOrigemNome { get; set; } = string.Empty;
    public string Competencia { get; set; } = string.Empty;
    public decimal ValorPago { get; set; }
    public DateOnly Data { get; set; }
    public DateTime CriadoEm { get; set; }
}
