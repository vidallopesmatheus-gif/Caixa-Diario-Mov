import { obterExtratoConta } from '../api/contasBancarias'
import { addDays } from './format'
import type { LancamentoExtrato } from '../types'

/**
 * Procura, na conta contrapartida, um lançamento do tipo oposto com o mesmo valor numa janela de
 * ±3 dias — indício de que a outra ponta da transferência já foi importada separadamente (ex.: o
 * extrato da conta de investimento já trouxe a entrada correspondente à aplicação). Achando, a
 * conversão deve VINCULAR a ele em vez de criar um lançamento novo, senão duplica a transferência.
 */
export async function buscarCandidatoContrapartida(
  contaContrapartidaId: string,
  data: string,
  valorAbsoluto: number,
  tipoOriginal: 'Entrada' | 'Saida',
): Promise<LancamentoExtrato | null> {
  const tipoEsperado = tipoOriginal === 'Saida' ? 'Entrada' : 'Saida'
  const de = addDays(data, -3)
  const ate = addDays(data, 3)
  try {
    const lancamentos = await obterExtratoConta(contaContrapartidaId, de, ate)
    const candidato = lancamentos.find(l =>
      l.id &&
      l.categoria !== 'Transferência' &&
      (tipoEsperado === 'Entrada' ? l.valor > 0 : l.valor < 0) &&
      Math.abs(Math.abs(l.valor) - valorAbsoluto) < 0.01
    )
    return candidato ?? null
  } catch {
    return null
  }
}
