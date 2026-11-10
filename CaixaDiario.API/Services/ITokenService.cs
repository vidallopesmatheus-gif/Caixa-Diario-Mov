using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public record TokenAcesso(string Token, DateTime ExpiraEm);

public interface ITokenService
{
    string GerarToken(Usuario usuario);
    TokenAcesso GerarTokenAcesso(Usuario usuario);
}
