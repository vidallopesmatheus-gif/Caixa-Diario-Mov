using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Usuarios;

public class AtualizarUsuarioDto
{
    [Required] public string NomeUsuario { get; set; } = string.Empty;
    public string? Senha { get; set; }
    [Required] public string Nome { get; set; } = string.Empty;
    public string? Loja { get; set; }
}
