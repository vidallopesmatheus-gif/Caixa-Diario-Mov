using CaixaDiario.API.DTOs.Projecao;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class ProjecaoService : IProjecaoService
{
    // Bloco 2 da rodada "Visão de Futuro", item 2: cenário "Conservador" aplica margem de
    // segurança só nos valores que são ESTIMATIVA (recorrência projetada, despesa fixa não
    // cadastrada) — nunca em provisionados/atrasados/fatura de cartão, que são compromissos
    // reais já assumidos, não previsão.
    private const decimal FatorReceitaConservador = 0.7m;
    private const decimal FatorDespesaConservador = 1.15m;

    public ProjecaoDto Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        int dias,
        Guid? contaBancariaId,
        ProjecaoOpcoes opcoes)
    {
        var hoje = DataLocalHelper.Hoje();
        var consolidado = !contaBancariaId.HasValue || contaBancariaId.Value == Guid.Empty;

        var registrosFiltrados = consolidado
            ? registros
            : registros.Where(r => r.ContaBancariaId == contaBancariaId).ToList();

        var contasExcluidasDoSaldo = ObterContasExcluidasDoSaldo(consolidado, opcoes);
        var saldoAtual = CalcularSaldoConsolidado(registros, registrosFiltrados, consolidado, contasExcluidasDoSaldo);

        var fatorReceita = opcoes.Cenario == "Conservador" ? FatorReceitaConservador : 1m;
        var fatorDespesa = opcoes.Cenario == "Conservador" ? FatorDespesaConservador : 1m;

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
        var despesaFixaNaoCadastrada = CalcularDespesaFixaNaoCadastrada(registros, recorrentesPagar, hoje) * fatorDespesa;

        // Dedup: recorrências que já estão materializadas como ContaProvisionada não devem ser
        // contadas duas vezes. Identifica pares (RecorrenciaId, DataVencimento) já provisionados.
        var jaMaterializados = registrosFiltrados
            .SelectMany(r => r.ContasReceber.Concat(r.ContasPagar))
            .Where(c => c.RecorrenciaId.HasValue && c.DataVencimento.HasValue && c.DataVencimento.Value > hoje)
            .Select(c => (c.RecorrenciaId!.Value, c.DataVencimento!.Value))
            .ToHashSet();

        // Item 4: faturas de cartão em aberto/fechadas (ainda não pagas) entram como saída
        // projetada na data de vencimento — só no modo "todas as contas" (ver comentário em
        // ObterContasExcluidasDoSaldo sobre por que o saldo do cartão fica de fora da soma inicial).
        var faturasPorDia = consolidado
            ? ObterFaturasPorDia(registros, opcoes.Contas, opcoes.PagamentosFatura, hoje, hoje.AddDays(dias))
            : new Dictionary<DateOnly, List<ProjecaoItemDto>>();

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
                    Valor     = RecorrenciaService.CalcularValorPrevisto(rec, registros) * fatorReceita,
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
                    Valor     = RecorrenciaService.CalcularValorPrevisto(rec, registros) * fatorDespesa,
                    Categoria = rec.Categoria,
                    Origem    = "Recorrente",
                });
            }

            if (faturasPorDia.TryGetValue(dia, out var faturasDoDia))
                saidas.AddRange(faturasDoDia);

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
        Guid? contaBancariaId,
        ProjecaoOpcoes opcoes)
    {
        var hoje = DataLocalHelper.Hoje();
        var consolidado = !contaBancariaId.HasValue || contaBancariaId.Value == Guid.Empty;

        var registrosFiltrados = consolidado
            ? registros
            : registros.Where(r => r.ContaBancariaId == contaBancariaId).ToList();

        var contasExcluidasDoSaldo = ObterContasExcluidasDoSaldo(consolidado, opcoes);

        // Item 5: a exclusão de reserva/cartão vale pro histórico TAMBÉM, não só pra projeção —
        // senão o gráfico "pula" exatamente em "hoje", quando a projeção passa a ignorar um saldo
        // que o histórico vinha contando até ali.
        decimal SaldoConsolidadoAte(DateOnly ateData)
        {
            var relevantes = registrosFiltrados.Where(r => r.Data <= ateData).ToList();
            if (!consolidado)
                return relevantes.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0m;
            return relevantes
                .Where(r => r.ContaBancariaId.HasValue && !contasExcluidasDoSaldo.Contains(r.ContaBancariaId.Value))
                .GroupBy(r => r.ContaBancariaId)
                .Sum(g => g.OrderByDescending(r => r.Data).First().SaldoFinal);
        }

        var saldoAtual = SaldoConsolidadoAte(hoje);
        var fatorReceita = opcoes.Cenario == "Conservador" ? FatorReceitaConservador : 1m;
        var fatorDespesa = opcoes.Cenario == "Conservador" ? FatorDespesaConservador : 1m;

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
        var despesaFixaNaoCadastrada = CalcularDespesaFixaNaoCadastrada(registros, recorrentesPagar, hoje) * fatorDespesa;

        var diasTotais = hoje.AddMonths(mesesFuturo).DayNumber - hoje.DayNumber;
        var faturasPorDia = consolidado
            ? ObterFaturasPorDia(registros, opcoes.Contas, opcoes.PagamentosFatura, hoje, hoje.AddDays(diasTotais))
            : new Dictionary<DateOnly, List<ProjecaoItemDto>>();

        var projetado = new List<TrajetoriaPontoDto>();
        var saldoCorrendo = saldoAtual;
        var proximoMarco = 1;
        for (int d = 1; d <= diasTotais && proximoMarco <= mesesFuturo; d++)
        {
            var dia = hoje.AddDays(d);
            var totalEntradas = receberPendentes.Where(c => c.DataVencimento == dia).Sum(c => c.Valor)
                + recorrentesReceber.Where(r => RecorrenciaService.OcorreEm(r, dia) && !jaMaterializados.Contains((r.Id, dia)))
                    .Sum(r => RecorrenciaService.CalcularValorPrevisto(r, registros) * fatorReceita);
            var totalSaidas = pagarPendentes.Where(c => c.DataVencimento == dia).Sum(c => c.Valor)
                + recorrentesPagar.Where(r => RecorrenciaService.OcorreEm(r, dia) && !jaMaterializados.Contains((r.Id, dia)))
                    .Sum(r => RecorrenciaService.CalcularValorPrevisto(r, registros) * fatorDespesa);

            if (d == 1)
            {
                totalEntradas += receberAtrasados.Sum(c => c.Valor);
                totalSaidas   += pagarAtrasados.Sum(c => c.Valor);
            }
            if (d % 30 == 0)
                totalSaidas += despesaFixaNaoCadastrada;
            if (faturasPorDia.TryGetValue(dia, out var faturasDoDia))
                totalSaidas += faturasDoDia.Sum(f => f.Valor);

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

    // Item 3: despesas fixas recorrentes por categoria que o cliente nunca cadastrou como
    // ContaRecorrente — mesma ideia de CalcularDespesaFixaNaoCadastrada, quebrada por categoria em
    // vez de um único total, pra virar uma lista com botão "Transformar em conta a pagar" na tela.
    // Pode não bater exatamente com a soma do valor agregado (ali a média/dedução é sobre o total;
    // aqui é categoria por categoria) — são dois usos diferentes: o agregado alimenta a simulação
    // dia a dia, esta lista é pra decisão do usuário.
    public List<DespesaEstimadaCategoriaDto> CalcularDespesasEstimadasPorCategoria(
        List<RegistroDiario> registros, List<ContaRecorrente> recorrentes, DateOnly hoje)
    {
        var recorrentesPagar = recorrentes.Where(r => r.Tipo == "Pagar" && r.Ativo).ToList();

        var porCategoriaPorMes = Enumerable.Range(1, 3)
            .Select(i => hoje.AddMonths(-i))
            .Select(m => registros
                .Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month)
                .SelectMany(r => r.Saidas)
                .Where(s => s.TipoCusto == "CustoFixo")
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Categoria) ? "Não Classificado" : s.Categoria)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Valor)))
            .ToList();

        var categorias = porCategoriaPorMes.SelectMany(m => m.Keys).Distinct().ToList();

        var recorrentePorCategoriaEm30Dias = new Dictionary<string, decimal>();
        for (var d = 1; d <= 30; d++)
        {
            var dia = hoje.AddDays(d);
            foreach (var rec in recorrentesPagar.Where(r => RecorrenciaService.OcorreEm(r, dia)))
            {
                var cat = string.IsNullOrWhiteSpace(rec.Categoria) ? "Não Classificado" : rec.Categoria;
                recorrentePorCategoriaEm30Dias[cat] = recorrentePorCategoriaEm30Dias.GetValueOrDefault(cat)
                    + RecorrenciaService.CalcularValorPrevisto(rec, registros);
            }
        }

        var resultado = new List<DespesaEstimadaCategoriaDto>();
        foreach (var cat in categorias)
        {
            var valoresNosMeses = porCategoriaPorMes.Select(m => m.GetValueOrDefault(cat, 0m)).Where(v => v > 0).ToList();
            if (valoresNosMeses.Count == 0) continue;
            var media = valoresNosMeses.Average();
            var jaCoberta = recorrentePorCategoriaEm30Dias.GetValueOrDefault(cat, 0m);
            var estimativa = Math.Round(Math.Max(0m, media - jaCoberta), 2);
            if (estimativa <= 0) continue;
            resultado.Add(new DespesaEstimadaCategoriaDto { Categoria = cat, ValorEstimado = estimativa });
        }

        return resultado.OrderByDescending(r => r.ValorEstimado).ToList();
    }

    // Item 4/5: contas que ficam de fora da soma do saldo "em caixa" — Investimento (reserva, a
    // menos que o usuário peça pra incluir) e CartaoCredito (dívida que entra como saída projetada
    // na data de vencimento da fatura, não como saldo negativo já "congelado" hoje). Só se aplica
    // no modo "todas as contas": selecionar uma conta específica é um recorte explícito.
    private static HashSet<Guid> ObterContasExcluidasDoSaldo(bool consolidado, ProjecaoOpcoes opcoes)
    {
        if (!consolidado) return new HashSet<Guid>();
        return opcoes.Contas
            .Where(c => c.Tipo == "CartaoCredito" || (c.Tipo == "Investimento" && !opcoes.IncluirReserva))
            .Select(c => c.Id)
            .ToHashSet();
    }

    private static decimal CalcularSaldoConsolidado(
        List<RegistroDiario> registros, List<RegistroDiario> registrosFiltrados, bool consolidado, HashSet<Guid> contasExcluidas)
    {
        if (!consolidado)
            return registrosFiltrados.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0m;

        return registros
            .Where(r => r.ContaBancariaId.HasValue && !contasExcluidas.Contains(r.ContaBancariaId.Value))
            .GroupBy(r => r.ContaBancariaId)
            .Sum(g => g.OrderByDescending(r => r.Data).First().SaldoFinal);
    }

    // Item 4: faturas de cartão em aberto ou fechadas (ainda não totalmente pagas) de cada conta
    // CartaoCredito do cliente, indexadas pela data de vencimento — mesmo cálculo de competência/
    // ciclo usado na tela de Faturas (FaturaCartaoService.CalcularFaturas), só que sem os
    // controles de acesso por requisição HTTP (a Projeção já recebeu tudo filtrado por cliente).
    private static Dictionary<DateOnly, List<ProjecaoItemDto>> ObterFaturasPorDia(
        List<RegistroDiario> registros, List<ContaBancaria> contas, List<PagamentoFatura> pagamentosFatura,
        DateOnly hoje, DateOnly ate)
    {
        var resultado = new Dictionary<DateOnly, List<ProjecaoItemDto>>();
        foreach (var cartao in contas.Where(c => c.Tipo == "CartaoCredito"))
        {
            var registrosDoCartao = registros.Where(r => r.ContaBancariaId == cartao.Id && !r.Excluido).ToList();
            var pagamentosDoCartao = pagamentosFatura.Where(p => p.ContaCartaoId == cartao.Id).ToList();
            var faturas = FaturaCartaoService.CalcularFaturas(cartao, registrosDoCartao, pagamentosDoCartao, hoje);

            foreach (var fatura in faturas.Where(f => f.Status != "Paga" && f.DataVencimento > hoje && f.DataVencimento <= ate))
            {
                if (!resultado.TryGetValue(fatura.DataVencimento, out var lista))
                {
                    lista = new List<ProjecaoItemDto>();
                    resultado[fatura.DataVencimento] = lista;
                }
                lista.Add(new ProjecaoItemDto
                {
                    Descricao = $"Fatura {cartao.Nome} ({fatura.Competencia})",
                    Valor = fatura.SaldoDevedor,
                    Origem = "Fatura",
                });
            }
        }
        return resultado;
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
