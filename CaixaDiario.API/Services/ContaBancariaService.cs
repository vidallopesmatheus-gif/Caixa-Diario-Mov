using CaixaDiario.API.DTOs.ContasBancarias;
using CaixaDiario.API.DTOs.Registros;
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

    public async Task<List<LancamentoExtratoDto>> ObterExtratoAsync(
        Guid contaId, Guid usuarioLogadoId, string perfil, DateOnly? de, DateOnly? ate)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var registros = (await _registroRepo.ListarPorContaAsync(contaId))
            .OrderBy(r => r.Data)
            .ToList();

        var linhas = new List<(DateOnly Data, LancamentoExtratoDto Dto)>();
        decimal saldo = conta.SaldoInicial;

        foreach (var r in registros)
        {
            // Sincroniza com o início do dia armazenado, evitando deriva por edições manuais de saldo.
            saldo = r.Inicio;

            foreach (var entrada in r.Entradas)
            {
                saldo += entrada.Valor;
                linhas.Add((r.Data, new LancamentoExtratoDto
                {
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = entrada.Descricao,
                    Categoria = entrada.Categoria,
                    Valor = entrada.Valor,
                    SaldoAcumulado = saldo,
                }));
            }

            foreach (var recebido in r.ContasReceber.Where(cp => cp.Pago && cp.DataBaixa == r.Data))
            {
                saldo += recebido.Valor;
                linhas.Add((r.Data, new LancamentoExtratoDto
                {
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = $"{recebido.Descricao} (recebimento)",
                    Categoria = recebido.Categoria,
                    Valor = recebido.Valor,
                    SaldoAcumulado = saldo,
                }));
            }

            foreach (var saida in r.Saidas)
            {
                saldo -= saida.Valor;
                linhas.Add((r.Data, new LancamentoExtratoDto
                {
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = saida.Descricao,
                    Categoria = saida.Categoria,
                    Valor = -saida.Valor,
                    SaldoAcumulado = saldo,
                }));
            }

            foreach (var pago in r.ContasPagar.Where(cp => cp.Pago && cp.DataBaixa == r.Data))
            {
                saldo -= pago.Valor;
                linhas.Add((r.Data, new LancamentoExtratoDto
                {
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = $"{pago.Descricao} (pagamento)",
                    Categoria = pago.Categoria,
                    Valor = -pago.Valor,
                    SaldoAcumulado = saldo,
                }));
            }
        }

        IEnumerable<(DateOnly Data, LancamentoExtratoDto Dto)> filtradas = linhas;
        if (de.HasValue) filtradas = filtradas.Where(l => l.Data >= de.Value);
        if (ate.HasValue) filtradas = filtradas.Where(l => l.Data <= ate.Value);

        return filtradas
            .OrderByDescending(l => l.Data)
            .Select(l => l.Dto)
            .ToList();
    }

    public async Task<PendenciasContaDto> ObterPendenciasAsync(Guid contaId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var registros = await _registroRepo.ListarPorClienteAsync(conta.ClienteId);

        var recebiveis = registros
            .SelectMany(r => r.ContasReceber)
            .Where(cp => !cp.Pago && cp.ContaBancariaId == contaId)
            .Select(MapProvisionadaToDto)
            .OrderBy(d => d.DataVencimento)
            .ToList();

        var pagamentos = registros
            .SelectMany(r => r.ContasPagar)
            .Where(cp => !cp.Pago && cp.ContaBancariaId == contaId)
            .Select(MapProvisionadaToDto)
            .OrderBy(d => d.DataVencimento)
            .ToList();

        return new PendenciasContaDto { Recebiveis = recebiveis, Pagamentos = pagamentos };
    }

    private static ContaProvisionadaDto MapProvisionadaToDto(ContaProvisionada c) => new()
    {
        Descricao = c.Descricao,
        Valor = c.Valor,
        DataVencimento = c.DataVencimento,
        Pago = c.Pago,
        Categoria = c.Categoria,
        RecorrenciaId = c.RecorrenciaId,
        DataBaixa = c.DataBaixa,
        ContaBancariaId = c.ContaBancariaId,
    };

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

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var regsDoMes = regsOrdenados.Where(r => r.Data.Year == hoje.Year && r.Data.Month == hoje.Month);
        var entradasMes = regsDoMes.SelectMany(r => r.Entradas).Sum(e => e.Valor);
        var saidasMes = regsDoMes.SelectMany(r => r.Saidas).Sum(s => s.Valor);

        return new ContaBancariaDto
        {
            Id = c.Id,
            ClienteId = c.ClienteId,
            Nome = c.Nome,
            Tipo = c.Tipo,
            SaldoInicial = c.SaldoInicial,
            SaldoAtual = saldoAtual,
            EntradasMes = entradasMes,
            SaidasMes = saidasMes,
            Ativa = c.Ativa,
            DataCriacao = c.DataCriacao,
        };
    }
}
