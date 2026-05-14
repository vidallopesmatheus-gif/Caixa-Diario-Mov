using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Auth;

public class LoginRequestDto
{
    [Required] public string NomeUsuario { get; set; } = string.Empty;
    [Required] public string Senha { get; set; } = string.Empty;
}
