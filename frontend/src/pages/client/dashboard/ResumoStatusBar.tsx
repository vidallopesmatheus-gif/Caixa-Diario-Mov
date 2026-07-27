import { fmtBRL } from '../../../utils/format'
import type { PeriodoOpcao } from '../../../utils/periodo'
import type { ContaBancaria } from '../../../types'

interface Props {
  contasBancarias: ContaBancaria[]
  contaFiltro: string | null
  onContaFiltroChange: (id: string | null) => void
  periodoOpcao: PeriodoOpcao
  onPeriodoOpcaoChange: (op: PeriodoOpcao) => void
  personalizadoDe: string
  personalizadoAte: string
  onPersonalizadoChange: (de: string, ate: string) => void
}

const OPCOES: { valor: PeriodoOpcao; label: string }[] = [
  { valor: 'hoje', label: 'Hoje' },
  { valor: '7dias', label: '7 dias' },
  { valor: 'mes', label: 'Mês atual' },
  { valor: 'personalizado', label: 'Personalizado' },
]

export default function ResumoStatusBar({
  contasBancarias, contaFiltro, onContaFiltroChange,
  periodoOpcao, onPeriodoOpcaoChange,
  personalizadoDe, personalizadoAte, onPersonalizadoChange,
}: Props) {
  const contasAtivas = contasBancarias.filter(c => c.ativa)
  const contaSelecionada = contaFiltro ? contasAtivas.find(c => c.id === contaFiltro) : null

  const saldoConsolidado = contaSelecionada
    ? contaSelecionada.saldoAtual
    : contasAtivas.reduce((s, c) => s + c.saldoAtual, 0)

  return (
    <div className="resumo-status-bar">
      <div className="resumo-saldo-principal">
        <span className="resumo-saldo-label">
          {contaSelecionada ? `Saldo — ${contaSelecionada.nome}` : 'Saldo consolidado'}
        </span>
        <span className={`resumo-saldo-valor ${saldoConsolidado >= 0 ? 'val-green' : 'val-red'}`}>
          {fmtBRL(saldoConsolidado)}
        </span>
        {!contaSelecionada && contasAtivas.length > 1 && (
          <span className="resumo-saldo-sub">{contasAtivas.length} contas</span>
        )}
      </div>

      <div className="resumo-controles">
        <div className="resumo-periodo-tabs">
          {OPCOES.map(o => (
            <button
              key={o.valor}
              type="button"
              className={`resumo-periodo-btn${periodoOpcao === o.valor ? ' active' : ''}`}
              onClick={() => onPeriodoOpcaoChange(o.valor)}
            >
              {o.label}
            </button>
          ))}
        </div>

        {periodoOpcao === 'personalizado' && (
          <div className="resumo-periodo-datas">
            <input type="date" value={personalizadoDe} max={personalizadoAte}
              onChange={e => onPersonalizadoChange(e.target.value, personalizadoAte)} />
            <span>até</span>
            <input type="date" value={personalizadoAte} min={personalizadoDe}
              onChange={e => onPersonalizadoChange(personalizadoDe, e.target.value)} />
          </div>
        )}

        {contasAtivas.length > 1 && (
          <select
            className="resumo-conta-select"
            value={contaFiltro ?? ''}
            onChange={e => onContaFiltroChange(e.target.value || null)}
          >
            <option value="">Todas as contas</option>
            {contasAtivas.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        )}
      </div>
    </div>
  )
}
