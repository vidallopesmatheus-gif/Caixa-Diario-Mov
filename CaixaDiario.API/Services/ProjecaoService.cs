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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

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

        // Recorrências ativas (não filtradas por conta — ContaRecorrente é nível de cliente)
        var recorrentesReceber = recorrentes.Where(r => r.Tipo == "Receber" && r.Ativo).ToList();
        var recorrentesPagar  = recorrentes.Where(r => r.Tipo == "Pagar"   && r.Ativo).ToList();

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
                    Valor     = rec.Valor,
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
                    Valor     = rec.Valor,
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
}
