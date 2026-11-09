using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

// Money Vision ("toda vez eu tenho que lançar ajuste de saldo"): fonte única do saldo de cada dia.
// Saldo final de D = saldo final de D−1 + efeito de D, partindo do SaldoInicial da conta. Antes cada
// write path somava o próprio delta ao Inicio/SaldoFinal gravados — qualquer um que errasse a conta
// (ou recebesse o Inicio do navegador) quebrava a cadeia pra sempre, e o usuário "consertava" com
// ajuste. Card do Banco, extrato, Caixa, Projeção e a migração de reparo usam estas funções.
public static class SaldoEncadeamento
{
    public sealed record SaldoDoDia(DateOnly Data, decimal Inicio, decimal Final);

    // Entradas − saídas ± baixas de contas a receber/pagar sem lançamento próprio (legado: as baixas
    // novas sempre criam/vinculam um lançamento, que já está em Entradas/Saidas) — mesma regra que o
    // extrato sempre usou pra mostrar essas baixas.
    public static decimal EfeitoDoDia(RegistroDiario r)
    {
        var baixasReceber = r.ContasReceber.Where(EhBaixaSemLancamento).Sum(c => c.ValorRealizado ?? c.Valor);
        var baixasPagar = r.ContasPagar.Where(EhBaixaSemLancamento).Sum(c => c.ValorRealizado ?? c.Valor);
        var efeito = r.Entradas.Sum(e => e.Valor) - r.Saidas.Sum(s => s.Valor) + baixasReceber - baixasPagar;
        return Math.Round(efeito, 2, MidpointRounding.AwayFromZero);
    }

    // Título pago cujo dinheiro NÃO está em nenhum lançamento: parcela de financiamento baixa em até
    // 3 lançamentos próprios (juros/amortização/encargos), nunca em LancamentoVinculadoId — contá-la
    // aqui também duplicaria o pagamento.
    public static bool EhBaixaSemLancamento(ContaProvisionada c) =>
        c.Pago && !c.LancamentoVinculadoId.HasValue && !c.OrigemFinanciamentoId.HasValue
        && !c.LancamentoJurosId.HasValue && !c.LancamentoAmortizacaoId.HasValue && !c.LancamentoEncargosId.HasValue;

    // Só leitura — nunca mexe nos registros (seguro de chamar sobre entidades rastreadas pelo EF).
    public static List<SaldoDoDia> Calcular(decimal saldoAntes, IEnumerable<RegistroDiario> registrosDaConta)
    {
        var resultado = new List<SaldoDoDia>();
        var saldo = saldoAntes;
        foreach (var r in registrosDaConta.Where(r => !r.Excluido).OrderBy(r => r.Data))
        {
            var final = saldo + EfeitoDoDia(r);
            resultado.Add(new SaldoDoDia(r.Data, saldo, final));
            saldo = final;
        }
        return resultado;
    }

    // Leitura da cadeia gravada — que só Encadear (cascata de toda escrita + migração de reparo)
    // escreve, então é sempre igual ao que Calcular daria. Card, extrato, Caixa e Projeção leem por
    // aqui: conta sem nenhum dia lançado vale o saldo inicial (antes a Projeção contava 0).
    public static decimal SaldoAtual(ContaBancaria conta, IEnumerable<RegistroDiario> registros) =>
        SaldoNoFimDe(conta, registros, DateOnly.MaxValue);

    // Saldo do app no FIM do dia informado (com ou sem registro naquele dia).
    public static decimal SaldoNoFimDe(ContaBancaria conta, IEnumerable<RegistroDiario> registros, DateOnly data) =>
        registros
            .Where(r => r.ContaBancariaId == conta.Id && !r.Excluido && r.Data <= data)
            .OrderByDescending(r => r.Data)
            .FirstOrDefault()?.SaldoFinal ?? conta.SaldoInicial;

    // Reencadeia gravando Inicio/SaldoFinal; devolve só os dias que mudaram (pro chamador persistir).
    public static List<RegistroDiario> Encadear(decimal saldoAntes, IEnumerable<RegistroDiario> registrosDaConta)
    {
        var alterados = new List<RegistroDiario>();
        var saldo = saldoAntes;
        foreach (var r in registrosDaConta.Where(r => !r.Excluido).OrderBy(r => r.Data))
        {
            var final = saldo + EfeitoDoDia(r);
            if (r.Inicio != saldo || r.SaldoFinal != final)
            {
                r.Inicio = saldo;
                r.SaldoFinal = final;
                alterados.Add(r);
            }
            saldo = final;
        }
        return alterados;
    }
}
