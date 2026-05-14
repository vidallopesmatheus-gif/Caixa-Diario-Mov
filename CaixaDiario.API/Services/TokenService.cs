using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CaixaDiario.API.Models;
using Microsoft.IdentityModel.Tokens;

namespace CaixaDiario.API.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config) => _config = config;

    public string GerarToken(Usuario usuario)
    {
        var secretKey = _config["Jwt:SecretKey"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;
        var expiresInHours = int.Parse(_config["Jwt:ExpiresInHours"] ?? "24");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("id", usuario.Id.ToString()),
            new Claim("nome_usuario", usuario.NomeUsuario),
            new Claim("perfil", usuario.Perfil)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiresInHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
