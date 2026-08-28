using System.Globalization;
using CaixaDiario.API.DTOs.Insights;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class InsightService : IInsightService
{
    private static readonly CultureInfo _ptBR = CultureInfo.GetCultureInfo("pt-BR");

    private static string Brl(decimal v) => "R$ " + v.ToString("N2", _ptBR);

    public List<InsightDto> Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        MetaAnual? meta)
    {
        var insights = new List<InsightDto>();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var anoAtual = hoje.Year;
        var mesAtual = hoje.Month;
        var diasDecorridos = Math.Max(1, hoje.Day);
        var diasNoMes = DateTime.DaysInMonth(anoAtual, mesAtual);

        var registrosValidos = registros.Where(r => !r.Excluido).ToList();

        // ── 1. Saldo negativo projetado (próximos 30 dias) ───────────────
        var saldoAtual = registrosValidos.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0m;
        decimal saldoCorrendo = saldoAtual;
        DateOnly? primeiroDiaNegativo = null;

        for (int d = 1; d <= 30 && primeiroDiaNegativo == null; d++)
        {
            var dia = hoje.AddDays(d);

            var entradasProv  = registrosValidos.SelectMany(r => r.ContasReceber)
                .Where(c => !c.Pago && c.DataVencimento == dia).Sum(c => c.Valor);
            var saidasProv    = registrosValidos.SelectMany(r => r.ContasPagar)
                .Where(c => !c.Pago && c.DataVencimento == dia).Sum(c => c.Valor);
            var entradasRec   = recorrentes.Where(r => r.Tipo == "Receber" && r.Ativo && RecorrenciaService.OcorreEm(r, dia)).Sum(r => r.Valor);
            var saidasRec     = recorrentes.Where(r => r.Tipo == "Pagar" && r.Ativo && RecorrenciaService.OcorreEm(r, dia)).Sum(r => r.Valor);

            saldoCorrendo += entradasProv + entradasRec - saidasProv - saidasRec;
            if (saldoCorrendo < 0) primeiroDiaNegativo = dia;
        }

        if (primeiroDiaNegativo.HasValue)
        {
            var diasAte = primeiroDiaNegativo.Value.DayNumber - hoje.DayNumber;
            insights.Add(new InsightDto
            {
                Tipo = "alerta",
                Prioridade = 1,
                Texto = $"O saldo projetado ficará negativo em {diasAte} dia{(diasAte != 1 ? "s" : "")}.",
                Detalhe = "Reveja as saídas programadas ou antecipe recebimentos.",
                Categoria = "saldo",
            });
        }

        // ── 2. Gasto do mês atual vs. média dos 3 meses anteriores ───────
        // (transferências entre contas e rendimento não são gasto — ficam fora do alerta)
        var gastoMesAtual = registrosValidos
            .Where(r => r.Data.Year == anoAtual && r.Data.Month == mesAtual)
            .SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor);

        var gastosMesesAnteriores = Enumerable.Range(1, 3)
            .Select(i => hoje.AddMonths(-i))
            .Select(m => registrosValidos
                .Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month)
                .SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor))
            .Where(v => v > 0)
            .ToList();

        if (gastosMesesAnteriores.Count > 0)
        {
            var mediaMensal = gastosMesesAnteriores.Average();
            // Extrapola o gasto parcial para o mês cheio
            var gastoExtrapolado = gastoMesAtual / diasDecorridos * diasNoMes;

            if (mediaMensal > 0)
            {
                var deltaPct = (gastoExtrapolado - mediaMensal) / mediaMensal * 100m;

                if (deltaPct > 15m)
                {
                    insights.Add(new InsightDto
                    {
                        Tipo = "alerta",
                        Prioridade = 2,
                        Texto = $"Gastos deste mês estão {deltaPct:F0}% acima da média mensal no ritmo atual.",
                        Detalhe = $"Média: {Brl(mediaMensal)} · Projeção do mês: {Brl(gastoExtrapolado)}",
                        Categoria = "gasto",
                    });

                    // ── 3. Impacto dos gastos excessivos na meta de sonho ──────
                    if (meta?.ModoMeta == "metodo" && meta.PrazoAnos > 0 && meta.ValorSonho > 0 && meta.TaxaRetorno > 0)
                    {
                        var aporteMensal = CalcularAporteMensal(meta);
                        if (aporteMensal > 0)
                        {
                            var excesso = gastoExtrapolado - mediaMensal;
                            var pctDoAporte = excesso / aporteMensal * 100m;
                            if (pctDoAporte > 10m)
                            {
                                var sonho = NomeSonho(meta);
                                insights.Add(new InsightDto
                                {
                                    Tipo = "alerta",
                                    Prioridade = 3,
                                    Texto = $"O excesso de gastos deste mês equivale a {pctDoAporte:F0}% do aporte necessário{sonho}.",
                                    Detalhe = "Reduzir gastos pode acelerar o objetivo.",
                                    Categoria = "meta",
                                });
                            }
                        }
                    }
                }
                else if (deltaPct < -15m)
                {
                    insights.Add(new InsightDto
                    {
                        Tipo = "positivo",
                        Prioridade = 4,
                        Texto = $"Ótimo controle! Os gastos deste mês estão {Math.Abs(deltaPct):F0}% abaixo da média.",
                        Detalhe = $"Economia estimada: {Brl(mediaMensal - gastoExtrapolado)} em relação à média mensal.",
                        Categoria = "gasto",
                    });
                }
            }
        }

        // ── 4. Status da meta de sonho (adiantada ou atrasada) ───────────
        if (meta?.ModoMeta == "metodo" && meta.PrazoAnos > 0 && meta.ValorSonho > 0 && meta.TaxaRetorno > 0)
        {
            var aporteMensal = CalcularAporteMensal(meta);
            if (aporteMensal > 0)
            {
                var prazoTotal = meta.PrazoAnos * 12;
                var mesesDecorridos = (anoAtual - meta.AtualizadoEm.Year) * 12 + (mesAtual - meta.AtualizadoEm.Month);
                var mesesRestantes = prazoTotal - mesesDecorridos;

                if (mesesDecorridos > 0 && mesesRestantes > 1)
                {
                    var i = Math.Pow(1 + (double)meta.TaxaRetorno / 100, 1.0 / 12) - 1;
                    var fvCorrigido = (double)meta.TotalInvestido * Math.Pow(1 + i, mesesRestantes);
                    var fvNecessario = (double)meta.ValorSonho - fvCorrigido;
                    var sonho = NomeSonho(meta);

                    if (fvNecessario <= 0)
                    {
                        insights.Add(new InsightDto
                        {
                            Tipo = "positivo",
                            Prioridade = 2,
                            Texto = $"Parabéns! Você já atingiu a meta{sonho} antes do prazo.",
                            Categoria = "meta",
                        });
                    }
                    else
                    {
                        var aporteAgora = (decimal)((fvNecessario * i) / (Math.Pow(1 + i, mesesRestantes) - 1));

                        if (aporteAgora > aporteMensal * 1.1m)
                        {
                            insights.Add(new InsightDto
                            {
                                Tipo = "alerta",
                                Prioridade = 3,
                                Texto = $"Sua meta{sonho} está com atraso — aporte necessário subiu para {Brl(aporteAgora)}/mês.",
                                Detalhe = "Considere aumentar o aporte mensal ou estender o prazo.",
                                Categoria = "meta",
                            });
                        }
                        else if (aporteAgora < aporteMensal * 0.9m)
                        {
                            insights.Add(new InsightDto
                            {
                                Tipo = "positivo",
                                Prioridade = 4,
                                Texto = $"Você está adiantado na meta{sonho}! O aporte necessário caiu para {Brl(aporteAgora)}/mês.",
                                Detalhe = "Continue no ritmo atual para chegar antes do prazo.",
                                Categoria = "meta",
                            });
                        }
                    }
                }
            }
        }

        // ── 5. Lucro do mês acima da média (insight positivo) ────────────
        // (lucro operacional — transferências e rendimento de investimento ficam fora,
        // senão o rendimento inflaria este "lucro" e o insight passaria a mensagem errada)
        var lucroMesAtual = registrosValidos
            .Where(r => r.Data.Year == anoAtual && r.Data.Month == mesAtual)
            .Sum(r => r.Entradas.Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor)
                    - r.Saidas.Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor));

        var lucrosMeses = Enumerable.Range(1, 3)
            .Select(i => hoje.AddMonths(-i))
            .Select(m => registrosValidos
                .Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month)
                .Sum(r => r.Entradas.Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor)
                        - r.Saidas.Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor)))
            .ToList();

        if (lucrosMeses.Any(l => l != 0))
        {
            var mediaLucro = lucrosMeses.Average();
            var lucroExtrapolado = lucroMesAtual / diasDecorridos * diasNoMes;

            if (mediaLucro > 0 && lucroMesAtual > 0 && lucroExtrapolado > mediaLucro * 1.15m)
            {
                var superavitPct = (lucroExtrapolado - mediaLucro) / mediaLucro * 100m;
                insights.Add(new InsightDto
                {
                    Tipo = "positivo",
                    Prioridade = 5,
                    Texto = $"O lucro deste mês está {superavitPct:F0}% acima da média dos últimos 3 meses.",
                    Detalhe = "Bom momento para reforçar seus investimentos.",
                    Categoria = "lucro",
                });
            }
        }

        return insights.OrderBy(i => i.Prioridade).Take(5).ToList();
    }

    private static decimal CalcularAporteMensal(MetaAnual meta)
    {
        if (meta.ValorSonho <= 0 || meta.PrazoAnos <= 0 || meta.TaxaRetorno <= 0) return 0m;
        var n = meta.PrazoAnos * 12;
        var i = Math.Pow(1 + (double)meta.TaxaRetorno / 100, 1.0 / 12) - 1;
        var fvAtual = (double)meta.TotalInvestido * Math.Pow(1 + i, n);
        var fvNecessario = (double)meta.ValorSonho - fvAtual;
        if (fvNecessario <= 0) return 0m;
        return (decimal)((fvNecessario * i) / (Math.Pow(1 + i, n) - 1));
    }

    private static string NomeSonho(MetaAnual meta) =>
        !string.IsNullOrWhiteSpace(meta.Sonho) ? $" \"{meta.Sonho}\"" : "";
}
