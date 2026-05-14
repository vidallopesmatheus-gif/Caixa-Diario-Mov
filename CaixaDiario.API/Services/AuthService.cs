using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class AuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ITokenService _tokenService;

    public AuthService(IUsuarioRepository usuarioRepository, ITokenService tokenService)
    {
        _usuarioRepository = usuarioRepository;
        _tokenService = tokenService;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NomeUsuario) || string.IsNullOrWhiteSpace(dto.Senha))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Nome de usuário e senha são obrigatórios.");

        var usuario = await _usuarioRepository.ObterPorNomeUsuarioAsync(dto.NomeUsuario);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Senha, usuario.SenhaHash))
            throw new ApiException(401, CodigoRetorno.CREDENCIAIS_INVALIDAS, "Usuário ou senha incorretos.");

        if (!usuario.Ativo)
            throw new ApiException(403, CodigoRetorno.USUARIO_INATIVO, "Usuário inativo.");

        return new LoginResponseDto
        {
            Id = usuario.Id,
            Token = _tokenService.GerarToken(usuario),
            Nome = usuario.Nome,
            Perfil = usuario.Perfil
        };
    }
}
