using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface ITokenService
{
    string GerarToken(Usuario usuario);
}
