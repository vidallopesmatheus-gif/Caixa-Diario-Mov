using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Usuarios;

public class AtualizarUsuarioDto
{
    [Required] public string NomeCompleto { get; set; } = string.Empty;
    public string? NomeEstabelecimento { get; set; }
    public string? Senha { get; set; }
}