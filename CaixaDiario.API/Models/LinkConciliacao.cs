namespace CaixaDiario.API.Models;

// Link público (sem login) pro cliente classificar seus próprios lançamentos pendentes.
// Gerar um novo pra um cliente revoga automaticamente o anterior ainda ativo — só um
// link "vale" por vez (ver LinkConciliacaoService.GerarAsync).
public class LinkConciliacao
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }
    public int TotalClassificadosPeloCliente { get; set; }

    public Usuario Cliente { get; set; } = null!;
}
