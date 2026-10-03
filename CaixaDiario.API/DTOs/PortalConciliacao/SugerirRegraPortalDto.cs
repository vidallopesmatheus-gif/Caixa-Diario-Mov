namespace CaixaDiario.API.DTOs.PortalConciliacao;

public class SugerirRegraPortalDto
{
    public Guid ContaBancariaId { get; set; }
    public string Tipo { get; set; } = string.Empty; // "Entrada" | "Saida"
    public string DescricaoReferencia { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
}
