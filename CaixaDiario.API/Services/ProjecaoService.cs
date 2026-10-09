using CaixaDiario.API.DTOs.Projecao;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class ProjecaoService : IProjecaoService
{
    public ProjecaoDto Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        int dias,
        Guid? contaBancariaId)
    {
        var hoje = DataLocalHelper.Hoje();

        // Saldo atual: último SaldoFinal considerando filtro de conta
        List<RegistroDiario> registrosFiltrados;
        if (contaBancariaId.HasValue && contaBancariaId.Value != Guid.Empty)
            registrosFiltrados = registros.Where(r => r.ContaBancariaId == contaBancariaId).ToList();
        else
            registrosFiltrados = registros;

        // Para "todas as contas", soma o último saldo de cada conta distinta
        decimal saldoAtual;
        if (contaBancariaId.HasValue && contaBancariaId.Value != Guid.Empty)
        {
            saldoAtual = registrosFiltrados
                .OrderByDescending(r => r.Data)
                .FirstOrDefault()?.SaldoFinal ?? 0m;
        }
        else
        {
            // Soma o último SaldoFinal de cada conta bancária
            saldoAtual = registros
                .Where(r => r.ContaBancariaId.HasValue)
                .GroupBy(r => r.ContaBancariaId)
                .Sum(g => g.OrderByDescending(r => r.Data).First().SaldoFinal);
        }

        // Pendentes provisionados (apenas não pagos e com vencimento futuro)
        var receberPendentes = registrosFiltrados
            .SelectMany(r => r.ContasReceber)
            .Where(c => !c.Pago && c.DataVencimento.HasValue && c.DataVencimento.Value > hoje)
            .ToList();

        var pagarPendentes = registrosFiltrados
            .SelectMany(r => r.ContasPagar)
            .Where(c => !c.Pago && c.DataVencimento.HasValue && c.DataVencimento.Value > hoje)
            .ToList();

        // Item 1.6: contas vencidas e ainda não pagas ("atrasadas") estavam fora da projeção —
        // só entrava quem tinha vencimento FUTURO, então um cliente com contas em atraso via o
        // saldo projetado sempre subir, como se aquela dívida já não existisse. Entram todas de
        // uma vez no primeiro dia da projeção (presume-se que serão quitadas em breve).
        var (receberAtrasados, pagarAtrasados) = ObterAtrasados(registrosFiltrados, hoje);

        // Recorrências ativas (não filtradas por conta — ContaRecorrente é nível de cliente)
        var recorrentesReceber = recorrentes.Where(r => r.Tipo == "Receber" && r.Ativo).ToList();
        var recorrentesPagar  = recorrentes.Where(r => r.Tipo == "Pagar"   && r.Ativo).ToList();

        // Item 1.6: despesas fixas reais que o cliente nunca cadastrou como ContaRecorrente (ex.:
        // ele sempre lança manualmente) também faziam a projeção parecer sempre subir. Mesma
        // lógica de OrcamentoDinamicoService (1.4): usa a DIFERENÇA entre a média histórica e o
        // que as recorrências já cobrem, nunca a soma (senão duplicaria a mesma despesa).
        var despesaFixaNaoCadastrada = CalcularDespesaFixaNaoCadastrada(registros, recorrentesPagar, hoje);

        // Dedup: recorrências que já estão materializadas como ContaProvisionada não devem ser
        // contadas duas vezes. Identifica pares (RecorrenciaId, DataVencimento) já provisionados.
        var jaMaterializados = registrosFiltrados
            .SelectMany(r => r.ContasReceber.Concat(r.ContasPagar))
            .Where(c => c.RecorrenciaId.HasValue && c.DataVencimento.HasValue && c.DataVencimento.Value > hoje)
            .Select(c => (c.RecorrenciaId!.Value, c.DataVencimento!.Value))
            .ToHashSet();

        // Projeção dia a dia
        var saldoCorrendo = saldoAtual;
        var listaDias = new List<ProjecaoDiaDto>();

        for (int d = 1; d <= dias; d++)
        {
            var dia = hoje.AddDays(d);
            var saldoIniciodia = saldoCorrendo;

            var entradas = new List<ProjecaoItemDto>();
            var saidas   = new List<ProjecaoItemDto>();

            // Atrasados: concentrados no 1º dia da projeção (ver comentário acima).
            if (d == 1)
            {
                foreach (var c in receberAtrasados)
                    entradas.Add(new ProjecaoItemDto { Descricao = c.Descricao, Valor = c.Valor, Categoria = c.Categoria, Origem = "Atrasado" });
                foreach (var c in pagarAtrasados)
                    saidas.Add(new ProjecaoItemDto { Descricao = c.Descricao, Valor = c.Valor, Categoria = c.Categoria, Origem = "Atrasado" });
            }

            // Despesa fixa média não cadastrada: um lançamento estimado por bloco de 30 dias.
            if (despesaFixaNaoCadastrada > 0 && d % 30 == 0)
                saidas.Add(new ProjecaoItemDto
                {
                    Descricao = "Despesas fixas médias (não cadastradas)",
                    Valor     = despesaFixaNaoCadastrada,
                    Origem    = "Estimativa",
                });

            // Provisionados manuais para este dia
            foreach (var c in receberPendentes.Where(c => c.DataVencimento == dia))
                entradas.Add(new ProjecaoItemDto
                {
                    Descricao = c.Descricao,
                    Valor     = c.Valor,
                    Categoria = c.Categoria,
                    Origem    = "Provisionado",
                });

            foreach (var c in pagarPendentes.Where(c => c.DataVencimento == dia))
                saidas.Add(new ProjecaoItemDto
                {
                    Descricao = c.Descricao,
                    Valor     = c.Valor,
                    Categoria = c.Categoria,
                    Origem    = "Provisionado",
                });

            // Recorrências: apenas se ainda não materializadas para este dia
            foreach (var rec in recorrentesReceber)
            {
                if (!RecorrenciaService.OcorreEm(rec, dia)) continue;
                if (jaMaterializados.Contains((rec.Id, dia))) continue;
                entradas.Add(new ProjecaoItemDto
                {
                    Descricao = rec.Descricao,
                    // Fase 1.6: ValorVariavel usa o previsto recalculado (média), nunca o Valor
                    // cadastrado cru — mesma fonte de verdade de RecorrenciaService/relatório 1.5.
                    Valor     = RecorrenciaService.CalcularValorPrevisto(rec, registros),
                    Categoria = rec.Categoria,
                    Origem    = "Recorrente",
                });
            }

            foreach (var rec in recorrentesPagar)
            {
                if (!RecorrenciaService.OcorreEm(rec, dia)) continue;
                if (jaMaterializados.Contains((rec.Id, dia))) continue;
                saidas.Add(new ProjecaoItemDto
                {
                    Descricao = rec.Descricao,
                    Valor     = RecorrenciaService.CalcularValorPrevisto(rec, registros),
                    Categoria = rec.Categoria,
                    Origem    = "Recorrente",
                });
            }

            var totalEntradas = entradas.Sum(e => e.Valor);
            var totalSaidas   = saidas.Sum(s => s.Valor);
            saldoCorrendo += totalEntradas - totalSaidas;

            // Só adiciona o dia se tiver movimentação, ou a cada 7 dias (para gráfico contínuo)
            if (entradas.Count > 0 || saidas.Count > 0 || d == 1 || d % 7 == 0 || d == dias)
            {
                listaDias.Add(new ProjecaoDiaDto
                {
                    Data          = dia,
                    SaldoInicio   = saldoIniciodia,
                    Entradas      = entradas,
                    Saidas        = saidas,
                    TotalEntradas = totalEntradas,
                    TotalSaidas   = totalSaidas,
                    SaldoFim      = saldoCorrendo,
                    SaldoNegativo = saldoCorrendo < 0,
                });
            }
        }

        // Garante que todos os dias do gráfico estejam presentes (inclusive sem movimento)
        // Recalcula adicionando pontos intermediários ausentes para o gráfico ficar contínuo
        var diasCompletos = new List<ProjecaoDiaDto>();
        saldoCorrendo = saldoAtual;
        for (int d = 1; d <= dias; d++)
        {
            var dia = hoje.AddDays(d);
            var existente = listaDias.FirstOrDefault(x => x.Data == dia);
            if (existente != null)
            {
                saldoCorrendo = existente.SaldoFim;
                diasCompletos.Add(existente);
            }
            else
            {
                diasCompletos.Add(new ProjecaoDiaDto
                {
                    Data          = dia,
                    SaldoInicio   = saldoCorrendo,
                    SaldoFim      = saldoCorrendo,
                    SaldoNegativo = saldoCorrendo < 0,
                });
            }
        }

        return new ProjecaoDto
        {
            SaldoAtual = saldoAtual,
            TotalDias  = dias,
            Dias       = diasCompletos,
        };
    }

    // ── Trajetória: histórico realizado + projeção, na mesma linha do tempo ─────────────────────
    // Só leitura — reaproveita o mesmo critério de saldo consolidado (soma do último SaldoFinal de
    // cada conta) já usado acima pro "saldo atual", e a mesma simulação dia-a-dia de pendentes +
    // recorrências já usada em Calcular — só que amostrada mês a mês em vez de dia a dia.
    public TrajetoriaDto CalcularTrajetoria(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        int mesesPassado,
        int mesesFuturo,
        Guid? contaBancariaId)
    {
        var hoje = DataLocalHelper.Hoje();
        var filtrarPorConta = contaBancariaId.HasValue && contaBancariaId.Value != Guid.Empty;

        var registrosFiltrados = filtrarPorConta
            ? registros.Where(r => r.ContaBancariaId == contaBancariaId).ToList()
            : registros;

        decimal SaldoConsolidadoAte(DateOnly ateData)
        {
            var relevantes = registrosFiltrados.Where(r => r.Data <= ateData).ToList();
            if (filtrarPorConta)
                return relevantes.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0m;
            return relevantes
                .Where(r => r.ContaBancariaId.HasValue)
                .GroupBy(r => r.ContaBancariaId)
                .Sum(g => g.OrderByDescending(r => r.Data).First().SaldoFinal);
        }

        var saldoAtual = SaldoConsolidadoAte(hoje);

        // ── Histórico: quantos meses de dado real existem de verdade (pode ser menos que o pedido) ──
        var primeiraData = registrosFiltrados.Select(r => r.Data).DefaultIfEmpty().Min();
        int mesesDisponiveis;
        if (primeiraData == default)
        {
            mesesDisponiveis = 0;
        }
        else
        {
            var mesesDesdeInicio = (hoje.Year - primeiraData.Year) * 12 + hoje.Month - primeiraData.Month + 1;
            mesesDisponiveis = Math.Clamp(mesesDesdeInicio, 0, mesesPassado);
        }

        var historico = new List<TrajetoriaPontoDto>();
        for (int i = mesesDisponiveis - 1; i >= 0; i--)
        {
            var mesRef = hoje.AddMonths(-i);
            // Mês corrente (i == 0): corta em hoje, não no fim do mês (ainda não acabou).
            var cutoff = i == 0 ? hoje : UltimoDiaDoMes(mesRef);
            historico.Add(new TrajetoriaPontoDto { Mes = $"{mesRef.Year}-{mesRef.Month:D2}", Saldo = SaldoConsolidadoAte(cutoff) });
        }

        // ── Projeção: mesma simulação de pendentes/recorrências de Calcular, amostrada mês a mês ──
        var receberPendentes = registrosFiltrados.SelectMany(r => r.ContasReceber)
            .Where(c => !c.Pago && c.DataVencimento.HasValue && c.DataVencimento.Value > hoje).ToList();
        var pagarPendentes = registrosFiltrados.SelectMany(r => r.ContasPagar)
            .Where(c => !c.Pago && c.DataVencimento.HasValue && c.DataVencimento.Value > hoje).ToList();
        var recorrentesReceber = recorrentes.Where(r => r.Tipo == "Receber" && r.Ativo).ToList();
        var recorrentesPagar = recorrentes.Where(r => r.Tipo == "Pagar" && r.Ativo).ToList();
        var jaMaterializados = registrosFiltrados
            .SelectMany(r => r.ContasReceber.Concat(r.ContasPagar))
            .Where(c => c.RecorrenciaId.HasValue && c.DataVencimento.HasValue && c.DataVencimento.Value > hoje)
            .Select(c => (c.RecorrenciaId!.Value, c.DataVencimento!.Value))
            .ToHashSet();

        // Item 1.6: mesma correção de Calcular — atrasados e despesa fixa não cadastrada, senão a
        // trajetória projetada também parece sempre subir.
        var (receberAtrasados, pagarAtrasados) = ObterAtrasados(registrosFiltrados, hoje);
        var despesaFixaNaoCadastrada = CalcularDespesaFixaNaoCadastrada(registros, recorrentesPagar, hoje);

        var projetado = new List<TrajetoriaPontoDto>();
        var saldoCorrendo = saldoAtual;
        var diasTotais = hoje.AddMonths(mesesFuturo).DayNumber - hoje.DayNumber;
        var proximoMarco = 1;
        for (int d = 1; d <= diasTotais && proximoMarco <= mesesFuturo; d++)
        {
            var dia = hoje.AddDays(d);
            var totalEntradas = receberPendentes.Where(c => c.DataVencimento == dia).Sum(c => c.Valor)
                + recorrentesReceber.Where(r => RecorrenciaService.OcorreEm(r, dia) && !jaMaterializados.Contains((r.Id, dia)))
                    .Sum(r => RecorrenciaService.CalcularValorPrevisto(r, registros));
            var totalSaidas = pagarPendentes.Where(c => c.DataVencimento == dia).Sum(c => c.Valor)
                + recorrentesPagar.Where(r => RecorrenciaService.OcorreEm(r, dia) && !jaMaterializados.Contains((r.Id, dia)))
                    .Sum(r => RecorrenciaService.CalcularValorPrevisto(r, registros));

            if (d == 1)
            {
                totalEntradas += receberAtrasados.Sum(c => c.Valor);
                totalSaidas   += pagarAtrasados.Sum(c => c.Valor);
            }
            if (d % 30 == 0)
                totalSaidas += despesaFixaNaoCadastrada;

            saldoCorrendo += totalEntradas - totalSaidas;

            var marco = hoje.AddMonths(proximoMarco);
            if (dia == marco)
            {
                projetado.Add(new TrajetoriaPontoDto { Mes = $"{marco.Year}-{marco.Month:D2}", Saldo = saldoCorrendo });
                proximoMarco++;
            }
        }

        return new TrajetoriaDto
        {
            Historico = historico,
            Projetado = projetado,
            MesesHistoricoDisponiveis = mesesDisponiveis,
            SaldoAtual = saldoAtual,
            VariacaoRealizada = historico.Count > 0 ? saldoAtual - historico[0].Saldo : 0m,
            VariacaoProjetada = projetado.Count > 0 ? projetado[^1].Saldo - saldoAtual : 0m,
        };
    }

    private static DateOnly UltimoDiaDoMes(DateOnly qualquerDiaDoMes) =>
        new(qualquerDiaDoMes.Year, qualquerDiaDoMes.Month, DateTime.DaysInMonth(qualquerDiaDoMes.Year, qualquerDiaDoMes.Month));

    // Item 1.6: contas (manuais) com vencimento <= hoje e ainda não pagas.
    private static (List<ContaProvisionada> Receber, List<ContaProvisionada> Pagar) ObterAtrasados(
        List<RegistroDiario> registrosFiltrados, DateOnly hoje)
    {
        var receberAtrasados = registrosFiltrados
            .SelectMany(r => r.ContasReceber)
            .Where(c => !c.Pago && c.DataVencimento.HasValue && c.DataVencimento.Value <= hoje)
            .ToList();
        var pagarAtrasados = registrosFiltrados
            .SelectMany(r => r.ContasPagar)
            .Where(c => !c.Pago && c.DataVencimento.HasValue && c.DataVencimento.Value <= hoje)
            .ToList();
        return (receberAtrasados, pagarAtrasados);
    }

    // Item 1.6: mesma fórmula de OrcamentoDinamicoService (1.4) — média de CustoFixo dos últimos
    // 3 meses menos o que as recorrências "Pagar" já cobrem num período de 30 dias. Nunca negativo
    // (Math.Max): se as recorrências já cobrem tudo (ou mais), não há nada "não cadastrado".
    private static decimal CalcularDespesaFixaNaoCadastrada(
        List<RegistroDiario> registros, List<ContaRecorrente> recorrentesPagar, DateOnly hoje)
    {
        var despesaFixaMeses = Enumerable.Range(1, 3)
            .Select(i => hoje.AddMonths(-i))
            .Select(m => registros
                .Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month)
                .SelectMany(r => r.Saidas).Where(s => s.TipoCusto == "CustoFixo").Sum(s => s.Valor))
            .Where(v => v > 0)
            .ToList();
        var despesaFixaMedia = despesaFixaMeses.Count > 0 ? despesaFixaMeses.Average() : 0m;

        decimal recorrentesPagarEm30Dias = 0m;
        for (int d = 1; d <= 30; d++)
        {
            var dia = hoje.AddDays(d);
            recorrentesPagarEm30Dias += recorrentesPagar
                .Where(r => RecorrenciaService.OcorreEm(r, dia))
                .Sum(r => RecorrenciaService.CalcularValorPrevisto(r, registros));
        }

        return Math.Max(0m, despesaFixaMedia - recorrentesPagarEm30Dias);
    }
}
