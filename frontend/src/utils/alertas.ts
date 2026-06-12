import type { Registro, ContaProvisionada } from '../types'

export interface ContaEmRisco {
  registroData: string
  tipo: 'receber' | 'pagar'
  index: number
  conta: ContaProvisionada
  vencida: boolean
}

export function getContasEmRisco(registros: Registro[], diasAntecedencia = 3): ContaEmRisco[] {
  const hoje = new Date()
  hoje.setHours(0, 0, 0, 0)
  const limite = new Date(hoje)
  limite.setDate(limite.getDate() + diasAntecedencia)
  const trintaDiasAtras = new Date(hoje)
  trintaDiasAtras.setDate(trintaDiasAtras.getDate() - 30)

  const resultado: ContaEmRisco[] = []

  for (const reg of registros) {
    const verificar = (contas: ContaProvisionada[], tipo: 'receber' | 'pagar') => {
      contas.forEach((c, i) => {
        if (c.pago) return
        if (!c.dataVencimento) return
        const venc = new Date(c.dataVencimento + 'T00:00:00')
        if (venc < trintaDiasAtras) return
        if (venc <= limite) {
          resultado.push({ registroData: reg.data, tipo, index: i, conta: c, vencida: venc < hoje })
        }
      })
    }
    verificar(reg.contasAReceber, 'receber')
    verificar(reg.contasAPagar, 'pagar')
  }

  return resultado.sort((a, b) => (a.conta.dataVencimento ?? '').localeCompare(b.conta.dataVencimento ?? ''))
}
