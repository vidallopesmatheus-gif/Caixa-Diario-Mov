namespace CaixaDiario.API.DTOs.LinksConciliacao;

public class LinkConciliacaoDto
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }
    public int TotalClassificadosPeloCliente { get; set; }
    public string Status { get; set; } = string.Empty; // "Ativo" | "Expirado" | "Revogado"
}
