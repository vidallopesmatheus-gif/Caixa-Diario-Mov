using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Usuarios;

public class CriarUsuarioDto
{
    [Required] public string NomeUsuario { get; set; } = string.Empty;
    [Required] public string Senha { get; set; } = string.Empty;
    [Required] public string Nome { get; set; } = string.Empty;
    public string? Loja { get; set; }
}
