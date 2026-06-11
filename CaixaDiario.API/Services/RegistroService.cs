using System.Text.Json;
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class RegistroService : IRegistroService
{
    private readonly IRegistroRepository _registroRepository;
    private readonly IAuditService _auditService;
    private readonly IRecorrenciaService _recorrenciaService;

    public RegistroService(
        IRegistroRepository registroRepository,
        IAuditService auditService,
        IRecorrenciaService recorrenciaService)
    {
        _registroRepository = registroRepository;
        _auditService = auditService;
        _recorrenciaService = recorrenciaService;
    }

    public async Task<List<RegistroDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        await _recorrenciaService.MaterializarMesAtualAsync(clienteId);
        var registros = await _registroRepository.ListarPorClienteAsync(clienteId);
        return registros.Select(MapToDto).ToList();
    }

    public async Task<RegistroDto> ObterPorDataAsync(Guid clienteId, DateOnly data, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registro = await _registroRepository.ObterPorClienteEDataAsync(clienteId, data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Registro não encontrado.");

        return MapToDto(registro);
    }

    public async Task<(RegistroDto dto, bool criado)> SalvarAsync(CriarRegistroDto dto, string nomeUsuarioLogado)
    {
        if (dto.Data > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ApiException(400, CodigoRetorno.DATA_FUTURA, "Não é possível registrar data futura.", "data");

        var existente = await _registroRepository.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data);
        var dadosAntes = existente != null ? JsonSerializer.Serialize(MapToDto(existente)) : null;

        if (existente != null)
        {
            existente.Inicio = dto.Inicio;
            existente.Entradas = dto.Entradas.Select(MapItemDto).ToList();
            existente.Saidas = dto.Saidas.Select(MapItemDto).ToList();
            existente.ContasReceber = AplicarBaixaAutomatica(dto.ContasReceber.Select(MapContaDto).ToList(), dto.Data);
            existente.ContasPagar = AplicarBaixaAutomatica(dto.ContasPagar.Select(MapContaDto).ToList(), dto.Data);
            existente.SaldoFinal = dto.SaldoFinal;
            existente.SalvoEm = DateTime.UtcNow;
            existente.AtualizadoEm = DateTime.UtcNow;
            existente.UsuarioAtualizacao = nomeUsuarioLogado;

            var atualizado = await _registroRepository.AtualizarAsync(existente);
            var resultDto = MapToDto(atualizado);

            await _auditService.LogAsync(existente.ClienteId, Guid.Empty, "RegistroDiario", "Edicao",
                $"{existente.ClienteId}/{existente.Data}", dadosAntes, JsonSerializer.Serialize(resultDto));

            return (resultDto, false);
        }

        var novo = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            Data = dto.Data,
            Inicio = dto.Inicio,
            Entradas = dto.Entradas.Select(MapItemDto).ToList(),
            Saidas = dto.Saidas.Select(MapItemDto).ToList(),
            ContasReceber = AplicarBaixaAutomatica(dto.ContasReceber.Select(MapContaDto).ToList(), dto.Data),
            ContasPagar = AplicarBaixaAutomatica(dto.ContasPagar.Select(MapContaDto).ToList(), dto.Data),
            SaldoFinal = dto.SaldoFinal,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
            UsuarioAtualizacao = nomeUsuarioLogado
        };

        var criado = await _registroRepository.AdicionarAsync(novo);
        var criadoDto = MapToDto(criado);

        await _auditService.LogAsync(novo.ClienteId, Guid.Empty, "RegistroDiario", "Criacao",
            $"{novo.ClienteId}/{novo.Data}", null, JsonSerializer.Serialize(criadoDto));

        return (criadoDto, true);
    }

    public async Task ExcluirAsync(Guid clienteId, DateOnly data, string motivo, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ApiException(400, CodigoRetorno.MOTIVO_OBRIGATORIO, "Motivo de exclusão é obrigatório.", "motivo_exclusao");

        var registro = await _registroRepository.ObterPorClienteEDataAsync(clienteId, data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Registro não encontrado.");

        var dadosAntes = JsonSerializer.Serialize(MapToDto(registro));
        registro.Excluido = true;
        registro.MotivoExclusao = motivo;
        registro.AtualizadoEm = DateTime.UtcNow;
        registro.UsuarioAtualizacao = usuarioLogadoId.ToString();

        await _registroRepository.AtualizarAsync(registro);
        await _auditService.LogAsync(clienteId, usuarioLogadoId, "RegistroDiario", "Exclusao",
            $"{clienteId}/{data}", dadosAntes, null);
    }

    private static List<ContaProvisionada> AplicarBaixaAutomatica(List<ContaProvisionada> contas, DateOnly data)
    {
        foreach (var c in contas)
        {
            if (c.DataVencimento.HasValue && c.DataVencimento.Value == data && !c.Pago)
                c.Pago = true;
        }
        return contas;
    }

    private static ItemFinanceiro MapItemDto(ItemFinanceiroDto d) =>
        new() { Descricao = d.Descricao, Valor = d.Valor, Categoria = d.Categoria, TipoCusto = d.TipoCusto };

    private static ContaProvisionada MapContaDto(ContaProvisionadaDto d) =>
        new() { Descricao = d.Descricao, Valor = d.Valor, DataVencimento = d.DataVencimento, Pago = d.Pago, Categoria = d.Categoria, RecorrenciaId = d.RecorrenciaId };

    private static RegistroDto MapToDto(RegistroDiario r) => new()
    {
        Id = r.Id,
        ClienteId = r.ClienteId,
        Data = r.Data,
        Inicio = r.Inicio,
        Entradas = r.Entradas.Select(s => new ItemFinanceiroDto { Descricao = s.Descricao, Valor = s.Valor, Categoria = s.Categoria, TipoCusto = s.TipoCusto }).ToList(),
        Saidas = r.Saidas.Select(s => new ItemFinanceiroDto { Descricao = s.Descricao, Valor = s.Valor, Categoria = s.Categoria, TipoCusto = s.TipoCusto }).ToList(),
        ContasReceber = r.ContasReceber.Select(s => new ContaProvisionadaDto { Descricao = s.Descricao, Valor = s.Valor, DataVencimento = s.DataVencimento, Pago = s.Pago, Categoria = s.Categoria, RecorrenciaId = s.RecorrenciaId }).ToList(),
        ContasPagar = r.ContasPagar.Select(s => new ContaProvisionadaDto { Descricao = s.Descricao, Valor = s.Valor, DataVencimento = s.DataVencimento, Pago = s.Pago, Categoria = s.Categoria, RecorrenciaId = s.RecorrenciaId }).ToList(),
        SaldoFinal = r.SaldoFinal,
        SalvoEm = r.SalvoEm
    };
}
