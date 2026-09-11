using System.Security.Cryptography;
using CaixaDiario.API.DTOs.LinksConciliacao;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class LinkConciliacaoService : ILinkConciliacaoService
{
    private readonly ILinkConciliacaoRepository _repo;

    public LinkConciliacaoService(ILinkConciliacaoRepository repo) => _repo = repo;

    public async Task<LinkConciliacaoDto> GerarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);

        // "Gerar novo link substituindo o anterior" — revoga o ativo antes de criar o novo, pra
        // nunca existir mais de um link válido ao mesmo tempo pro mesmo cliente.
        var ativo = await _repo.ObterAtivoPorClienteAsync(clienteId);
        if (ativo != null)
        {
            ativo.RevogadoEm = DateTime.UtcNow;
            await _repo.AtualizarAsync(ativo);
        }

        var agora = DateTime.UtcNow;
        var link = new LinkConciliacao
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            Token = GerarToken(),
            CriadoEm = agora,
            ExpiraEm = agora.AddHours(24),
            TotalClassificadosPeloCliente = 0,
        };

        var criado = await _repo.AdicionarAsync(link);
        return MapToDto(criado);
    }

    public async Task<List<LinkConciliacaoDto>> ListarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var links = await _repo.ListarPorClienteAsync(clienteId);
        return links.Select(MapToDto).ToList();
    }

    public async Task RevogarAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var link = await _repo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.LINK_CONCILIACAO_NAO_ENCONTRADO, "Link não encontrado.");
        VerificarAcesso(link.ClienteId, usuarioLogadoId, perfil);
        if (link.RevogadoEm != null)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Este link já foi revogado.");

        link.RevogadoEm = DateTime.UtcNow;
        await _repo.AtualizarAsync(link);
    }

    // Mesma entropia de uma chave de API — 32 bytes aleatórios, codificados url-safe.
    private static string GerarToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static LinkConciliacaoDto MapToDto(LinkConciliacao link) => new()
    {
        Id = link.Id,
        Token = link.Token,
        CriadoEm = link.CriadoEm,
        ExpiraEm = link.ExpiraEm,
        RevogadoEm = link.RevogadoEm,
        UltimoAcessoEm = link.UltimoAcessoEm,
        TotalClassificadosPeloCliente = link.TotalClassificadosPeloCliente,
        Status = DeterminarStatus(link),
    };

    private static string DeterminarStatus(LinkConciliacao link)
    {
        if (link.RevogadoEm != null) return "Revogado";
        if (DateTime.UtcNow > link.ExpiraEm) return "Expirado";
        return "Ativo";
    }

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }
}
