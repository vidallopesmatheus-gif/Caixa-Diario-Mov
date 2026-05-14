namespace CaixaDiario.API.Models;

public class Usuario
{
    public Guid Id { get; set; }
    public string NomeUsuario { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Loja { get; set; }
    public string Perfil { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public string? UsuarioAtualizacao { get; set; }

    public ICollection<RegistroDiario> Registros { get; set; } = new List<RegistroDiario>();
}
