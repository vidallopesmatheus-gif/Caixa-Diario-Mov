using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

/// <summary>Uma ponta de lançamento (entrada ou saída) de um dia de uma conta.</summary>
public sealed record PontaLancamento(
    RegistroDiario Registro, string Tipo, Guid ItemId, decimal Valor, string Descricao, string? FitId,
    bool Provisoria, Guid? TransferenciaId, string? TipoCusto, bool ContrapartidaConfirmada)
{
    public DateOnly Data => Registro.Data;
    public Guid ContaId => Registro.ContaBancariaId ?? Guid.Empty;
    public decimal ValorComSinal => Tipo == "Entrada" ? Valor : -Valor;
}

/// <summary>
/// "Transferência cria contrapartida falsa" — o que é par real e o que o app inventou. Tudo leitura
/// pura: a conversão (TransferenciaService), o diagnóstico de saldo e a revisão de transferências
/// usam as mesmas regras.
/// </summary>
public static class ContrapartidaAnalise
{
    public const decimal Tolerancia = 0.01m;
    public const int JanelaDias = 2;
    public const string PrefixoDescricao = "↔ Transferência";

    // A descrição da contrapartida nunca copia a do outro banco ("Pix recebido de…" numa saída do
    // Nubank parecia sinal trocado) — a original fica em Transferencia.Descricao.
    public static string DescricaoContrapartida(string tipoNestaConta, string nomeOutraConta) =>
        tipoNestaConta == "Entrada" ? $"{PrefixoDescricao} de {nomeOutraConta}" : $"{PrefixoDescricao} para {nomeOutraConta}";

    public static List<PontaLancamento> Pontas(IEnumerable<RegistroDiario> registros) =>
        registros.Where(r => !r.Excluido)
            .SelectMany(r => r.Entradas.Select(e => new PontaLancamento(r, "Entrada", e.Id, e.Valor, e.Descricao, e.FitId, e.Provisoria, e.TransferenciaId, e.TipoCusto, e.ContrapartidaConfirmada))
                .Concat(r.Saidas.Select(s => new PontaLancamento(r, "Saida", s.Id, s.Valor, s.Descricao, s.FitId, s.Provisoria, s.TransferenciaId, s.TipoCusto, s.ContrapartidaConfirmada))))
            .ToList();

    /// <summary>Lançamentos reais soltos que podem ser a outra ponta: mesmo sentido, valor ±0,01, data ±2 dias.</summary>
    public static List<PontaLancamento> CandidatosPar(IEnumerable<PontaLancamento> pontasDaConta, string tipo, decimal valor, DateOnly data) =>
        pontasDaConta.Where(p => p.Tipo == tipo && p.TransferenciaId == null && !p.Provisoria
                && Math.Abs(p.Valor - valor) <= Tolerancia && Math.Abs(p.Data.DayNumber - data.DayNumber) <= JanelaDias
                && !LancamentoFiltro.EhAjusteConciliacao(p.TipoCusto) && p.TipoCusto != LancamentoFiltro.TipoPagamentoFatura)
            .OrderBy(p => Math.Abs(p.Data.DayNumber - data.DayNumber))
            .ToList();

    // chavesImportadas: auditoria de importação da conta (ConciliacaoSaldoCalculo.ChaveImportada).
    public static bool VeioDoExtrato(PontaLancamento p, HashSet<(DateOnly, decimal, string)> chavesImportadas) =>
        p.FitId != null || chavesImportadas.Contains(ConciliacaoSaldoCalculo.ChaveImportada(p.Data, p.ValorComSinal, p.Descricao));

    // Ponta de transferência que o app inventou: não veio de extrato e é provisória, ou tem o rótulo
    // de contrapartida, ou copiou a descrição da outra ponta (o jeito antigo, sem a marca).
    public static bool FoiCriadaPeloApp(PontaLancamento p, PontaLancamento? outraPonta, HashSet<(DateOnly, decimal, string)> chavesImportadas) =>
        p.TransferenciaId != null && !VeioDoExtrato(p, chavesImportadas)
        && (p.Provisoria || p.Descricao.StartsWith(PrefixoDescricao, StringComparison.Ordinal)
            || (outraPonta != null && Normalizar(outraPonta.Descricao) == Normalizar(p.Descricao)));

    private static string Normalizar(string s) => s.Trim().ToUpperInvariant();
}

/// <summary>O que a revisão encontrou numa contrapartida criada pelo app.</summary>
public enum TipoAchadoContrapartida
{
    // Caso A: a conta tem a contrapartida criada E o lançamento real solto — a real substitui a criada.
    DuplicadaComReal,
    // Caso A (04/09): dois pares pra mesma transferência, cada um com uma ponta inventada — vira um par.
    ParDuplicado,
    // Caso B: a conta tem extrato na data e não tem o lançamento — o usuário decide pra onde foi.
    SemLancamentoNoExtrato,
    // Caso C: a conta não tem extrato na data — fica como provisória até o extrato chegar.
    AguardandoExtrato,
}

public sealed record AchadoContrapartida(
    TipoAchadoContrapartida Tipo, Transferencia Transferencia, PontaLancamento Criada, PontaLancamento? OutraPonta,
    PontaLancamento? Real, Transferencia? OutraTransferencia, PontaLancamento? OutraCriada);

public static class RevisaoContrapartidas
{
    /// <summary>
    /// Classifica cada contrapartida criada pelo app. diasComExtratoPorConta/chavesPorConta: o que
    /// cada conta já tem de extrato importado.
    /// </summary>
    public static List<AchadoContrapartida> Analisar(
        List<RegistroDiario> registrosCliente, List<Transferencia> transferencias,
        Dictionary<Guid, SortedSet<DateOnly>> diasComExtratoPorConta, Dictionary<Guid, HashSet<(DateOnly, decimal, string)>> chavesPorConta)
    {
        var pontas = ContrapartidaAnalise.Pontas(registrosCliente);
        var porTransferencia = pontas.Where(p => p.TransferenciaId.HasValue).GroupBy(p => p.TransferenciaId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var transferenciasPorId = transferencias.ToDictionary(t => t.Id);
        var pontasPorConta = pontas.GroupBy(p => p.ContaId).ToDictionary(g => g.Key, g => g.ToList());
        HashSet<(DateOnly, decimal, string)> Chaves(Guid contaId) => chavesPorConta.GetValueOrDefault(contaId) ?? new();

        PontaLancamento? Outra(PontaLancamento p) =>
            porTransferencia.GetValueOrDefault(p.TransferenciaId!.Value)?.FirstOrDefault(o => o.ItemId != p.ItemId);
        bool EhCriada(PontaLancamento p) => ContrapartidaAnalise.FoiCriadaPeloApp(p, Outra(p), Chaves(p.ContaId));

        var achados = new List<AchadoContrapartida>();
        var usados = new HashSet<Guid>();
        foreach (var criada in pontas.Where(p => p.TransferenciaId.HasValue && transferenciasPorId.ContainsKey(p.TransferenciaId.Value) && EhCriada(p))
                     .OrderBy(p => p.Data))
        {
            if (usados.Contains(criada.ItemId)) continue;
            var transferencia = transferenciasPorId[criada.TransferenciaId!.Value];
            var outra = Outra(criada);
            var daConta = pontasPorConta[criada.ContaId].Where(p => !usados.Contains(p.ItemId)).ToList();

            var solto = ContrapartidaAnalise.CandidatosPar(daConta, criada.Tipo, criada.Valor, criada.Data)
                .FirstOrDefault(p => ContrapartidaAnalise.VeioDoExtrato(p, Chaves(p.ContaId)));
            if (solto != null)
            {
                usados.UnionWith(new[] { criada.ItemId, solto.ItemId });
                achados.Add(new(TipoAchadoContrapartida.DuplicadaComReal, transferencia, criada, outra, solto, null, null));
                continue;
            }

            // A real desta conta já é ponta de OUTRA transferência cuja outra ponta também foi inventada.
            var deOutroPar = daConta.Where(p => p.Tipo == criada.Tipo && p.TransferenciaId.HasValue && p.TransferenciaId != criada.TransferenciaId
                    && Math.Abs(p.Valor - criada.Valor) <= ContrapartidaAnalise.Tolerancia
                    && Math.Abs(p.Data.DayNumber - criada.Data.DayNumber) <= ContrapartidaAnalise.JanelaDias
                    && transferenciasPorId.ContainsKey(p.TransferenciaId.Value) && !EhCriada(p))
                .Select(p => (Real: p, OutraCriada: Outra(p)))
                .FirstOrDefault(x => x.OutraCriada != null && !usados.Contains(x.OutraCriada.ItemId) && EhCriada(x.OutraCriada));
            if (deOutroPar.Real != null)
            {
                usados.UnionWith(new[] { criada.ItemId, deOutroPar.Real.ItemId, deOutroPar.OutraCriada!.ItemId });
                achados.Add(new(TipoAchadoContrapartida.ParDuplicado, transferencia, criada, outra, deOutroPar.Real,
                    transferenciasPorId[deOutroPar.Real.TransferenciaId!.Value], deOutroPar.OutraCriada));
                continue;
            }

            usados.Add(criada.ItemId);
            var temExtrato = diasComExtratoPorConta.GetValueOrDefault(criada.ContaId)?.Contains(criada.Data) ?? false;
            if (temExtrato && !criada.ContrapartidaConfirmada)
                achados.Add(new(TipoAchadoContrapartida.SemLancamentoNoExtrato, transferencia, criada, outra, null, null, null));
            else if (!temExtrato)
                achados.Add(new(TipoAchadoContrapartida.AguardandoExtrato, transferencia, criada, outra, null, null, null));
        }
        return achados;
    }
}
