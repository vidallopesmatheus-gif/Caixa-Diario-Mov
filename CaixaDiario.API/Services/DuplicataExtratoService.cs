using System.Text.RegularExpressions;
using CaixaDiario.API.DTOs.ContasBancarias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Fase 0.5: "Revisar duplicatas" — acha prováveis pares de lançamentos reais da MESMA conta e do
// MESMO sentido que são a mesma transação importada duas vezes em arquivos/formatos diferentes do
// banco (ex.: CSV de um jeito, OFX de outro, descrições levemente diferentes). Nunca exclui nada
// sozinho: só sugere pares pra revisão manual — a exclusão usa o endpoint de sempre
// (ImportacaoService.ExcluirLancamentoAsync), que já sabe reabrir título vinculado e recalcular saldo.
public class DuplicataExtratoService : IDuplicataExtratoService
{
    private const int JanelaDias = 2;
    private const decimal ToleranciaValor = 0.01m;
    private const int ScoreMinimo = 40;

    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IDuplicataDispensadaRepository _dispensadaRepo;

    public DuplicataExtratoService(
        IContaBancariaRepository contaRepo, IRegistroRepository registroRepo, IDuplicataDispensadaRepository dispensadaRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _dispensadaRepo = dispensadaRepo;
    }

    public async Task<List<DuplicataProvavelDto>> ListarProvaveisAsync(Guid contaBancariaId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await VerificarAcessoAsync(contaBancariaId, usuarioLogadoId, perfil);

        var registros = (await _registroRepo.ListarPorContaAsync(contaBancariaId)).Where(r => !r.Excluido).ToList();

        var entradas = registros
            .SelectMany(r => r.Entradas.Where(EhCandidata).Select(e => new Item(r.Data, e.Id, e.Descricao, e.Valor)))
            .ToList();
        var saidas = registros
            .SelectMany(r => r.Saidas.Where(EhCandidata).Select(s => new Item(r.Data, s.Id, s.Descricao, s.Valor)))
            .ToList();

        var resultado = new List<DuplicataProvavelDto>();
        resultado.AddRange(EncontrarPares(entradas, "Entrada"));
        resultado.AddRange(EncontrarPares(saidas, "Saida"));

        // Item 3.2: "Manter os dois" — remove os pares que o usuário já revisou e decidiu manter,
        // senão eles voltam a aparecer em toda abertura do modal, pra sempre.
        var dispensados = (await _dispensadaRepo.ListarPorContaAsync(contaBancariaId))
            .Select(d => (d.LancamentoMenorId, d.LancamentoMaiorId))
            .ToHashSet();
        resultado = resultado.Where(d => !dispensados.Contains(ParOrdenado(d.LancamentoAId, d.LancamentoBId))).ToList();

        return resultado.OrderByDescending(d => d.Score).ToList();
    }

    public async Task ManterOsDoisAsync(Guid contaBancariaId, Guid lancamentoAId, Guid lancamentoBId, Guid usuarioLogadoId, string perfil)
    {
        await VerificarAcessoAsync(contaBancariaId, usuarioLogadoId, perfil);
        var (menor, maior) = ParOrdenado(lancamentoAId, lancamentoBId);
        var jaExiste = (await _dispensadaRepo.ListarPorContaAsync(contaBancariaId))
            .Any(d => d.LancamentoMenorId == menor && d.LancamentoMaiorId == maior);
        if (jaExiste) return;

        await _dispensadaRepo.AdicionarAsync(new DuplicataDispensada
        {
            Id = Guid.NewGuid(), ContaBancariaId = contaBancariaId,
            LancamentoMenorId = menor, LancamentoMaiorId = maior, CriadoEm = DateTime.UtcNow,
        });
    }

    private async Task<ContaBancaria> VerificarAcessoAsync(Guid contaBancariaId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaBancariaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (perfil == "cliente" && usuarioLogadoId != conta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return conta;
    }

    // Normaliza o par pela ordem do Guid — quem veio como "A" ou "B" na sugestão depende da
    // ordem de iteração (ver EncontrarPares), que pode mudar entre chamadas.
    private static (Guid Menor, Guid Maior) ParOrdenado(Guid a, Guid b) =>
        a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    // Transferências nunca entram: as duas pontas já têm par vinculado de verdade (TransferenciaId)
    // e vivem em contas/sentidos diferentes — nunca são a mesma transação duplicada na MESMA conta.
    private static bool EhCandidata(ItemFinanceiro e) =>
        e.Id != Guid.Empty && e.TransferenciaId == null && e.TipoCusto != "Transferencia" && !e.Provisoria;
    private static bool EhCandidata(ItemFinanceiroSaida s) =>
        s.Id != Guid.Empty && s.TransferenciaId == null && s.TipoCusto != "Transferencia" && !s.Provisoria;

    // Pool em memória: cada lançamento só pode aparecer em UM par (o de maior score), pra não
    // sugerir o mesmo item pra duas suspeitas diferentes.
    private static List<DuplicataProvavelDto> EncontrarPares(List<Item> itens, string tipo)
    {
        var candidatos = new List<(Item A, Item B, int Score)>();
        for (var i = 0; i < itens.Count; i++)
        {
            for (var j = i + 1; j < itens.Count; j++)
            {
                var a = itens[i];
                var b = itens[j];
                if (Math.Abs(a.Valor - b.Valor) > ToleranciaValor) continue;
                if (Math.Abs(a.Data.DayNumber - b.Data.DayNumber) > JanelaDias) continue;

                var score = CalcularScoreDescricao(a.Descricao, b.Descricao);
                if (score < ScoreMinimo) continue;
                candidatos.Add((a, b, score));
            }
        }

        var usados = new HashSet<Guid>();
        var resultado = new List<DuplicataProvavelDto>();
        foreach (var (a, b, score) in candidatos.OrderByDescending(c => c.Score))
        {
            if (usados.Contains(a.Id) || usados.Contains(b.Id)) continue;
            usados.Add(a.Id);
            usados.Add(b.Id);
            resultado.Add(new DuplicataProvavelDto
            {
                Tipo = tipo, Score = score,
                LancamentoAId = a.Id, DescricaoA = a.Descricao, DataA = a.Data.ToString("yyyy-MM-dd"), ValorA = a.Valor,
                LancamentoBId = b.Id, DescricaoB = b.Descricao, DataB = b.Data.ToString("yyyy-MM-dd"), ValorB = b.Valor,
            });
        }
        return resultado;
    }

    private static int CalcularScoreDescricao(string descricaoA, string descricaoB)
    {
        var docA = DescricaoMatcher.ExtrairDocumento(descricaoA);
        var docB = DescricaoMatcher.ExtrairDocumento(descricaoB);
        if (docA != null && docA == docB) return 100;

        var tokensA = Tokenizar(descricaoA);
        var tokensB = Tokenizar(descricaoB);
        if (tokensA.Count == 0 || tokensB.Count == 0) return 0;

        var intersecao = tokensA.Intersect(tokensB).Count();
        var uniao = tokensA.Union(tokensB).Count();
        return uniao == 0 ? 0 : (int)Math.Round(100.0 * intersecao / uniao);
    }

    private static HashSet<string> Tokenizar(string descricao) =>
        Regex.Matches(descricao.ToUpperInvariant(), "[A-Z0-9]+")
            .Select(m => m.Value)
            .Where(t => t.Length >= 3)
            .ToHashSet();

    private sealed record Item(DateOnly Data, Guid Id, string Descricao, decimal Valor);
}
