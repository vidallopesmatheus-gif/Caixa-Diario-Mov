using CaixaDiario.API.DTOs.OrcamentoDinamico;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class OrcamentoDinamicoService : IOrcamentoDinamicoService
{
    public OrcamentoDinamicoDto Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        List<MetaAnual> metas)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var anoAtual = hoje.Year;
        var mesAtual = hoje.Month;
        var diasNoMes = DateTime.DaysInMonth(anoAtual, mesAtual);

        var registrosValidos = registros.Where(r => !r.Excluido).ToList();

        // Receita esperada = média dos últimos 3 meses
        // (transferências entre contas e rendimento não contam como receita)
        var receitasMeses = Enumerable.Range(1, 3)
            .Select(i => hoje.AddMonths(-i))
            .Select(m => registrosValidos
                .Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month)
                .SelectMany(r => r.Entradas).Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor))
            .Where(v => v > 0)
            .ToList();
        var receitaEsperada = receitasMeses.Count > 0 ? receitasMeses.Average() : 0m;

        // Compromissos fixos = contas a pagar não pagas com vencimento neste mês + recorrências "Pagar"
        var contasPagarMes = registrosValidos
            .SelectMany(r => r.ContasPagar)
            .Where(c => !c.Pago
                && c.DataVencimento.HasValue
                && c.DataVencimento.Value.Year == anoAtual
                && c.DataVencimento.Value.Month == mesAtual)
            .Sum(c => c.Valor);

        decimal recorrenciasPagar = 0m;
        for (int d = 1; d <= diasNoMes; d++)
        {
            var dia = new DateOnly(anoAtual, mesAtual, d);
            recorrenciasPagar += recorrentes
                .Where(r => r.Tipo == "Pagar" && r.Ativo && RecorrenciaService.OcorreEm(r, dia))
                .Sum(r => r.Valor);
        }

        var compromissosFixos = contasPagarMes + recorrenciasPagar;

        // Aporte necessário = soma de todas as metas ativas
        var aporteNecessario = metas
            .Where(m => m.ModoMeta == "metodo" && m.ValorSonho > 0 && m.PrazoAnos > 0 && m.TaxaRetorno > 0)
            .Sum(m => CalcularAporteMensal(m));

        var saldoLivre = receitaEsperada - compromissosFixos - aporteNecessario;

        // Gasto variável atual (saídas do mês corrente, exceto transferências/rendimento)
        var gastoVariavelAtual = registrosValidos
            .Where(r => r.Data.Year == anoAtual && r.Data.Month == mesAtual)
            .SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor);

        var ultrapassado = saldoLivre > 0 && gastoVariavelAtual > saldoLivre;
        var percentualUtilizado = saldoLivre > 0
            ? Math.Round(gastoVariavelAtual / saldoLivre * 100m, 1)
            : 0m;

        return new OrcamentoDinamicoDto
        {
            ReceitaEsperada     = Math.Round(receitaEsperada, 2),
            CompromissosFixos   = Math.Round(compromissosFixos, 2),
            AporteNecessario    = Math.Round(aporteNecessario, 2),
            SaldoLivre          = Math.Round(saldoLivre, 2),
            GastoVariavelAtual  = Math.Round(gastoVariavelAtual, 2),
            Ultrapassado        = ultrapassado,
            PercentualUtilizado = percentualUtilizado,
        };
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
}
