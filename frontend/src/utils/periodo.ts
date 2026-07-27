import { addDays, todayISO } from './format'

export type PeriodoOpcao = 'hoje' | '7dias' | 'mes' | 'personalizado'

export interface JanelaPeriodo {
  de: string
  ate: string
  deAnterior: string
  ateAnterior: string
  label: string
}

function diffDias(de: string, ate: string): number {
  const a = new Date(de + 'T12:00:00')
  const b = new Date(ate + 'T12:00:00')
  return Math.round((b.getTime() - a.getTime()) / 86400000) + 1
}

function primeiroDiaMes(iso: string): string {
  return `${iso.slice(0, 7)}-01`
}

function ultimoDiaMes(iso: string): string {
  const [ano, mes] = iso.split('-').map(Number)
  return new Date(ano, mes, 0).toISOString().slice(0, 10)
}

/**
 * Resolve a janela do período selecionado e a janela equivalente imediatamente anterior,
 * usada como base de comparação ("vs. período anterior") nos cards do dashboard.
 */
export function calcularJanelaPeriodo(
  opcao: PeriodoOpcao,
  personalizado?: { de: string; ate: string },
): JanelaPeriodo {
  const hoje = todayISO()

  switch (opcao) {
    case 'hoje':
      return { de: hoje, ate: hoje, deAnterior: addDays(hoje, -1), ateAnterior: addDays(hoje, -1), label: 'hoje' }

    case '7dias': {
      const de = addDays(hoje, -6)
      return { de, ate: hoje, deAnterior: addDays(de, -7), ateAnterior: addDays(de, -1), label: 'últimos 7 dias' }
    }

    case 'mes': {
      const de = primeiroDiaMes(hoje)
      const ate = ultimoDiaMes(hoje)
      const mesAnteriorRef = addDays(de, -1) // último dia do mês anterior
      return {
        de, ate,
        deAnterior: primeiroDiaMes(mesAnteriorRef),
        ateAnterior: ultimoDiaMes(mesAnteriorRef),
        label: 'mês atual',
      }
    }

    case 'personalizado': {
      const de = personalizado?.de ?? hoje
      const ate = personalizado?.ate ?? hoje
      const dias = Math.max(1, diffDias(de, ate))
      return {
        de, ate,
        deAnterior: addDays(de, -dias),
        ateAnterior: addDays(de, -1),
        label: 'período selecionado',
      }
    }
  }
}
