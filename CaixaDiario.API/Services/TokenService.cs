using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CaixaDiario.API.Models;
using Microsoft.IdentityModel.Tokens;

namespace CaixaDiario.API.Services;

public class TokenService : ITokenService
{
    // Money Vision ("desloga toda hora"): o access token é curto — quem renova a sessão é o refresh
    // token (cookie HttpOnly). Antes era 24h e, vencido, o usuário ia pro login sem aviso.
    private const int MinutosAcessoPadrao = 30;

    private readonly IConfiguration _config;

    public TokenService(IConfiguration config) => _config = config;

    public string GerarToken(Usuario usuario) => GerarTokenAcesso(usuario).Token;

    public TokenAcesso GerarTokenAcesso(Usuario usuario)
    {
        var secretKey = _config["Jwt:SecretKey"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;
        var minutos = int.TryParse(_config["Jwt:AccessTokenMinutes"], out var m) && m > 0 ? m : MinutosAcessoPadrao;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("id", usuario.Id.ToString()),
            new Claim("nome_usuario", usuario.NomeUsuario),
            new Claim("perfil", usuario.Perfil)
        };

        var expiraEm = DateTime.UtcNow.AddMinutes(minutos);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiraEm,
            signingCredentials: credentials);

        return new TokenAcesso(new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }

    // 64 bytes aleatórios em base64url — vai só pro cookie; o banco guarda o hash (HashRefreshToken).
    public static string GerarRefreshToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
