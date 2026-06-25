using CaixaDiario.API.DTOs.SaudeFinanceira;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class SaudeFinanceiraService : ISaudeFinanceiraService
{
    public SaudeFinanceiraDto Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        List<MetaAnual> metas)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var anoAtual = hoje.Year;
        var mesAtual = hoje.Month;
        var diasNoMes = DateTime.DaysInMonth(anoAtual, mesAtual);
        var reg = registros.Where(r => !r.Excluido).ToList();

        return new SaudeFinanceiraDto
        {
            TaxaPoupanca        = CalcTaxaPoupanca(reg, anoAtual, mesAtual),
            ComprometimentoFixos = CalcComprometimento(reg, recorrentes, hoje, anoAtual, mesAtual, diasNoMes),
            RitmoMeta           = CalcRitmoMeta(metas, anoAtual, mesAtual),
        };
    }

    // ── 1. Taxa de Poupança ─────────────────────────────────────────────────
    private static GaugeIndicadorDto CalcTaxaPoupanca(
        List<RegistroDiario> reg, int ano, int mes)
    {
        var doMes = reg.Where(r => r.Data.Year == ano && r.Data.Month == mes).ToList();
        var receita  = doMes.SelectMany(r => r.Entradas).Sum(e => e.Valor);
        var despesas = doMes.SelectMany(r => r.Saidas).Sum(s => s.Valor);

        if (receita <= 0)
            return Indisponivel("Taxa de Poupança",
                "Quanto da receita mensal sobra após todas as despesas.",
                "Sem receitas registradas neste mês.");

        var taxa = (receita - despesas) / receita * 100m;
        return new GaugeIndicadorDto
        {
            Titulo           = "Taxa de Poupança",
            Valor            = Math.Round(taxa, 1),
            ValorNormalizado = Math.Max(0m, Math.Min(100m, taxa)),
            Semaforo         = taxa >= 20m ? "verde" : taxa >= 5m ? "amarelo" : "vermelho",
            Descricao        = "Quanto da receita mensal sobra após todas as despesas. Acima de 20% é saudável.",
            Calculo          = $"(Receita − Despesas) ÷ Receita = {taxa:F1}%",
            Disponivel       = true,
        };
    }

    // ── 2. Comprometimento com Fixos ────────────────────────────────────────
    private static GaugeIndicadorDto CalcComprometimento(
        List<RegistroDiario> reg, List<ContaRecorrente> recorrentes,
        DateOnly hoje, int ano, int mes, int diasNoMes)
    {
        // Receita esperada = média dos últimos 3 meses
        var receitasMeses = Enumerable.Range(1, 3)
            .Select(i => hoje.AddMonths(-i))
            .Select(m => reg.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month)
                           .SelectMany(r => r.Entradas).Sum(e => e.Valor))
            .Where(v => v > 0).ToList();
        var receitaEsperada = receitasMeses.Count > 0 ? receitasMeses.Average() : 0m;

        if (receitaEsperada <= 0)
            return Indisponivel("Comprometimento Fixo",
                "Percentual da receita preso em despesas fixas e recorrentes.",
                "Sem histórico de receita nos últimos 3 meses.");

        // Contas a pagar com vencimento este mês
        var contasPagar = reg
            .SelectMany(r => r.ContasPagar)
            .Where(c => !c.Pago && c.DataVencimento.HasValue
                && c.DataVencimento.Value.Year == ano
                && c.DataVencimento.Value.Month == mes)
            .Sum(c => c.Valor);

        // Recorrências "Pagar" que ocorrem em qualquer dia deste mês
        decimal recorrPagar = 0m;
        for (int d = 1; d <= diasNoMes; d++)
        {
            var dia = new DateOnly(ano, mes, d);
            recorrPagar += recorrentes
                .Where(r => r.Tipo == "Pagar" && r.Ativo && RecorrenciaService.OcorreEm(r, dia))
                .Sum(r => r.Valor);
        }

        var fixos = contasPagar + recorrPagar;
        var comprVal = fixos / receitaEsperada * 100m;

        return new GaugeIndicadorDto
        {
            Titulo           = "Comprometimento Fixo",
            Valor            = Math.Round(comprVal, 1),
            ValorNormalizado = Math.Max(0m, Math.Min(100m, comprVal)),
            Semaforo         = comprVal <= 40m ? "verde" : comprVal <= 70m ? "amarelo" : "vermelho",
            Descricao        = "Percentual da receita esperada comprometida com fixos. Abaixo de 40% é saudável.",
            Calculo          = $"Despesas fixas ÷ Receita esperada = {comprVal:F1}%",
            Disponivel       = true,
        };
    }

    // ── 3. Ritmo da Meta ────────────────────────────────────────────────────
    private static GaugeIndicadorDto CalcRitmoMeta(
        List<MetaAnual> metas, int anoAtual, int mesAtual)
    {
        var elegiveis = metas.Where(m =>
            m.ModoMeta == "metodo" && m.ValorSonho > 0
            && m.PrazoAnos > 0 && m.TaxaRetorno > 0).ToList();

        if (elegiveis.Count == 0)
            return Indisponivel("Ritmo da Meta",
                "Progresso real vs. ritmo necessário para atingir a meta de sonho.",
                "Nenhuma meta de investimento configurada.");

        decimal? piorRitmo = null;
        string melhorCalculo = "";

        foreach (var meta in elegiveis)
        {
            var aporteMensal = CalcularAporteMensal(meta);
            if (aporteMensal <= 0) continue;

            var prazoTotal       = meta.PrazoAnos * 12;
            var mesesDecorridos  = (anoAtual - meta.AtualizadoEm.Year) * 12
                                 + (mesAtual  - meta.AtualizadoEm.Month);
            var mesesRestantes   = prazoTotal - mesesDecorridos;

            if (mesesDecorridos <= 0 || mesesRestantes <= 1) continue;

            var i = Math.Pow(1 + (double)meta.TaxaRetorno / 100, 1.0 / 12) - 1;
            var fvCorrigido  = (double)meta.TotalInvestido * Math.Pow(1 + i, mesesRestantes);
            var fvNecessario = (double)meta.ValorSonho - fvCorrigido;

            decimal ritmo;
            if (fvNecessario <= 0)
            {
                ritmo = 150m; // meta já atingida
            }
            else
            {
                var aporteAgora = (decimal)((fvNecessario * i) / (Math.Pow(1 + i, mesesRestantes) - 1));
                if (aporteAgora <= 0) continue;
                ritmo = aporteMensal / aporteAgora * 100m;
            }

            // Guarda o pior ritmo (menor valor = mais atrasado)
            if (piorRitmo == null || ritmo < piorRitmo)
            {
                piorRitmo = ritmo;
                var label = string.IsNullOrWhiteSpace(meta.Sonho) ? "meta" : $"\"{meta.Sonho}\"";
                melhorCalculo = $"Aporte planejado ÷ Aporte necessário agora = {ritmo:F1}% ({label})";
            }
        }

        if (piorRitmo == null)
            return Indisponivel("Ritmo da Meta",
                "Progresso real vs. ritmo necessário para atingir a meta de sonho.",
                "Dados insuficientes — aguarde ao menos 1 mês após configurar a meta.");

        var val = Math.Round(piorRitmo.Value, 1);
        return new GaugeIndicadorDto
        {
            Titulo           = "Ritmo da Meta",
            Valor            = val,                              // pode ser >100 se adiantado
            ValorNormalizado = Math.Max(0m, Math.Min(100m, val)), // arco vai de 0 a 100
            Semaforo         = val >= 95m ? "verde" : val >= 75m ? "amarelo" : "vermelho",
            Descricao        = "Compara o aporte planejado com o aporte necessário agora. 100% = exatamente no ritmo; acima = adiantado; abaixo = atrasado.",
            Calculo          = melhorCalculo,
            Disponivel       = true,
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static decimal CalcularAporteMensal(MetaAnual meta)
    {
        if (meta.ValorSonho <= 0 || meta.PrazoAnos <= 0 || meta.TaxaRetorno <= 0) return 0m;
        var n = meta.PrazoAnos * 12;
        var i = Math.Pow(1 + (double)meta.TaxaRetorno / 100, 1.0 / 12) - 1;
        var fvAtual      = (double)meta.TotalInvestido * Math.Pow(1 + i, n);
        var fvNecessario = (double)meta.ValorSonho - fvAtual;
        if (fvNecessario <= 0) return 0m;
        return (decimal)((fvNecessario * i) / (Math.Pow(1 + i, n) - 1));
    }

    private static GaugeIndicadorDto Indisponivel(string titulo, string descricao, string calculo) =>
        new()
        {
            Titulo     = titulo,
            Descricao  = descricao,
            Calculo    = calculo,
            Semaforo   = "cinza",
            Disponivel = false,
        };
}
