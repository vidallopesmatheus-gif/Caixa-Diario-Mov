using System.Text.RegularExpressions;
using CaixaDiario.API.DTOs.Conciliacao;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Fase 1.2: motor de sugestão de vínculo entre títulos pendentes (ContaProvisionada, Pago=false)
// e lançamentos já existentes no extrato (Entrada/Saída) ainda não vinculados a nenhum outro
// título — evita que o usuário dê baixa manual criando um lançamento duplicado quando o dinheiro
// já apareceu no extrato importado. Não há cache/job: a tela re-busca após cada import ou baixa.
public class ConciliacaoService : IConciliacaoService
{
    private const int JanelaDias = 5;
    private const decimal TolerenciaValor = 0.30m;
    private const int ScoreMinimo = 50;

    private readonly IRegistroRepository _registroRepo;

    public ConciliacaoService(IRegistroRepository registroRepo) => _registroRepo = registroRepo;

    public async Task<List<SugestaoVinculoDto>> ListarSugestoesAsync(
        Guid clienteId, DateOnly de, DateOnly ate, Guid usuarioLogadoId, string perfil, Guid? contaBancariaId = null)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        if (ate < de)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Data final não pode ser anterior à inicial.", "ate");

        // Janela estendida: um lançamento pode casar com um título vencido até 5 dias antes/depois
        // do período pedido pela tela.
        var registros = await _registroRepo.ListarPorPeriodoAsync(clienteId, de.AddDays(-JanelaDias), ate.AddDays(JanelaDias));

        var vinculados = ColetarLancamentosJaVinculados(registros);
        var pendentes = ColetarPendentes(registros, de, ate)
            .Where(p => !contaBancariaId.HasValue || !p.ContaBancariaId.HasValue || p.ContaBancariaId == contaBancariaId)
            .ToList();
        var candidatosPorTipo = ColetarCandidatos(registros, vinculados, contaBancariaId);

        var triplas = new List<(ContaProvisionadaPendente Pendente, Candidato Candidato, int Score)>();
        foreach (var pendente in pendentes)
        {
            var candidatos = candidatosPorTipo.GetValueOrDefault(pendente.Tipo, new List<Candidato>());
            foreach (var candidato in candidatos)
            {
                if (pendente.ContaBancariaId.HasValue && candidato.ContaBancariaId != pendente.ContaBancariaId.Value)
                    continue;

                var score = CalcularScore(pendente, candidato);
                if (score >= ScoreMinimo)
                    triplas.Add((pendente, candidato, score));
            }
        }

        // Atribuição gulosa: cada título e cada lançamento só podem aparecer em uma sugestão —
        // evita sugerir o mesmo lançamento pra dois títulos diferentes.
        var pendentesUsados = new HashSet<Guid>();
        var candidatosUsados = new HashSet<Guid>();
        var resultado = new List<SugestaoVinculoDto>();

        foreach (var (pendente, candidato, score) in triplas.OrderByDescending(t => t.Score).ThenBy(t => Math.Abs((t.Candidato.Data.DayNumber - (t.Pendente.DataVencimento?.DayNumber ?? t.Candidato.Data.DayNumber)))))
        {
            if (pendentesUsados.Contains(pendente.Id) || candidatosUsados.Contains(candidato.Id))
                continue;

            pendentesUsados.Add(pendente.Id);
            candidatosUsados.Add(candidato.Id);

            resultado.Add(new SugestaoVinculoDto
            {
                ContaProvisionadaId = pendente.Id,
                Tipo = pendente.Tipo,
                Descricao = pendente.Descricao,
                Valor = pendente.Valor,
                DataVencimento = pendente.DataVencimento,
                ContaBancariaId = candidato.ContaBancariaId,
                LancamentoId = candidato.Id,
                LancamentoDescricao = candidato.Descricao,
                LancamentoValor = candidato.Valor,
                LancamentoData = candidato.Data,
                Score = score,
            });
        }

        return resultado.OrderByDescending(r => r.Score).ThenBy(r => r.DataVencimento).ToList();
    }

    private static int CalcularScore(ContaProvisionadaPendente pendente, Candidato candidato)
    {
        if (pendente.DataVencimento is null) return 0;

        var diffDias = Math.Abs(candidato.Data.DayNumber - pendente.DataVencimento.Value.DayNumber);
        if (diffDias > JanelaDias) return 0;

        if (pendente.Valor == 0) return 0;
        var diffPercent = Math.Abs(candidato.Valor - pendente.Valor) / pendente.Valor;
        if (diffPercent > TolerenciaValor) return 0;

        var scoreData = 40m * (1 - (decimal)diffDias / JanelaDias);
        var scoreValor = 30m * (1 - diffPercent / TolerenciaValor);
        var scoreDescricao = CalcularScoreDescricao(pendente.Descricao, candidato.Descricao);
        var scoreCategoria = pendente.Categoria != null && candidato.Categoria != null
            && string.Equals(pendente.Categoria, candidato.Categoria, StringComparison.OrdinalIgnoreCase)
            ? 10m
            : 0m;

        return (int)Math.Round(scoreData + scoreValor + scoreDescricao + scoreCategoria, MidpointRounding.AwayFromZero);
    }

    private static decimal CalcularScoreDescricao(string descricaoPendente, string descricaoCandidato)
    {
        var docPendente = DescricaoMatcher.ExtrairDocumento(descricaoPendente);
        var docCandidato = DescricaoMatcher.ExtrairDocumento(descricaoCandidato);
        if (docPendente != null && docPendente == docCandidato) return 20m;

        var tokensPendente = Tokenizar(descricaoPendente);
        var tokensCandidato = Tokenizar(descricaoCandidato);
        if (tokensPendente.Count == 0 || tokensCandidato.Count == 0) return 0m;

        var intersecao = tokensPendente.Intersect(tokensCandidato).Count();
        var uniao = tokensPendente.Union(tokensCandidato).Count();
        if (uniao == 0) return 0m;

        return 20m * intersecao / uniao;
    }

    private static HashSet<string> Tokenizar(string descricao) =>
        Regex.Matches(descricao.ToUpperInvariant(), "[A-Z0-9]+")
            .Select(m => m.Value)
            .Where(t => t.Length >= 3)
            .ToHashSet();

    private static HashSet<Guid> ColetarLancamentosJaVinculados(List<RegistroDiario> registros)
    {
        var ids = new HashSet<Guid>();
        foreach (var r in registros)
        {
            foreach (var c in r.ContasReceber.Where(c => c.LancamentoVinculadoId.HasValue))
                ids.Add(c.LancamentoVinculadoId!.Value);
            foreach (var c in r.ContasPagar.Where(c => c.LancamentoVinculadoId.HasValue))
                ids.Add(c.LancamentoVinculadoId!.Value);
        }
        return ids;
    }

    private static List<ContaProvisionadaPendente> ColetarPendentes(List<RegistroDiario> registros, DateOnly de, DateOnly ate)
    {
        var lista = new List<ContaProvisionadaPendente>();
        foreach (var r in registros)
        {
            foreach (var c in r.ContasReceber.Where(c => !c.Pago && c.DataVencimento.HasValue && c.DataVencimento.Value >= de && c.DataVencimento.Value <= ate))
                lista.Add(new ContaProvisionadaPendente(c.Id, "Receber", c.Descricao, c.Valor, c.DataVencimento, c.Categoria, c.ContaBancariaId));
            foreach (var c in r.ContasPagar.Where(c => !c.Pago && c.DataVencimento.HasValue && c.DataVencimento.Value >= de && c.DataVencimento.Value <= ate))
                lista.Add(new ContaProvisionadaPendente(c.Id, "Pagar", c.Descricao, c.Valor, c.DataVencimento, c.Categoria, c.ContaBancariaId));
        }
        return lista;
    }

    private static Dictionary<string, List<Candidato>> ColetarCandidatos(List<RegistroDiario> registros, HashSet<Guid> vinculados, Guid? contaBancariaId)
    {
        var receber = new List<Candidato>();
        var pagar = new List<Candidato>();

        foreach (var r in registros)
        {
            if (r.ContaBancariaId is not Guid contaId) continue;
            if (contaBancariaId.HasValue && contaId != contaBancariaId.Value) continue;

            foreach (var e in r.Entradas.Where(e => EhCandidatoValido(e.Id, e.TransferenciaId, e.TipoCusto, vinculados)))
                receber.Add(new Candidato(e.Id, contaId, e.Descricao, e.Valor, r.Data, e.Categoria));

            foreach (var s in r.Saidas.Where(s => EhCandidatoValido(s.Id, s.TransferenciaId, s.TipoCusto, vinculados)))
                pagar.Add(new Candidato(s.Id, contaId, s.Descricao, s.Valor, r.Data, s.Categoria));
        }

        return new Dictionary<string, List<Candidato>> { ["Receber"] = receber, ["Pagar"] = pagar };
    }

    private static bool EhCandidatoValido(Guid id, Guid? transferenciaId, string? tipoCusto, HashSet<Guid> vinculados) =>
        id != Guid.Empty && !transferenciaId.HasValue && tipoCusto != "Transferencia" && !vinculados.Contains(id);

    private sealed record ContaProvisionadaPendente(Guid Id, string Tipo, string Descricao, decimal Valor, DateOnly? DataVencimento, string? Categoria, Guid? ContaBancariaId);

    private sealed record Candidato(Guid Id, Guid ContaBancariaId, string Descricao, decimal Valor, DateOnly Data, string? Categoria);
}
