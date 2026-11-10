using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class AuthService : IAuthService
{
    private const int DiasRefreshPadrao = 14;
    // Duas abas renovando ao mesmo tempo mandam o MESMO cookie — a segunda chega com um token que a
    // primeira acabou de trocar. Dentro desta janela isso é corrida, não roubo: renova de novo em
    // vez de derrubar a sessão.
    private static readonly TimeSpan JanelaCorridaRotacao = TimeSpan.FromSeconds(30);

    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository? _refreshTokenRepository;
    private readonly IConfiguration? _config;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IUsuarioRepository usuarioRepository, ITokenService tokenService,
        IRefreshTokenRepository? refreshTokenRepository = null, IConfiguration? config = null, TimeProvider? timeProvider = null)
    {
        _usuarioRepository = usuarioRepository;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _config = config;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private DateTime Agora() => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<SessaoEmitida> LoginAsync(LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NomeUsuario) || string.IsNullOrWhiteSpace(dto.Senha))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Nome de usuário e senha são obrigatórios.");

        var usuario = await _usuarioRepository.ObterPorNomeUsuarioAsync(dto.NomeUsuario.ToLower());

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Senha, usuario.SenhaHash))
            throw new ApiException(401, CodigoRetorno.CREDENCIAIS_INVALIDAS, "Usuário ou senha incorretos.");

        if (!usuario.Ativo)
            throw new ApiException(403, CodigoRetorno.USUARIO_INATIVO, "Usuário inativo.");

        if (_refreshTokenRepository != null)
            await _refreshTokenRepository.RemoverExpiradosDoUsuarioAsync(usuario.Id, Agora());
        return await EmitirSessaoAsync(usuario, familiaId: Guid.NewGuid());
    }

    // Troca o refresh token do cookie por um novo (rotação) + um access token novo. Qualquer falha é
    // o mesmo 401 "sessão expirada" — o front só precisa saber que tem que mandar pro login.
    public async Task<SessaoEmitida> RenovarAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || _refreshTokenRepository == null) throw SessaoExpirada();

        var agora = Agora();
        var atual = await _refreshTokenRepository.ObterPorHashAsync(TokenService.HashRefreshToken(refreshToken));
        if (atual == null || atual.ExpiraEm <= agora) throw SessaoExpirada();

        if (atual.RevogadoEm.HasValue)
        {
            var corrida = atual.Substituido && agora - atual.RevogadoEm.Value <= JanelaCorridaRotacao;
            if (!corrida)
            {
                // Token já trocado sendo usado de novo fora da janela: o cookie vazou. Derruba a sessão inteira.
                await _refreshTokenRepository.RevogarFamiliaAsync(atual.FamiliaId, agora);
                throw SessaoExpirada();
            }
        }
        else
        {
            atual.RevogadoEm = agora;
            atual.Substituido = true;
            await _refreshTokenRepository.AtualizarAsync(atual);
        }

        var usuario = await _usuarioRepository.ObterPorIdAsync(atual.UsuarioId);
        if (usuario == null || !usuario.Ativo)
        {
            await _refreshTokenRepository.RevogarFamiliaAsync(atual.FamiliaId, agora);
            throw SessaoExpirada();
        }

        return await EmitirSessaoAsync(usuario, atual.FamiliaId);
    }

    public async Task EncerrarAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || _refreshTokenRepository == null) return;
        var atual = await _refreshTokenRepository.ObterPorHashAsync(TokenService.HashRefreshToken(refreshToken));
        if (atual != null) await _refreshTokenRepository.RevogarFamiliaAsync(atual.FamiliaId, Agora());
    }

    private async Task<SessaoEmitida> EmitirSessaoAsync(Usuario usuario, Guid familiaId)
    {
        var acesso = _tokenService.GerarTokenAcesso(usuario);
        var resposta = new LoginResponseDto
        {
            UsuarioId = usuario.Id,
            Token = acesso.Token,
            ExpiraEm = acesso.ExpiraEm,
            NomeCompleto = usuario.Nome,
            NomeUsuario = usuario.NomeUsuario,
            NomeEstabelecimento = usuario.Loja ?? string.Empty,
            Perfil = usuario.Perfil
        };
        if (_refreshTokenRepository == null) return new SessaoEmitida(resposta, null, null);

        var dias = int.TryParse(_config?["Jwt:RefreshTokenDays"], out var d) && d > 0 ? d : DiasRefreshPadrao;
        var agora = Agora();
        var bruto = TokenService.GerarRefreshToken();
        var expiraEm = agora.AddDays(dias);
        await _refreshTokenRepository.AdicionarAsync(new RefreshToken
        {
            Id = Guid.NewGuid(), UsuarioId = usuario.Id, TokenHash = TokenService.HashRefreshToken(bruto),
            FamiliaId = familiaId, CriadoEm = agora, ExpiraEm = expiraEm,
        });
        return new SessaoEmitida(resposta, bruto, expiraEm);
    }

    private static ApiException SessaoExpirada() =>
        new(401, CodigoRetorno.SESSAO_EXPIRADA, "Sua sessão expirou. Entre de novo para continuar.");
}
