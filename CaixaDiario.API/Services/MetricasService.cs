using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class MetricasService : IMetricasService
{
    public MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo)
    {
        var entradas = registrosDoPeriodo.SelectMany(r => r.Entradas).ToList();
        var saidas = registrosDoPeriodo.SelectMany(r => r.Saidas).ToList();

        var receita = entradas.Where(e => e.TipoCusto == "Receita").Sum(e => e.Valor);
        var custosFixos = saidas.Where(s => s.TipoCusto == "CustoFixo" && s.Categoria != "Manutenção").Sum(s => s.Valor);
        var custosVariaveis = saidas.Where(s => s.TipoCusto == "CustoVariavel").Sum(s => s.Valor);

        var temCategoria = entradas.Any(e => e.Categoria != null) || saidas.Any(s => s.Categoria != null);

        var dto = new MetricasPeriodoDto();

        if (temCategoria && receita > 0)
        {
            var ebitdaValor = receita - custosFixos - custosVariaveis;
            var ebitdaPerc = ebitdaValor / receita;
            dto.Ebitda = new EbitdaDto
            {
                Valor = ebitdaValor,
                Percentual = ebitdaPerc,
                Semaforo = ebitdaPerc >= 0.15m ? "verde" : ebitdaPerc >= 0.05m ? "amarelo" : "vermelho",
            };

            var salarios = saidas.Where(s => s.Categoria == "Salários/Folha").Sum(s => s.Valor);
            var insumos = saidas.Where(s => s.Categoria == "Insumos/Mercadoria").Sum(s => s.Valor);
            if (salarios > 0 || insumos > 0)
            {
                var primeCostPerc = (salarios + insumos) / receita;
                dto.PrimeCost = new PrimeCostDto
                {
                    Percentual = primeCostPerc,
                    Semaforo = primeCostPerc < 0.6m ? "verde" : primeCostPerc <= 0.75m ? "amarelo" : "vermelho",
                };
            }

            if (custosFixos > 0 || custosVariaveis > 0)
            {
                var mc = (receita - custosVariaveis) / receita;
                var pe = mc > 0 ? custosFixos / mc : 0;
                dto.PontoDeEquilibrio = new PontoDeEquilibrioDto
                {
                    Valor = pe,
                    Receita = receita,
                    Semaforo = receita >= pe * 1.2m ? "verde" : receita >= pe ? "amarelo" : "vermelho",
                };
            }
        }

        var saldoAtual = todosRegistros.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;
        var totalReceber = todosRegistros.SelectMany(r => r.ContasReceber).Where(c => !c.Pago).Sum(c => c.Valor);
        var totalPagar = todosRegistros.SelectMany(r => r.ContasPagar).Where(c => !c.Pago).Sum(c => c.Valor);
        dto.SaldoProjetado = saldoAtual + totalReceber - totalPagar;

        // Valuation
        var ultimos3Meses = Enumerable.Range(0, 3)
            .Select(i => DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-i))
            .Select(m => todosRegistros.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month).ToList())
            .ToList();

        if (ultimos3Meses.Any(m => m.Count > 0))
        {
            var lucrosMensais = ultimos3Meses.Select(m =>
                m.SelectMany(r => r.Entradas).Sum(e => e.Valor) -
                m.SelectMany(r => r.Saidas).Sum(s => s.Valor)).ToList();
            var lucroMedioMensal = lucrosMensais.Average();
            var valuationValor = lucroMedioMensal * 12 * 3;
            string valuationSemaforo = "cinza";
            if (ultimos3Meses[0].Count > 0 && ultimos3Meses[1].Count > 0)
            {
                var lucroAtual = lucrosMensais[0];
                var lucroAnterior = lucrosMensais[1];
                valuationSemaforo = lucroAnterior == 0 ? "cinza"
                    : (lucroAtual - lucroAnterior) / Math.Abs(lucroAnterior) > 0.05m ? "verde"
                    : (lucroAtual - lucroAnterior) / Math.Abs(lucroAnterior) < -0.05m ? "vermelho"
                    : "amarelo";
            }
            dto.Valuation = new ValuationDto { Valor = valuationValor, Semaforo = valuationSemaforo };
        }

        // Runway
        var burnMedioMensal = ultimos3Meses
            .Where(m => m.Count > 0)
            .Select(m => m.SelectMany(r => r.Saidas).Sum(s => s.Valor))
            .DefaultIfEmpty(0)
            .Average();
        var runwayMeses = burnMedioMensal > 0 ? Math.Round(saldoAtual / burnMedioMensal, 1) : 0;

        dto.Runway = new RunwayDto
        {
            Meses = runwayMeses,
            Semaforo = burnMedioMensal == 0 ? "cinza"
                : runwayMeses > 6 ? "verde"
                : runwayMeses >= 3 ? "amarelo"
                : "vermelho",
        };

        // Liquidez
        var hoje30 = DateOnly.FromDateTime(DateTime.UtcNow);
        var em30dias = hoje30.AddDays(30);
        var contasPagarProximas = todosRegistros
            .SelectMany(r => r.ContasPagar)
            .Where(c => !c.Pago && c.DataVencimento.HasValue &&
                        c.DataVencimento.Value >= hoje30 && c.DataVencimento.Value <= em30dias)
            .Sum(c => c.Valor);

        if (contasPagarProximas == 0)
        {
            dto.Liquidez = new LiquidezDto { AltaLiquidez = true, Semaforo = "verde" };
        }
        else
        {
            var indice = Math.Round(saldoAtual / contasPagarProximas, 2);
            dto.Liquidez = new LiquidezDto
            {
                Indice = indice,
                AltaLiquidez = false,
                Semaforo = indice >= 1.5m ? "verde" : indice >= 1.0m ? "amarelo" : "vermelho",
            };
        }

        return dto;
    }

    public List<EvolucaoMensalDto> CalcularEvolucao(List<RegistroDiario> registros, int meses)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var resultado = new List<EvolucaoMensalDto>();

        for (int i = meses - 1; i >= 0; i--)
        {
            var ref_ = hoje.AddMonths(-i);
            var prefixo = $"{ref_.Year}-{ref_.Month:D2}";
            var doMes = registros.Where(r => r.Data.ToString("yyyy-MM").StartsWith(prefixo)).ToList();

            var receita = doMes.SelectMany(r => r.Entradas).Sum(e => e.Valor);
            var custos = doMes.SelectMany(r => r.Saidas).Sum(s => s.Valor);
            var saldo = doMes.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;

            resultado.Add(new EvolucaoMensalDto
            {
                Mes = prefixo,
                Receita = receita,
                Custos = custos,
                Lucro = receita - custos,
                Saldo = saldo,
            });
        }

        return resultado;
    }

    public FluxoProjetadoDto CalcularFluxoProjetado(List<RegistroDiario> registros, List<ContaRecorrente> recorrentes, int dias)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var saldoAtual = registros.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;

        var fluxoDias = new List<FluxoDiaDto>();
        var saldoCorrendo = saldoAtual;

        for (int d = 1; d <= dias; d++)
        {
            var dia = hoje.AddDays(d);

            var entradas = registros.SelectMany(r => r.ContasReceber)
                .Where(c => !c.Pago && c.DataVencimento == dia).Sum(c => c.Valor);

            var saidas = registros.SelectMany(r => r.ContasPagar)
                .Where(c => !c.Pago && c.DataVencimento == dia).Sum(c => c.Valor);

            var entradasRec = recorrentes.Where(r => r.Tipo == "Receber" && r.Ativo &&
                r.DataInicio <= dia && (r.DataFim == null || r.DataFim >= dia) &&
                r.DataInicio.Day == dia.Day).Sum(r => r.Valor);

            var saidasRec = recorrentes.Where(r => r.Tipo == "Pagar" && r.Ativo &&
                r.DataInicio <= dia && (r.DataFim == null || r.DataFim >= dia) &&
                r.DataInicio.Day == dia.Day).Sum(r => r.Valor);

            saldoCorrendo += entradas + entradasRec - saidas - saidasRec;

            fluxoDias.Add(new FluxoDiaDto { Data = dia, SaldoProjetado = saldoCorrendo });
        }

        return new FluxoProjetadoDto { SaldoAtual = saldoAtual, Dias = fluxoDias };
    }
}
