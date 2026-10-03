using System.Globalization;
using CaixaDiario.API.DTOs.SaudeFinanceira;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class SaudeFinanceiraService : ISaudeFinanceiraService
{
    public SaudeFinanceiraDto Calcular(
        List<RegistroDiario> registros,
        List<MetaAnual> metas)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        // Último mês FECHADO (mês anterior ao atual), não o corrente — Taxa de Poupança e
        // Comprometimento Fixo são indicadores mensais, incompletos enquanto o mês ainda não
        // terminou (mostravam "sem receita" no dia 1, ou um número com poucos dias de dados).
        // Comprometimento já usava essa mesma janela internamente (média dos últimos 3 meses
        // contados a partir de "hoje", o que já exclui o mês corrente); Taxa de Poupança usava o
        // mês corrente — essa divergência interna era parte do bug relatado.
        var mesFechado = hoje.AddMonths(-1);
        var anoRef = mesFechado.Year;
        var mesRef = mesFechado.Month;
        var reg = registros.Where(r => !r.Excluido).ToList();
        var nomeMes = CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetMonthName(mesRef);

        return new SaudeFinanceiraDto
        {
            Periodo              = $"{char.ToUpperInvariant(nomeMes[0])}{nomeMes[1..]}/{anoRef} · último mês fechado",
            TaxaPoupanca        = CalcTaxaPoupanca(reg, anoRef, mesRef),
            ComprometimentoFixos = CalcComprometimento(reg, hoje),
            RitmoMeta           = CalcRitmoMeta(metas, hoje),
        };
    }

    // ── 1. Taxa de Poupança ─────────────────────────────────────────────────
    private static GaugeIndicadorDto CalcTaxaPoupanca(
        List<RegistroDiario> reg, int ano, int mes)
    {
        var doMes = reg.Where(r => r.Data.Year == ano && r.Data.Month == mes).ToList();
        var receita  = doMes.SelectMany(r => r.Entradas).Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor);
        var despesas = doMes.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor);

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
    // Numerador e denominador usam o mesmo critério — média dos últimos 3 meses de valores
    // REALIZADOS (Entradas/Saídas de verdade), não projeção de Contas a Pagar/Recorrências do mês
    // corrente. Essas duas fontes formais ficavam de fora do numerador: um cliente que não usa
    // Contas a Pagar/Recorrências (registra despesa fixa como saída comum, categorizada CustoFixo)
    // via zerava o indicador mesmo tendo despesa fixa real todo mês.
    private static GaugeIndicadorDto CalcComprometimento(List<RegistroDiario> reg, DateOnly hoje)
    {
        decimal MediaUltimosTresMeses(Func<RegistroDiario, decimal> somaDoRegistro)
        {
            var valores = Enumerable.Range(1, 3)
                .Select(i => hoje.AddMonths(-i))
                .Select(m => reg.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month).Sum(somaDoRegistro))
                .Where(v => v > 0).ToList();
            return valores.Count > 0 ? valores.Average() : 0m;
        }

        var receitaEsperada = MediaUltimosTresMeses(
            r => r.Entradas.Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor));

        if (receitaEsperada <= 0)
            return Indisponivel("Comprometimento Fixo",
                "Percentual da receita preso em despesas fixas e recorrentes.",
                "Sem histórico de receita nos últimos 3 meses.");

        var fixos = MediaUltimosTresMeses(
            r => r.Saidas.Where(s => s.TipoCusto == "CustoFixo").Sum(s => s.Valor));

        var comprVal = fixos / receitaEsperada * 100m;

        return new GaugeIndicadorDto
        {
            Titulo           = "Comprometimento Fixo",
            Valor            = Math.Round(comprVal, 1),
            ValorNormalizado = Math.Max(0m, Math.Min(100m, comprVal)),
            Semaforo         = comprVal <= 40m ? "verde" : comprVal <= 70m ? "amarelo" : "vermelho",
            Descricao        = "Percentual da receita comprometida com despesas fixas, média dos últimos 3 meses. Abaixo de 40% é saudável.",
            Calculo          = $"Despesas fixas ÷ Receita, média 3 meses = {comprVal:F1}%",
            Disponivel       = true,
        };
    }

    // ── 3. Ritmo da Meta ────────────────────────────────────────────────────
    // Proporcional e linear, não mais "aporte planejado vs. aporte necessário agora" a juros
    // compostos — essa conta travava num "Dados insuficientes, aguarde 1 mês" sempre que
    // mesesDecorridos <= 0 (meta recém-criada), e não dava pra mostrar nada em R$ nesse momento,
    // que é exatamente quando o usuário mais quer ver o card reagir. Acumulado (TotalInvestido)
    // é comparado direto com o esperado linear até hoje (ValorSonho × fração do prazo decorrida):
    // funciona desde o dia 1 da meta (fração = 0, esperado = 0) e dá uma diferença em R$ sempre.
    private static GaugeIndicadorDto CalcRitmoMeta(List<MetaAnual> metas, DateOnly hoje)
    {
        var elegiveis = metas.Where(m => m.ModoMeta == "metodo" && m.ValorSonho > 0).ToList();

        if (elegiveis.Count == 0)
            return Indisponivel("Ritmo da Meta",
                "Compara o quanto já foi investido com o esperado linear até hoje, para a meta de sonho.",
                "Nenhuma meta de investimento configurada.");

        GaugeIndicadorDto? pior = null;
        decimal piorRazao = decimal.MaxValue;
        var algumaAtingida = false;

        foreach (var meta in elegiveis)
        {
            var label = string.IsNullOrWhiteSpace(meta.Sonho) ? "meta" : $"\"{meta.Sonho}\"";

            if (meta.TotalInvestido >= meta.ValorSonho)
            {
                algumaAtingida = true;
                continue;
            }

            var dataInicio = DateOnly.FromDateTime(meta.AtualizadoEm);
            var dataAlvo = meta.DataAlvo ?? dataInicio.AddMonths(meta.PrazoAnos * 12);
            var prazoTotalMeses = MesesEntre(dataInicio, dataAlvo);
            if (prazoTotalMeses <= 0) continue; // dado de prazo inconsistente — não dá pra projetar

            var mesesDecorridos = Math.Max(0, MesesEntre(dataInicio, hoje));
            var fracaoDecorrida = Math.Min(1m, (decimal)mesesDecorridos / prazoTotalMeses);
            var esperado = meta.ValorSonho * fracaoDecorrida;
            var diferenca = meta.TotalInvestido - esperado;
            // Nada esperado ainda (meta criada agora): qualquer valor já investido conta como
            // adiantado; sem nada investido, trata como "no ritmo" em vez de indisponível.
            var razao = esperado > 0 ? meta.TotalInvestido / esperado : (meta.TotalInvestido > 0 ? 1.5m : 1m);

            if (razao < piorRazao)
            {
                piorRazao = razao;
                var status = razao >= 1.1m ? "Adiantado" : razao >= 0.9m ? "No ritmo" : "Atrasado";
                pior = new GaugeIndicadorDto
                {
                    Titulo           = "Ritmo da Meta",
                    Valor            = Math.Round(razao * 100m, 1),
                    ValorNormalizado = Math.Max(0m, Math.Min(100m, razao * 100m)),
                    Semaforo         = razao >= 0.9m ? "verde" : razao >= 0.7m ? "amarelo" : "vermelho",
                    Descricao        = "Compara o quanto já foi investido com o esperado linear até hoje, para a meta de sonho.",
                    Calculo          = $"Investido vs. esperado linear até hoje, {Math.Round(fracaoDecorrida * 100m, 0)}% do prazo decorrido ({label})",
                    Disponivel       = true,
                    StatusRitmo      = status,
                    DiferencaReais   = Math.Round(diferenca, 2),
                };
            }
        }

        if (pior != null) return pior;

        if (algumaAtingida)
            return new GaugeIndicadorDto
            {
                Titulo = "Ritmo da Meta", Valor = 100m, ValorNormalizado = 100m, Semaforo = "verde",
                Descricao = "Compara o quanto já foi investido com o esperado linear até hoje, para a meta de sonho.",
                Calculo = "Meta de sonho já atingida.", Disponivel = true,
                StatusRitmo = "Atingida", DiferencaReais = 0m,
            };

        return Indisponivel("Ritmo da Meta",
            "Compara o quanto já foi investido com o esperado linear até hoje, para a meta de sonho.",
            "Meta sem prazo configurado (defina o prazo ou a data-alvo).");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static int MesesEntre(DateOnly inicio, DateOnly fim) =>
        (fim.Year - inicio.Year) * 12 + (fim.Month - inicio.Month);

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
