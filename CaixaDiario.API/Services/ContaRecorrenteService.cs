using System.Text.Json;
using CaixaDiario.API.DTOs.ContasRecorrentes;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class ContaRecorrenteService : IContaRecorrenteService
{
    private readonly IContaRecorrenteRepository _repo;
    private readonly IAuditService _auditService;

    public ContaRecorrenteService(IContaRecorrenteRepository repo, IAuditService auditService)
    {
        _repo = repo;
        _auditService = auditService;
    }

    public async Task<List<ContaRecorrenteDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var contas = await _repo.ListarAtivasPorClienteAsync(clienteId);
        return contas.Select(MapToDto).ToList();
    }

    public async Task<ContaRecorrenteDto> CriarAsync(CriarContaRecorrenteDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != dto.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (dto.Tipo != "Receber" && dto.Tipo != "Pagar")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Tipo deve ser 'Receber' ou 'Pagar'.", "tipo");

        var periodicidadesValidas = new[] { "Semanal", "Quinzenal", "Mensal", "Trimestral", "Semestral", "Anual" };
        if (!periodicidadesValidas.Contains(dto.Periodicidade))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Periodicidade inválida.", "periodicidade");

        if (dto.QuantidadeParcelas.HasValue && dto.QuantidadeParcelas.Value < 1)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Quantidade de parcelas deve ser maior que zero.", "quantidadeParcelas");

        var conta = new ContaRecorrente
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            Descricao = dto.Descricao,
            Valor = dto.Valor,
            Categoria = dto.Categoria,
            Tipo = dto.Tipo,
            DataInicio = dto.DataInicio,
            DataFim = dto.DataFim,
            Periodicidade = dto.Periodicidade,
            QuantidadeParcelas = dto.QuantidadeParcelas,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        var salva = await _repo.AdicionarAsync(conta);
        await _auditService.LogAsync(salva.ClienteId, usuarioLogadoId, "ContaRecorrente", "Criacao",
            salva.Id.ToString(), null, JsonSerializer.Serialize(MapToDto(salva)));

        return MapToDto(salva);
    }

    public async Task<ContaRecorrenteDto> AtualizarAsync(Guid clienteId, Guid id, AtualizarContaRecorrenteDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var conta = await _repo.ObterPorIdAsync(clienteId, id)
            ?? throw new ApiException(404, CodigoRetorno.CONTA_RECORRENTE_NAO_ENCONTRADA, "Conta recorrente não encontrada.");

        var antes = JsonSerializer.Serialize(MapToDto(conta));

        if (dto.Descricao != null) conta.Descricao = dto.Descricao;
        if (dto.Valor.HasValue) conta.Valor = dto.Valor.Value;
        if (dto.Categoria != null) conta.Categoria = dto.Categoria;
        if (dto.DataFim.HasValue) conta.DataFim = dto.DataFim;
        conta.AtualizadoEm = DateTime.UtcNow;

        var atualizada = await _repo.AtualizarAsync(conta);
        await _auditService.LogAsync(clienteId, usuarioLogadoId, "ContaRecorrente", "Edicao",
            id.ToString(), antes, JsonSerializer.Serialize(MapToDto(atualizada)));

        return MapToDto(atualizada);
    }

    public async Task DesativarAsync(Guid clienteId, Guid id, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var conta = await _repo.ObterPorIdAsync(clienteId, id)
            ?? throw new ApiException(404, CodigoRetorno.CONTA_RECORRENTE_NAO_ENCONTRADA, "Conta recorrente não encontrada.");

        var antes = JsonSerializer.Serialize(MapToDto(conta));
        conta.Ativo = false;
        conta.AtualizadoEm = DateTime.UtcNow;

        await _repo.AtualizarAsync(conta);
        await _auditService.LogAsync(clienteId, usuarioLogadoId, "ContaRecorrente", "Exclusao",
            id.ToString(), antes, null);
    }

    private static ContaRecorrenteDto MapToDto(ContaRecorrente c) => new()
    {
        Id = c.Id, ClienteId = c.ClienteId, Descricao = c.Descricao, Valor = c.Valor,
        Categoria = c.Categoria, Tipo = c.Tipo, DataInicio = c.DataInicio, DataFim = c.DataFim,
        Periodicidade = c.Periodicidade, QuantidadeParcelas = c.QuantidadeParcelas,
        Ativo = c.Ativo, CriadoEm = c.CriadoEm,
    };
}
