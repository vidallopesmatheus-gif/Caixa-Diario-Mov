namespace CaixaDiario.API.DTOs.Usuarios;

public class UsuarioDto
{
    public Guid Id { get; set; }
    public string NomeUsuario { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Loja { get; set; }
    public string Perfil { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime CriadoEm { get; set; }
}
