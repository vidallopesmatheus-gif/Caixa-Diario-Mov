import { describe, it, expect } from 'vitest'
import { getContasEmRisco } from './alertas'
import type { Registro } from '../types'

function criarRegistro(data: string, contas: { tipo: 'receber' | 'pagar', vencimento: string, pago: boolean }[]): Registro {
  return {
    id: '1', clienteId: 'c1', data,
    saldoInicio: 0, entradas: [], saidas: [],
    contasAReceber: contas.filter(c => c.tipo === 'receber').map(c => ({
      descricao: 'Teste', valor: 100, dataVencimento: c.vencimento, pago: c.pago
    })),
    contasAPagar: contas.filter(c => c.tipo === 'pagar').map(c => ({
      descricao: 'Teste', valor: 100, dataVencimento: c.vencimento, pago: c.pago
    })),
    saldoConfirmado: 0, saldoCalculado: 0, criadoEm: data,
  }
}

describe('getContasEmRisco', () => {
  it('retorna vazia quando não há registros', () => {
    expect(getContasEmRisco([])).toHaveLength(0)
  })

  it('ignora contas já pagas', () => {
    const hoje = new Date().toISOString().slice(0, 10)
    const reg = criarRegistro('2026-01-01', [{ tipo: 'pagar', vencimento: hoje, pago: true }])
    expect(getContasEmRisco([reg])).toHaveLength(0)
  })

  it('inclui contas vencidas não pagas', () => {
    const ontem = new Date(Date.now() - 86400000).toISOString().slice(0, 10)
    const reg = criarRegistro('2026-01-01', [{ tipo: 'pagar', vencimento: ontem, pago: false }])
    const result = getContasEmRisco([reg])
    expect(result).toHaveLength(1)
    expect(result[0].vencida).toBe(true)
  })

  it('inclui contas próximas do vencimento', () => {
    const amanha = new Date(Date.now() + 86400000).toISOString().slice(0, 10)
    const reg = criarRegistro('2026-01-01', [{ tipo: 'receber', vencimento: amanha, pago: false }])
    const result = getContasEmRisco([reg])
    expect(result).toHaveLength(1)
    expect(result[0].vencida).toBe(false)
  })

  it('ignora contas vencidas há mais de 30 dias', () => {
    const muitoAntiga = new Date(Date.now() - 31 * 86400000).toISOString().slice(0, 10)
    const reg = criarRegistro('2026-01-01', [{ tipo: 'pagar', vencimento: muitoAntiga, pago: false }])
    expect(getContasEmRisco([reg])).toHaveLength(0)
  })

  it('ignora contas com vencimento além do período de antecedência', () => {
    const emUmaSemana = new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10)
    const reg = criarRegistro('2026-01-01', [{ tipo: 'pagar', vencimento: emUmaSemana, pago: false }])
    expect(getContasEmRisco([reg], 3)).toHaveLength(0)
  })
})
