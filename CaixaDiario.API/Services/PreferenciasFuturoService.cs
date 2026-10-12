using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Bloco 3 da rodada "Visão de Futuro": service dedicado e pequeno, separado do CRUD de Usuario
// (UsuarioService é admin-only) — aqui é o próprio cliente lendo/editando as 2 preferências da
// tela "Meu Futuro" dele.
public class PreferenciasFuturoService : IPreferenciasFuturoService
{
    private readonly IUsuarioRepository _usuarioRepo;

    public PreferenciasFuturoService(IUsuarioRepository usuarioRepo) => _usuarioRepo = usuarioRepo;

    public async Task<PreferenciasFuturoDto> ObterAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        var usuario = await ObterOuFalharAsync(clienteId, usuarioLogadoId, perfil);
        return new PreferenciasFuturoDto { ReservaMinima = usuario.ReservaMinima, TipoPessoa = usuario.TipoPessoa };
    }

    public async Task<PreferenciasFuturoDto> AtualizarAsync(
        Guid clienteId, Guid usuarioLogadoId, string perfil, AtualizarPreferenciasFuturoDto dto)
    {
        var usuario = await ObterOuFalharAsync(clienteId, usuarioLogadoId, perfil);

        if (dto.ReservaMinima is < 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "A reserva mínima não pode ser negativa.", "reservaMinima");
        if (dto.TipoPessoa is not (null or "PF" or "PJ"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Tipo de pessoa inválido.", "tipoPessoa");

        usuario.ReservaMinima = dto.ReservaMinima;
        usuario.TipoPessoa = dto.TipoPessoa;
        usuario.AtualizadoEm = DateTime.UtcNow;
        await _usuarioRepo.AtualizarAsync(usuario);

        return new PreferenciasFuturoDto { ReservaMinima = usuario.ReservaMinima, TipoPessoa = usuario.TipoPessoa };
    }

    private async Task<Models.Usuario> ObterOuFalharAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return await _usuarioRepo.ObterPorIdAsync(clienteId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Usuário não encontrado.");
    }
}
