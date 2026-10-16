using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ICategoriaRepository _categoriaRepository;

    public UsuarioService(IUsuarioRepository usuarioRepository, ICategoriaRepository categoriaRepository)
    {
        _usuarioRepository = usuarioRepository;
        _categoriaRepository = categoriaRepository;
    }

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
        if (string.IsNullOrWhiteSpace(dto.NomeUsuario) || string.IsNullOrWhiteSpace(dto.NomeCompleto))
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
            Nome = dto.NomeCompleto,
            Loja = dto.NomeEstabelecimento,
            Perfil = "cliente",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            UsuarioAtualizacao = nomeUsuarioLogado
        };

        var criado = await _usuarioRepository.AdicionarAsync(usuario);

        // Correção de integridade (Parte 1): todo cliente novo recebe sua PRÓPRIA cópia do plano
        // de categorias padrão (molde ClienteId == null, Origem == "Padrao") — dali em diante a
        // lista dele é independente; editar/desativar nunca afeta outro cliente nem o molde.
        var padrao = await _categoriaRepository.ListarPadraoAtivasAsync();
        if (padrao.Count > 0)
        {
            var copias = padrao.Select(c => new Categoria
            {
                Id = Guid.NewGuid(),
                Nome = c.Nome,
                Tipo = c.Tipo,
                TipoMovimento = c.TipoMovimento,
                GrupoId = c.GrupoId,
                Ordem = c.Ordem,
                Ativa = true,
                CriadoEm = DateTime.UtcNow,
                ClienteId = criado.Id,
                Origem = "Cliente",
                CriadoPor = null,
            }).ToList();
            await _categoriaRepository.AdicionarVariasAsync(copias);
        }

        return MapToDto(criado);
    }

    public async Task<UsuarioDto> AtualizarAsync(Guid id, AtualizarUsuarioDto dto, string nomeUsuarioLogado)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.USUARIO_NAO_ENCONTRADO, "Usuário não encontrado.");

        if (string.IsNullOrWhiteSpace(dto.NomeCompleto))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Nome é obrigatório.");

        if (!string.IsNullOrWhiteSpace(dto.Senha) && dto.Senha.Length < 4)
            throw new ApiException(400, CodigoRetorno.SENHA_MUITO_CURTA, "Senha deve ter no mínimo 4 caracteres.", "senha");

        usuario.Nome = dto.NomeCompleto;
        usuario.Loja = dto.NomeEstabelecimento;
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
        NomeCompleto = u.Nome,
        NomeEstabelecimento = u.Loja,
        Perfil = u.Perfil,
        Ativo = u.Ativo,
        CriadoEm = u.CriadoEm
    };
}
