using CaixaDiario.API.DTOs.Metas;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class MetaService : IMetaService
{
    private readonly IMetaRepository _metaRepository;
    private readonly IMetaProgressoService _metaProgressoService;

    public MetaService(IMetaRepository metaRepository, IMetaProgressoService metaProgressoService)
    {
        _metaRepository = metaRepository;
        _metaProgressoService = metaProgressoService;
    }

    public async Task<MetaAnualDto> ObterMetaAsync(Guid clienteId, int ano, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        // Só a meta "simples" (Meta de Faturamento Mensal) ainda é 1-por-ano — objetivos (modo
        // "metodo") são vários por cliente e são buscados/editados por Id via ListarMetasAsync.
        var meta = await _metaRepository.ObterMetaSimplesPorClienteEAnoAsync(clienteId, ano)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");

        // Se a meta estiver vinculada a uma conta de investimento, TotalInvestido passa a
        // refletir o saldo real da conta em vez do valor gravado manualmente (não persiste).
        var metas = new List<MetaAnual> { meta };
        await _metaProgressoService.AplicarSaldoDeContasVinculadasAsync(clienteId, metas);

        return MapToDto(meta);
    }

    public async Task<List<MetaAnualDto>> ListarMetasAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var metas = await _metaRepository.ListarPorClienteAsync(clienteId);
        await _metaProgressoService.AplicarSaldoDeContasVinculadasAsync(clienteId, metas);
        return metas.OrderByDescending(m => m.Ano).Select(MapToDto).ToList();
    }

    public async Task<MetaAnualDto> SalvarMetaAsync(SalvarMetaAnualDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != dto.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (dto.ModoMeta == "metodo" && dto.DataAlvo == null)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a data planejada do objetivo.");

        // "simples" (Meta de Faturamento Mensal) continua 1-por-ano, identificada por (Cliente,
        // Ano). "metodo" (objetivo) não tem mais essa identidade — cada um é uma linha própria,
        // localizada pelo Id explícito; sem Id, é sempre um objetivo novo.
        MetaAnual? existente;
        if (dto.ModoMeta == "simples")
        {
            existente = await _metaRepository.ObterMetaSimplesPorClienteEAnoAsync(dto.ClienteId, dto.Ano);
        }
        else if (dto.Id.HasValue)
        {
            existente = await _metaRepository.ObterPorIdAsync(dto.Id.Value)
                ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Objetivo não encontrado.");
            if (existente.ClienteId != dto.ClienteId)
                throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        }
        else
        {
            existente = null;
        }

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
            existente.MargemPJ = dto.MargemPJ;
            existente.IconeSonho = dto.IconeSonho;
            existente.DataAlvo = dto.DataAlvo;
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
            MargemPJ = dto.MargemPJ,
            IconeSonho = dto.IconeSonho,
            DataAlvo = dto.DataAlvo,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };
        return MapToDto(await _metaRepository.SalvarAsync(nova));
    }

    public async Task ExcluirMetaAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var meta = await _metaRepository.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");
        if (perfil == "cliente" && usuarioLogadoId != meta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        await _metaRepository.RemoverAsync(meta);
    }

    private static MetaAnualDto MapToDto(MetaAnual m) => new()
    {
        Id = m.Id, ClienteId = m.ClienteId, Ano = m.Ano,
        MetaReceita = m.MetaReceita, MetaLucro = m.MetaLucro,
        MesInicio = m.MesInicio, PeriodoMeses = m.PeriodoMeses, SalvoEm = m.AtualizadoEm,
        Sonho = m.Sonho, ModoMeta = m.ModoMeta,
        ValorSonho = m.ValorSonho, PrazoAnos = m.PrazoAnos,
        TaxaRetorno = m.TaxaRetorno, TotalInvestido = m.TotalInvestido,
        MargemPJ = m.MargemPJ, IconeSonho = m.IconeSonho,
        ContaInvestimentoId = m.ContaInvestimentoId,
        DataAlvo = m.DataAlvo,
    };
}
