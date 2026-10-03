using CaixaDiario.API.DTOs.ConfiguracaoFinanceira;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class ConfiguracaoFinanceiraService : IConfiguracaoFinanceiraService
{
    private readonly IUsuarioRepository _usuarioRepository;

    public ConfiguracaoFinanceiraService(IUsuarioRepository usuarioRepository) => _usuarioRepository = usuarioRepository;

    public async Task<ConfiguracaoFinanceiraDto> ObterAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var cliente = await ObterOuFalharAsync(clienteId);
        return MapToDto(cliente);
    }

    public async Task<ConfiguracaoFinanceiraDto> AtualizarAsync(
        Guid clienteId, AtualizarConfiguracaoFinanceiraDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (dto.CustoVidaMensalManual.HasValue && dto.CustoVidaMensalManual.Value < 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Custo de vida mensal não pode ser negativo.", "custoVidaMensalManual");
        if (dto.TaxaRetiradaFire <= 0 || dto.TaxaRetiradaFire > 100)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Taxa de retirada deve estar entre 0 e 100%.", "taxaRetiradaFire");

        var cliente = await ObterOuFalharAsync(clienteId);
        cliente.CustoVidaMensalManual = dto.CustoVidaMensalManual;
        cliente.TaxaRetiradaFire = dto.TaxaRetiradaFire;
        var atualizado = await _usuarioRepository.AtualizarAsync(cliente);
        return MapToDto(atualizado);
    }

    private async Task<Models.Usuario> ObterOuFalharAsync(Guid clienteId) =>
        await _usuarioRepository.ObterPorIdAsync(clienteId)
            ?? throw new ApiException(404, CodigoRetorno.USUARIO_NAO_ENCONTRADO, "Cliente não encontrado.");

    private static ConfiguracaoFinanceiraDto MapToDto(Models.Usuario u) => new()
    {
        CustoVidaMensalManual = u.CustoVidaMensalManual,
        TaxaRetiradaFire = u.TaxaRetiradaFire,
    };
}
