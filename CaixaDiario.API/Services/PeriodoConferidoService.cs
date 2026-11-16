using System.Text.Json;
using CaixaDiario.API.DTOs.ConciliacaoSaldo;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public interface IPeriodoConferidoService
{
    // "Conferir saldo" que bateu com o banco: trava a conta até a data.
    Task TravarAsync(ContaBancaria conta, DateOnly data, decimal saldoBanco, Guid usuarioId, string perfil);
    Task<TravaPeriodoDto> ReabrirAsync(Guid contaId, ReabrirPeriodoDto dto, Guid usuarioId, string perfil);
    Task<List<ConferenciaSaldoDto>> ListarHistoricoAsync(Guid contaId, Guid usuarioId, string perfil);
    // O que foi criado, editado ou excluído desde a última conferência, dentro do que ela retratou.
    Task<List<MudancaDesdeConferenciaDto>> MudancasDesdeConferenciaAsync(ContaBancaria conta, List<RegistroDiario> registrosDaConta);
}

public class PeriodoConferidoService : IPeriodoConferidoService
{
    // Quanto a conferência guarda pra trás — cobre a reabertura de qualquer mês recente.
    private const int DiasRetratados = 120;

    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IConferenciaSaldoRepository _conferenciaRepo;
    private readonly IAuditService? _audit;
    private readonly TimeProvider _timeProvider;

    public PeriodoConferidoService(
        IContaBancariaRepository contaRepo, IRegistroRepository registroRepo, IConferenciaSaldoRepository conferenciaRepo,
        IAuditService? audit = null, TimeProvider? timeProvider = null)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _conferenciaRepo = conferenciaRepo;
        _audit = audit;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private DateTime Agora() => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task TravarAsync(ContaBancaria conta, DateOnly data, decimal saldoBanco, Guid usuarioId, string perfil)
    {
        var antes = conta.TravadoAte;
        // Conferir uma data anterior à trava não encurta a trava.
        if (antes is not { } a || data >= a)
        {
            conta.TravadoAte = data;
            conta.TravaSaldoBanco = saldoBanco;
            conta.TravadoPorConsultor = perfil == "admin";
            await _contaRepo.AtualizarAsync(conta);
        }

        var registros = (await _registroRepo.ListarPorContaAsync(conta.Id)).Where(r => !r.Excluido).ToList();
        await _conferenciaRepo.AdicionarAsync(new ConferenciaSaldo
        {
            Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Tipo = ConferenciaSaldo.TipoConferencia,
            Data = data, SaldoBanco = saldoBanco, TravadoAteAntes = antes, TravadoAteDepois = conta.TravadoAte,
            UsuarioId = usuarioId, Perfil = perfil, Retrato = JsonSerializer.Serialize(Retratar(registros, data)), OcorridoEm = Agora(),
        });
    }

    public async Task<TravaPeriodoDto> ReabrirAsync(Guid contaId, ReabrirPeriodoDto dto, Guid usuarioId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (perfil == "cliente" && usuarioId != conta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        if (conta.TravadoAte is not { } travadoAte)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Esta conta não tem período conferido.");
        if (conta.TravadoPorConsultor && perfil == "cliente")
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Período conferido pelo consultor — peça para reabrir.");
        if (dto.APartirDe is { } inicio && inicio > travadoAte)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, $"A conta está conferida só até {travadoAte:dd/MM/yyyy}.", "aPartirDe");

        var novaTrava = dto.APartirDe?.AddDays(-1);
        conta.TravadoAte = novaTrava;
        // O saldo do banco conferido era o da data antiga; o da nova trava não foi informado.
        conta.TravaSaldoBanco = null;
        if (novaTrava == null) conta.TravadoPorConsultor = false;
        await _contaRepo.AtualizarAsync(conta);

        var motivo = string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim();
        await _conferenciaRepo.AdicionarAsync(new ConferenciaSaldo
        {
            Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Tipo = ConferenciaSaldo.TipoReabertura,
            Data = dto.APartirDe, TravadoAteAntes = travadoAte, TravadoAteDepois = novaTrava,
            UsuarioId = usuarioId, Perfil = perfil, Motivo = motivo, OcorridoEm = Agora(),
        });
        if (_audit != null)
            await _audit.LogAsync(conta.ClienteId, usuarioId, "PeriodoConferido", "Edicao", conta.Id.ToString(),
                JsonSerializer.Serialize(new { travadoAte }),
                JsonSerializer.Serialize(new { travadoAte = novaTrava, aPartirDe = dto.APartirDe, motivo }));

        return new TravaPeriodoDto { TravadoAte = conta.TravadoAte, TravaSaldoBanco = conta.TravaSaldoBanco, TravadoPorConsultor = conta.TravadoPorConsultor };
    }

    public async Task<List<ConferenciaSaldoDto>> ListarHistoricoAsync(Guid contaId, Guid usuarioId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (perfil == "cliente" && usuarioId != conta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return (await _conferenciaRepo.ListarPorContaAsync(contaId)).Select(c => new ConferenciaSaldoDto
        {
            Tipo = c.Tipo, Data = c.Data, SaldoBanco = c.SaldoBanco, TravadoAteAntes = c.TravadoAteAntes,
            TravadoAteDepois = c.TravadoAteDepois, Quem = c.Perfil == "admin" ? "Consultor" : "Cliente",
            Motivo = c.Motivo, OcorridoEm = c.OcorridoEm,
        }).ToList();
    }

    public async Task<List<MudancaDesdeConferenciaDto>> MudancasDesdeConferenciaAsync(ContaBancaria conta, List<RegistroDiario> registrosDaConta)
    {
        var ultima = (await _conferenciaRepo.ListarPorContaAsync(conta.Id))
            .FirstOrDefault(c => c.Tipo == ConferenciaSaldo.TipoConferencia && c.Retrato != null);
        if (ultima?.Retrato == null || ultima.Data is not { } dataConferida) return new();
        var retrato = JsonSerializer.Deserialize<Retrato>(ultima.Retrato);
        if (retrato == null) return new();

        var antes = retrato.Linhas;
        var agora = Retratar(registrosDaConta.Where(r => !r.Excluido), dataConferida).Linhas;
        var mudancas = new List<MudancaDesdeConferenciaDto>();

        // Lançamento antigo sem Id (Guid.Empty) é identificado pelo conteúdo — só vira criado/excluído.
        static string Chave(LinhaRetrato l) => l.Id != Guid.Empty ? l.Id.ToString() : $"{l.Data}|{l.Valor}|{l.Descricao}";
        var porChaveAntes = antes.GroupBy(Chave).ToDictionary(g => g.Key, g => g.ToList());
        var porChaveAgora = agora.GroupBy(Chave).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (chave, linhasAgora) in porChaveAgora)
        {
            var linhasAntes = porChaveAntes.GetValueOrDefault(chave) ?? new();
            if (linhasAntes.Count == 1 && linhasAgora.Count == 1)
            {
                var (a, b) = (linhasAntes[0], linhasAgora[0]);
                if (a.Valor != b.Valor || a.Data != b.Data || a.Descricao != b.Descricao)
                    mudancas.Add(new() { Tipo = "Editado", Data = b.Data, Descricao = b.Descricao, ValorAntes = a.Valor, ValorDepois = b.Valor });
                continue;
            }
            foreach (var l in linhasAgora.Skip(linhasAntes.Count))
                mudancas.Add(new() { Tipo = "Criado", Data = l.Data, Descricao = l.Descricao, ValorDepois = l.Valor });
        }
        foreach (var (chave, linhasAntes) in porChaveAntes)
        {
            var qtdAgora = porChaveAgora.GetValueOrDefault(chave)?.Count ?? 0;
            if (linhasAntes.Count == 1 && qtdAgora == 1) continue;
            foreach (var l in linhasAntes.Skip(qtdAgora))
                mudancas.Add(new() { Tipo = "Excluido", Data = l.Data, Descricao = l.Descricao, ValorAntes = l.Valor });
        }
        return mudancas.OrderBy(m => m.Data).ToList();
    }

    private static Retrato Retratar(IEnumerable<RegistroDiario> registros, DateOnly ate)
    {
        var desde = ate.AddDays(-DiasRetratados);
        var linhas = registros.Where(r => r.Data >= desde && r.Data <= ate)
            .SelectMany(r => r.Entradas.Select(e => new LinhaRetrato(r.Data, e.Id, e.Valor, e.Descricao))
                .Concat(r.Saidas.Select(s => new LinhaRetrato(r.Data, s.Id, -s.Valor, s.Descricao))))
            .ToList();
        return new Retrato(desde, linhas);
    }

    private sealed record Retrato(DateOnly Desde, List<LinhaRetrato> Linhas);
    private sealed record LinhaRetrato(DateOnly Data, Guid Id, decimal Valor, string Descricao);
}
