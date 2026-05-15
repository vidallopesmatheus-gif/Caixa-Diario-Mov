using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;

    public UsuarioService(IUsuarioRepository usuarioRepository) => _usuarioRepository = usuarioRepository;

    public async Task<List<UsuarioDto>> ListarAsync()
    {
        var usuarios = await _usuarioRepository.ListarAtivosAsync();
        return usuarios.Select(MapToDto).ToList();
    }

    public async Task<UsuarioDto> ObterPorIdAsync(Guid id)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.USUARIO_NAO_ENCONTRADO, "Usuário não encontrado.");
        return MapToDto(usuario);
    }

    public async Task<UsuarioDto> CriarAsync(CriarUsuarioDto dto, string nomeUsuarioLogado)
    {
        if (string.IsNullOrWhiteSpace(dto.NomeUsuario) || string.IsNullOrWhiteSpace(dto.Nome))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Nome e usuário são obrigatórios.");

        if (dto.Senha.Length < 4)
            throw new ApiException(400, CodigoRetorno.SENHA_MUITO_CURTA, "Senha deve ter no mínimo 4 caracteres.", "senha");

        if (await _usuarioRepository.ExisteNomeUsuarioAsync(dto.NomeUsuario))
            throw new ApiException(409, CodigoRetorno.NOME_USUARIO_DUPLICADO, "Nome de usuário já existe.", "nome_usuario");

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeUsuario = dto.NomeUsuario.ToLower(),
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(dto.Senha),
            Nome = dto.Nome,
            Loja = dto.Loja,
            Perfil = "cliente",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            UsuarioAtualizacao = nomeUsuarioLogado
        };

        var criado = await _usuarioRepository.AdicionarAsync(usuario);
        return MapToDto(criado);
    }

    public async Task<UsuarioDto> AtualizarAsync(Guid id, AtualizarUsuarioDto dto, string nomeUsuarioLogado)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.USUARIO_NAO_ENCONTRADO, "Usuário não encontrado.");

        if (string.IsNullOrWhiteSpace(dto.NomeUsuario) || string.IsNullOrWhiteSpace(dto.Nome))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Nome e usuário são obrigatórios.");

        if (!string.IsNullOrWhiteSpace(dto.Senha) && dto.Senha.Length < 4)
            throw new ApiException(400, CodigoRetorno.SENHA_MUITO_CURTA, "Senha deve ter no mínimo 4 caracteres.", "senha");

        if (await _usuarioRepository.ExisteNomeUsuarioAsync(dto.NomeUsuario, id))
            throw new ApiException(409, CodigoRetorno.NOME_USUARIO_DUPLICADO, "Nome de usuário já existe.", "nome_usuario");

        usuario.NomeUsuario = dto.NomeUsuario.ToLower();
        usuario.Nome = dto.Nome;
        usuario.Loja = dto.Loja;
        usuario.AtualizadoEm = DateTime.UtcNow;
        usuario.UsuarioAtualizacao = nomeUsuarioLogado;

        if (!string.IsNullOrWhiteSpace(dto.Senha))
            usuario.SenhaHash = BCrypt.Net.BCrypt.HashPassword(dto.Senha);

        var atualizado = await _usuarioRepository.AtualizarAsync(usuario);
        return MapToDto(atualizado);
    }

    public async Task DesativarAsync(Guid id, string nomeUsuarioLogado)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.USUARIO_NAO_ENCONTRADO, "Usuário não encontrado.");

        usuario.Ativo = false;
        usuario.AtualizadoEm = DateTime.UtcNow;
        usuario.UsuarioAtualizacao = nomeUsuarioLogado;

        await _usuarioRepository.AtualizarAsync(usuario);
    }

    private static UsuarioDto MapToDto(Usuario u) => new()
    {
        Id = u.Id,
        NomeUsuario = u.NomeUsuario,
        Nome = u.Nome,
        Loja = u.Loja,
        Perfil = u.Perfil,
        Ativo = u.Ativo,
        CriadoEm = u.CriadoEm
    };
}
