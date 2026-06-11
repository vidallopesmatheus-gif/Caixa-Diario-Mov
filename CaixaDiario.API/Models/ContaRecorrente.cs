namespace CaixaDiario.API.Models;

public class ContaRecorrente
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public int DiaVencimento { get; set; }  // 1–28
    public bool Ativa { get; set; } = true;
    public DateTime CriadaEm { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
