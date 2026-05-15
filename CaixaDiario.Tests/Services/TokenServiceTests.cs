using System.IdentityModel.Tokens.Jwt;
using CaixaDiario.API.Models;
using CaixaDiario.API.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace CaixaDiario.Tests.Services;

public class TokenServiceTests
{
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Jwt:SecretKey"]).Returns("chave-super-secreta-testes-1234567890abcdef");
        configMock.Setup(c => c["Jwt:Issuer"]).Returns("CaixaDiario");
        configMock.Setup(c => c["Jwt:Audience"]).Returns("CaixaDiarioApp");
        configMock.Setup(c => c["Jwt:ExpiresInHours"]).Returns("24");
        _sut = new TokenService(configMock.Object);
    }

    private static Usuario CriarUsuario() => new()
    {
        Id = Guid.NewGuid(),
        NomeUsuario = "joao",
        SenhaHash = "hash",
        Nome = "João Silva",
        Perfil = "cliente",
        Ativo = true,
        CriadoEm = DateTime.UtcNow
    };

    [Fact]
    public void GerarToken_RetornaStringNaoVazia()
    {
        var token = _sut.GerarToken(CriarUsuario());
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GerarToken_IncluiClaimId()
    {
        var usuario = CriarUsuario();
        var token = _sut.GerarToken(usuario);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(usuario.Id.ToString(), jwt.Claims.First(c => c.Type == "id").Value);
    }

    [Fact]
    public void GerarToken_IncluiClaimNomeUsuario()
    {
        var usuario = CriarUsuario();
        var token = _sut.GerarToken(usuario);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("joao", jwt.Claims.First(c => c.Type == "nome_usuario").Value);
    }

    [Fact]
    public void GerarToken_IncluiClaimPerfil()
    {
        var usuario = CriarUsuario();
        var token = _sut.GerarToken(usuario);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("cliente", jwt.Claims.First(c => c.Type == "perfil").Value);
    }
}
