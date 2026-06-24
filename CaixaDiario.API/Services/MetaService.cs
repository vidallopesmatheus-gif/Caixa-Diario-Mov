using CaixaDiario.API.DTOs.Metas;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class MetaService : IMetaService
{
    private readonly IMetaRepository _metaRepository;

    public MetaService(IMetaRepository metaRepository) => _metaRepository = metaRepository;

    public async Task<MetaAnualDto> ObterMetaAsync(Guid clienteId, int ano, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var meta = await _metaRepository.ObterPorClienteEAnoAsync(clienteId, ano)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");

        return MapToDto(meta);
    }

    public async Task<MetaAnualDto> SalvarMetaAsync(SalvarMetaAnualDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != dto.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var existente = await _metaRepository.ObterPorClienteEAnoAsync(dto.ClienteId, dto.Ano);

        if (existente != null)
        {
            existente.MetaReceita = dto.MetaReceita;
            existente.MetaLucro = dto.MetaLucro;
            existente.MesInicio = dto.MesInicio;
            existente.PeriodoMeses = dto.PeriodoMeses;
            existente.Sonho = dto.Sonho;
            existente.ModoMeta = dto.ModoMeta;
            existente.ValorSonho = dto.ValorSonho;
            existente.PrazoAnos = dto.PrazoAnos;
            existente.TaxaRetorno = dto.TaxaRetorno;
            existente.TotalInvestido = dto.TotalInvestido;
            existente.AtualizadoEm = DateTime.UtcNow;
            return MapToDto(await _metaRepository.SalvarAsync(existente));
        }

        var nova = new MetaAnual
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            Ano = dto.Ano,
            MetaReceita = dto.MetaReceita,
            MetaLucro = dto.MetaLucro,
            MesInicio = dto.MesInicio,
            PeriodoMeses = dto.PeriodoMeses,
            Sonho = dto.Sonho,
            ModoMeta = dto.ModoMeta,
            ValorSonho = dto.ValorSonho,
            PrazoAnos = dto.PrazoAnos,
            TaxaRetorno = dto.TaxaRetorno,
            TotalInvestido = dto.TotalInvestido,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };
        return MapToDto(await _metaRepository.SalvarAsync(nova));
    }

    private static MetaAnualDto MapToDto(MetaAnual m) => new()
    {
        Id = m.Id, ClienteId = m.ClienteId, Ano = m.Ano,
        MetaReceita = m.MetaReceita, MetaLucro = m.MetaLucro,
        MesInicio = m.MesInicio, PeriodoMeses = m.PeriodoMeses, SalvoEm = m.AtualizadoEm,
        Sonho = m.Sonho, ModoMeta = m.ModoMeta,
        ValorSonho = m.ValorSonho, PrazoAnos = m.PrazoAnos,
        TaxaRetorno = m.TaxaRetorno, TotalInvestido = m.TotalInvestido,
    };
}
