using CaixaDiario.API.DTOs.ContasBancarias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class ContaBancariaService : IContaBancariaService
{
    private static readonly HashSet<string> TiposValidos = new() { "Caixa", "ContaCorrente", "Investimento" };

    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;

    public ContaBancariaService(IContaBancariaRepository contaRepo, IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
    }

    public async Task<List<ContaBancariaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        return contas.Select(c => MapToDto(c, registros)).ToList();
    }

    public async Task<ContaBancariaDto> ObterPorIdAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        var registros = await _registroRepo.ListarPorContaAsync(id);
        return MapToDto(conta, registros);
    }

    public async Task<ContaBancariaDto> CriarAsync(CriarContaBancariaDto dto, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(dto.ClienteId, usuarioLogadoId, perfil);
        if (!TiposValidos.Contains(dto.Tipo))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, $"Tipo inválido. Use: {string.Join(", ", TiposValidos)}");

        var conta = new ContaBancaria
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            Nome = dto.Nome.Trim(),
            Tipo = dto.Tipo,
            SaldoInicial = dto.SaldoInicial,
            Ativa = true,
            DataCriacao = DateTime.UtcNow,
        };

        var criada = await _contaRepo.AdicionarAsync(conta);
        return MapToDto(criada, new List<Models.RegistroDiario>());
    }

    public async Task<ContaBancariaDto> AtualizarAsync(Guid id, AtualizarContaBancariaDto dto, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        if (!TiposValidos.Contains(dto.Tipo))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, $"Tipo inválido. Use: {string.Join(", ", TiposValidos)}");

        conta.Nome = dto.Nome.Trim();
        conta.Tipo = dto.Tipo;
        conta.SaldoInicial = dto.SaldoInicial;
        conta.Ativa = dto.Ativa;

        var atualizada = await _contaRepo.AtualizarAsync(conta);
        var registros = await _registroRepo.ListarPorContaAsync(id);
        return MapToDto(atualizada, registros);
    }

    public async Task InativarAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        conta.Ativa = false;
        await _contaRepo.AtualizarAsync(conta);
    }

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }

    private static ContaBancariaDto MapToDto(ContaBancaria c, IEnumerable<Models.RegistroDiario> registros)
    {
        var regsOrdenados = registros
            .Where(r => r.ContaBancariaId == c.Id && !r.Excluido)
            .OrderByDescending(r => r.Data)
            .ToList();

        var saldoAtual = regsOrdenados.FirstOrDefault()?.SaldoFinal ?? c.SaldoInicial;

        return new ContaBancariaDto
        {
            Id = c.Id,
            ClienteId = c.ClienteId,
            Nome = c.Nome,
            Tipo = c.Tipo,
            SaldoInicial = c.SaldoInicial,
            SaldoAtual = saldoAtual,
            Ativa = c.Ativa,
            DataCriacao = c.DataCriacao,
        };
    }
}
