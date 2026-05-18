using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class RegistroService : IRegistroService
{
    private readonly IRegistroRepository _registroRepository;

    public RegistroService(IRegistroRepository registroRepository) => _registroRepository = registroRepository;

    public async Task<List<RegistroDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

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

        if (existente != null)
        {
            existente.Inicio = dto.Inicio;
            existente.Entrada = dto.Entrada;
            existente.Saidas = dto.Saidas.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList();
            existente.ContasReceber = dto.ContasReceber.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList();
            existente.ContasPagar = dto.ContasPagar.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList();
            existente.SaldoFinal = dto.SaldoFinal;
            existente.SalvoEm = DateTime.UtcNow;
            existente.AtualizadoEm = DateTime.UtcNow;
            existente.UsuarioAtualizacao = nomeUsuarioLogado;

            var atualizado = await _registroRepository.AtualizarAsync(existente);
            return (MapToDto(atualizado), false);
        }

        var novo = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            Data = dto.Data,
            Inicio = dto.Inicio,
            Entrada = dto.Entrada,
            Saidas = dto.Saidas.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
            ContasReceber = dto.ContasReceber.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
            ContasPagar = dto.ContasPagar.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
            SaldoFinal = dto.SaldoFinal,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
            UsuarioAtualizacao = nomeUsuarioLogado
        };

        var criado = await _registroRepository.AdicionarAsync(novo);
        return (MapToDto(criado), true);
    }

    public async Task ExcluirAsync(Guid clienteId, DateOnly data, string motivo, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ApiException(400, CodigoRetorno.MOTIVO_OBRIGATORIO, "Motivo de exclusão é obrigatório.", "motivo_exclusao");

        var registro = await _registroRepository.ObterPorClienteEDataAsync(clienteId, data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Registro não encontrado.");

        registro.Excluido = true;
        registro.MotivoExclusao = motivo;
        registro.AtualizadoEm = DateTime.UtcNow;
        registro.UsuarioAtualizacao = usuarioLogadoId.ToString();

        await _registroRepository.AtualizarAsync(registro);
    }

    private static RegistroDto MapToDto(RegistroDiario r) => new()
    {
        Id = r.Id,
        ClienteId = r.ClienteId,
        Data = r.Data,
        Inicio = r.Inicio,
        Entrada = r.Entrada,
        Saidas = r.Saidas.Select(s => new ItemFinanceiroDto { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
        ContasReceber = r.ContasReceber.Select(s => new ItemFinanceiroDto { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
        ContasPagar = r.ContasPagar.Select(s => new ItemFinanceiroDto { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
        SaldoFinal = r.SaldoFinal,
        SalvoEm = r.SalvoEm
    };
}
