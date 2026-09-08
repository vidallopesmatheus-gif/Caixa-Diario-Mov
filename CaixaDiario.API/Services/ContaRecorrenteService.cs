using System.Text.Json;
using CaixaDiario.API.DTOs.ContasRecorrentes;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class ContaRecorrenteService : IContaRecorrenteService
{
    private static readonly string[] PeriodicidadesValidas =
        { "Semanal", "Quinzenal", "Mensal", "Trimestral", "Semestral", "Anual" };

    private readonly IContaRecorrenteRepository _repo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IAuditService _auditService;

    public ContaRecorrenteService(IContaRecorrenteRepository repo, IRegistroRepository registroRepo, IAuditService auditService)
    {
        _repo = repo;
        _registroRepo = registroRepo;
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

        if (!PeriodicidadesValidas.Contains(dto.Periodicidade))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Periodicidade inválida.", "periodicidade");

        if (dto.QuantidadeParcelas.HasValue && dto.QuantidadeParcelas.Value < 1)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Quantidade de parcelas deve ser maior que zero.", "quantidadeParcelas");

        // Teto de segurança: 360 (30 anos mensais) evita digitar por engano um valor em R$ neste campo.
        if (dto.QuantidadeParcelas.HasValue && dto.QuantidadeParcelas.Value > 360)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Quantidade de parcelas não pode ser maior que 360.", "quantidadeParcelas");

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
            ContaBancariaId = dto.ContaBancariaId,
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

        if (dto.Periodicidade != null && !PeriodicidadesValidas.Contains(dto.Periodicidade))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Periodicidade inválida.", "periodicidade");

        var antes = JsonSerializer.Serialize(MapToDto(conta));

        if (dto.Descricao != null) conta.Descricao = dto.Descricao;
        if (dto.Valor.HasValue) conta.Valor = dto.Valor.Value;
        if (dto.Categoria != null) conta.Categoria = dto.Categoria;
        if (dto.DataInicio.HasValue) conta.DataInicio = dto.DataInicio.Value;
        if (dto.DataFim.HasValue) conta.DataFim = dto.DataFim;
        if (dto.Periodicidade != null) conta.Periodicidade = dto.Periodicidade;
        if (dto.ContaBancariaId.HasValue) conta.ContaBancariaId = dto.ContaBancariaId;
        conta.AtualizadoEm = DateTime.UtcNow;

        var atualizada = await _repo.AtualizarAsync(conta);

        if (dto.AplicarAsPendentes)
            await AtualizarOcorrenciasPendentesAsync(clienteId, atualizada);

        await _auditService.LogAsync(clienteId, usuarioLogadoId, "ContaRecorrente", "Edicao",
            id.ToString(), antes, JsonSerializer.Serialize(MapToDto(atualizada)));

        return MapToDto(atualizada);
    }

    public async Task DesativarAsync(Guid clienteId, Guid id, bool removerPendentes, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var conta = await _repo.ObterPorIdAsync(clienteId, id)
            ?? throw new ApiException(404, CodigoRetorno.CONTA_RECORRENTE_NAO_ENCONTRADA, "Conta recorrente não encontrada.");

        var antes = JsonSerializer.Serialize(MapToDto(conta));
        conta.Ativo = false;
        conta.AtualizadoEm = DateTime.UtcNow;

        await _repo.AtualizarAsync(conta);

        if (removerPendentes)
            await RemoverOcorrenciasPendentesAsync(clienteId, id);

        await _auditService.LogAsync(clienteId, usuarioLogadoId, "ContaRecorrente", "Exclusao",
            id.ToString(), antes, null);
    }

    // Propaga Descricao/Valor/Categoria/ContaBancariaId pras ocorrências pendentes (não pagas) já
    // materializadas dessa recorrência. Nunca toca em ocorrências pagas (fato financeiro já
    // realizado) nem na DataVencimento de nenhuma (intrínseca a quando a ocorrência foi gerada).
    private async Task AtualizarOcorrenciasPendentesAsync(Guid clienteId, ContaRecorrente conta)
    {
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        foreach (var registro in registros)
        {
            var mudouReceber = AplicarNaLista(registro.ContasReceber, conta);
            var mudouPagar = AplicarNaLista(registro.ContasPagar, conta);
            if (!mudouReceber && !mudouPagar) continue;

            // Reatribui a lista — jsonb sem value comparer, EF só detecta mudança na referência.
            if (mudouReceber) registro.ContasReceber = new List<ContaProvisionada>(registro.ContasReceber);
            if (mudouPagar) registro.ContasPagar = new List<ContaProvisionada>(registro.ContasPagar);
            registro.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(registro);
        }
    }

    private static bool AplicarNaLista(List<ContaProvisionada> contas, ContaRecorrente conta)
    {
        var mudou = false;
        foreach (var c in contas)
        {
            if (c.RecorrenciaId != conta.Id || c.Pago) continue;
            c.Descricao = conta.Descricao;
            c.Valor = conta.Valor;
            c.Categoria = conta.Categoria;
            c.ContaBancariaId = conta.ContaBancariaId;
            mudou = true;
        }
        return mudou;
    }

    // Remove as ocorrências pendentes (não pagas) já materializadas dessa recorrência. Nunca toca
    // em ocorrências pagas — removê-las apagaria um fato financeiro já refletido no saldo.
    private async Task RemoverOcorrenciasPendentesAsync(Guid clienteId, Guid recorrenciaId)
    {
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        foreach (var registro in registros)
        {
            var novasReceber = registro.ContasReceber.Where(c => !(c.RecorrenciaId == recorrenciaId && !c.Pago)).ToList();
            var novasPagar = registro.ContasPagar.Where(c => !(c.RecorrenciaId == recorrenciaId && !c.Pago)).ToList();
            if (novasReceber.Count == registro.ContasReceber.Count && novasPagar.Count == registro.ContasPagar.Count)
                continue;

            registro.ContasReceber = novasReceber;
            registro.ContasPagar = novasPagar;
            registro.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(registro);
        }
    }

    private static ContaRecorrenteDto MapToDto(ContaRecorrente c) => new()
    {
        Id = c.Id, ClienteId = c.ClienteId, Descricao = c.Descricao, Valor = c.Valor,
        Categoria = c.Categoria, Tipo = c.Tipo, DataInicio = c.DataInicio, DataFim = c.DataFim,
        Periodicidade = c.Periodicidade, QuantidadeParcelas = c.QuantidadeParcelas,
        Ativo = c.Ativo, CriadoEm = c.CriadoEm, ContaBancariaId = c.ContaBancariaId,
    };
}
