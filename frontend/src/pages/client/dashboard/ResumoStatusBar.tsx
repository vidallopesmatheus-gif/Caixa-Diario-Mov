import type { PeriodoOpcao } from '../../../utils/periodo'
import type { ContaBancaria } from '../../../types'
import { MvHero, MvTabs } from '../../../components/mv'
import { fmtLidoEm } from '../../../utils/format'

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

  // Cartão (Bloco 3, Money Vision): saldo de cartão é dívida, nunca dinheiro — nunca soma no
  // consolidado nem no patrimônio.
  const saldoConsolidado = contasAtivas
    .filter(c => c.tipo !== 'CartaoCredito' && c.tipo !== 'Investimento')
    .reduce((s, c) => s + c.saldoAtual, 0)
  const saldoInvestido = contasAtivas
    .filter(c => c.tipo === 'Investimento')
    .reduce((s, c) => s + c.saldoAtual, 0)
  const patrimonio = saldoConsolidado + saldoInvestido

  return (
    <>
      {contaSelecionada ? (
        <MvHero
          eyebrow={`Saldo — ${contaSelecionada.nome}`}
          value={contaSelecionada.saldoAtual}
          tone={contaSelecionada.saldoAtual >= 0 ? 'good' : 'bad'}
          footnote={fmtLidoEm('saldo da conta')}
        />
      ) : (
        <MvHero
          eyebrow="Patrimônio"
          value={patrimonio}
          tone={patrimonio >= 0 ? 'good' : 'bad'}
          breakdown={[
            { label: 'Disponível', valor: saldoConsolidado },
            { label: 'Investido', valor: saldoInvestido },
          ]}
          footnote={fmtLidoEm('saldo das contas bancárias')}
        />
      )}

      <div className="mv-controles-row">
        <MvTabs opcoes={OPCOES} ativa={periodoOpcao} onChange={onPeriodoOpcaoChange} />

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
            className="mv-select"
            value={contaFiltro ?? ''}
            onChange={e => onContaFiltroChange(e.target.value || null)}
          >
            <option value="">Todas as contas</option>
            {contasAtivas.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        )}
      </div>
    </>
  )
}
