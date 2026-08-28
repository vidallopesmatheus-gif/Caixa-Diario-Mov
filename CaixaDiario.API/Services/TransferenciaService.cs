using System.Text.Json;
using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class TransferenciaService : ITransferenciaService
{
    private readonly ITransferenciaRepository _transferenciaRepo;
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IAuditService _auditService;

    public TransferenciaService(
        ITransferenciaRepository transferenciaRepo,
        IContaBancariaRepository contaRepo,
        IRegistroRepository registroRepo,
        IAuditService auditService)
    {
        _transferenciaRepo = transferenciaRepo;
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _auditService = auditService;
    }

    public async Task<TransferenciaDto> CriarAsync(CriarTransferenciaDto dto, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(dto.ClienteId, usuarioLogadoId, perfil);

        if (dto.Data > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ApiException(400, CodigoRetorno.DATA_FUTURA, "Não é possível registrar data futura.", "data");
        if (dto.ContaOrigemId == dto.ContaDestinoId)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Conta de origem e destino devem ser diferentes.");

        var origem = await ObterContaDoClienteAsync(dto.ContaOrigemId, dto.ClienteId);
        var destino = await ObterContaDoClienteAsync(dto.ContaDestinoId, dto.ClienteId);
        if (!origem.Ativa || !destino.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "Conta de origem e destino devem estar ativas.");

        var transferenciaId = Guid.NewGuid();
        var descricaoOrigem = string.IsNullOrWhiteSpace(dto.Descricao) ? $"Transferência para {destino.Nome}" : dto.Descricao;
        var descricaoDestino = string.IsNullOrWhiteSpace(dto.Descricao) ? $"Transferência de {origem.Nome}" : dto.Descricao;

        var (regOrigem, origemNovo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, origem, dto.Data);
        regOrigem.Saidas.Add(new ItemFinanceiroSaida
        {
            Descricao = descricaoOrigem,
            Valor = dto.Valor,
            Categoria = "Transferência",
            TipoCusto = LancamentoFiltro.TipoTransferencia,
            TransferenciaId = transferenciaId,
        });
        regOrigem.SaldoFinal -= dto.Valor;
        regOrigem.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regOrigem, origemNovo);

        var (regDestino, destinoNovo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, destino, dto.Data);
        regDestino.Entradas.Add(new ItemFinanceiro
        {
            Descricao = descricaoDestino,
            Valor = dto.Valor,
            Categoria = "Transferência",
            TipoCusto = LancamentoFiltro.TipoTransferencia,
            TransferenciaId = transferenciaId,
        });
        regDestino.SaldoFinal += dto.Valor;
        regDestino.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regDestino, destinoNovo);

        var transferencia = new Transferencia
        {
            Id = transferenciaId,
            ClienteId = dto.ClienteId,
            ContaOrigemId = origem.Id,
            ContaDestinoId = destino.Id,
            Data = dto.Data,
            Valor = dto.Valor,
            Descricao = dto.Descricao,
            CriadoEm = DateTime.UtcNow,
        };
        var criada = await _transferenciaRepo.AdicionarAsync(transferencia);

        var resultado = MapToDto(criada, origem.Nome, destino.Nome);
        await _auditService.LogAsync(dto.ClienteId, usuarioLogadoId, "Transferencia", "Criacao",
            transferenciaId.ToString(), null, JsonSerializer.Serialize(resultado));

        return resultado;
    }

    public async Task<List<TransferenciaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var transferencias = await _transferenciaRepo.ListarPorClienteAsync(clienteId);
        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var nomesPorId = contas.ToDictionary(c => c.Id, c => c.Nome);

        return transferencias
            .Select(t => MapToDto(t, nomesPorId.GetValueOrDefault(t.ContaOrigemId, "—"), nomesPorId.GetValueOrDefault(t.ContaDestinoId, "—")))
            .ToList();
    }

    public async Task EstornarAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var transferencia = await _transferenciaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.TRANSFERENCIA_NAO_ENCONTRADA, "Transferência não encontrada.");
        VerificarAcesso(transferencia.ClienteId, usuarioLogadoId, perfil);

        var regOrigem = await _registroRepo.ObterPorContaEDataAsync(transferencia.ContaOrigemId, transferencia.Data);
        if (regOrigem != null)
        {
            regOrigem.Saidas.RemoveAll(s => s.TransferenciaId == transferencia.Id);
            regOrigem.SaldoFinal += transferencia.Valor;
            regOrigem.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regOrigem);
        }

        var regDestino = await _registroRepo.ObterPorContaEDataAsync(transferencia.ContaDestinoId, transferencia.Data);
        if (regDestino != null)
        {
            regDestino.Entradas.RemoveAll(e => e.TransferenciaId == transferencia.Id);
            regDestino.SaldoFinal -= transferencia.Valor;
            regDestino.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regDestino);
        }

        await _transferenciaRepo.RemoverAsync(transferencia);
        await _auditService.LogAsync(transferencia.ClienteId, usuarioLogadoId, "Transferencia", "Estorno",
            transferencia.Id.ToString(), JsonSerializer.Serialize(transferencia), null);
    }

    private async Task<ContaBancaria> ObterContaDoClienteAsync(Guid contaId, Guid clienteId)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (conta.ClienteId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return conta;
    }

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }

    private static TransferenciaDto MapToDto(Transferencia t, string contaOrigemNome, string contaDestinoNome) => new()
    {
        Id = t.Id,
        ClienteId = t.ClienteId,
        ContaOrigemId = t.ContaOrigemId,
        ContaOrigemNome = contaOrigemNome,
        ContaDestinoId = t.ContaDestinoId,
        ContaDestinoNome = contaDestinoNome,
        Data = t.Data,
        Valor = t.Valor,
        Descricao = t.Descricao,
        CriadoEm = t.CriadoEm,
    };
}
