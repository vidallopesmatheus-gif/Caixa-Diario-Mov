import type { ContaBancaria, Registro } from '../types'

/**
 * Capital real de portfólio — duas fontes, sem contar o mesmo dinheiro duas vezes:
 *  1) Saldo das contas tipo Investimento — já reflete qualquer aporte/resgate feito POR
 *     TRANSFERÊNCIA entre uma conta corrente e essa conta (a transferência move o saldo direto).
 *  2) Aporte/resgate lançado como categoria tipo Investimento SEM passar por uma conta
 *     Investimento dedicada (ex.: cliente sem conta de investimento cadastrada, só categoriza
 *     "Aplicação CDB" como saída da conta corrente) — essa parte nunca aparece em (1).
 */
export function calcularCapitalInvestido(contasBancarias: ContaBancaria[], registros: Registro[]): number {
  const saldoContasInvestimento = contasBancarias
    .filter(c => c.tipo === 'Investimento' && c.ativa)
    .reduce((s, c) => s + c.saldoAtual, 0)

  const contasInvestimentoIds = new Set(contasBancarias.filter(c => c.tipo === 'Investimento').map(c => c.id))
  const fluxoForaDeContaInvestimento = registros.reduce((total, r) => {
    if (r.contaBancariaId && contasInvestimentoIds.has(r.contaBancariaId)) return total // já contado acima
    const aportes = r.saidas.filter(s => s.tipoCusto === 'Investimento').reduce((s, i) => s + i.valor, 0)
    const resgates = r.entradas.filter(e => e.tipoCusto === 'Investimento').reduce((s, i) => s + i.valor, 0)
    return total + aportes - resgates
  }, 0)

  return saldoContasInvestimento + fluxoForaDeContaInvestimento
}

export interface ProjecaoSelic {
  dez: number
  vinte: number
  trinta: number
}

/** Projeção bruta (sem IR, sem inflação, sem aportes futuros) de `capitalInvestido` composto à SELIC. */
export function calcularProjecaoSelic(
  capitalInvestido: number | null | undefined,
  selicAnual: number | null | undefined,
): ProjecaoSelic | null {
  if (!capitalInvestido || capitalInvestido <= 0) return null
  if (!selicAnual || selicAnual <= 0) return null

  const taxa = 1 + selicAnual / 100
  return {
    dez: capitalInvestido * Math.pow(taxa, 10),
    vinte: capitalInvestido * Math.pow(taxa, 20),
    trinta: capitalInvestido * Math.pow(taxa, 30),
  }
}
