import { fmtBRL, fmtDate } from '../../utils/format'
import type { PontoConciliacao, StatusConciliacao } from '../../types'
import './Conciliacao.css'

// A partir daqui, "o extrato está atrasado" — antes disso é só fim de semana/feriado.
export const DIAS_PARA_AVISAR_EXTRATO_ATRASADO = 5

const fmtDiaMes = (iso: string) => fmtDate(iso).slice(0, 5)

interface Props {
  status: StatusConciliacao
  /** Card do Banco: só o essencial, sem botões. */
  compacto?: boolean
  onVerDiagnostico?: (ponto: PontoConciliacao) => void
  onImportar?: () => void
}

/** Money Vision: "Extrato até 07/11/2026 · Saldo do app R$ X · Banco em 07/11 R$ X ✅". */
export default function StatusConciliacaoResumo({ status, compacto, onVerDiagnostico, onImportar }: Props) {
  const ponto = status.ultimoPonto
  const atrasado = status.extratoAte && (status.diasSemExtrato ?? 0) > DIAS_PARA_AVISAR_EXTRATO_ATRASADO

  return (
    <div className={`conc-status${compacto ? ' conc-status--compacto' : ''}`}>
      <div className="conc-status-linha">
        {status.extratoAte
          ? <span>Extrato até <strong>{compacto ? fmtDiaMes(status.extratoAte) : fmtDate(status.extratoAte)}</strong></span>
          : <span>Nenhum extrato importado ainda</span>}
        {/* Card do Banco: "Extrato até 07/11 · App R$ X · Banco R$ X". */}
        <span>{compacto ? 'App' : 'Saldo do app'} <strong>{fmtBRL(status.saldoApp)}</strong></span>
        {ponto && (
          <span className={ponto.conciliado ? 'conc-ok' : 'conc-alerta'}>
            Banco em {fmtDiaMes(ponto.data)} <strong>{fmtBRL(ponto.saldoBanco)}</strong> {ponto.conciliado ? '✅' : '⚠'}
          </span>
        )}
      </div>

      {ponto && !ponto.conciliado && (
        <div className="conc-status-linha conc-alerta">
          ⚠ Diferença de {fmtBRL(Math.abs(ponto.diferenca))} desde {fmtDiaMes(status.divergenteDesde ?? ponto.data)}
          {onVerDiagnostico && (
            <button type="button" className="conc-link" onClick={() => onVerDiagnostico(ponto)}>ver diagnóstico</button>
          )}
        </div>
      )}

      {atrasado && (
        <div className="conc-status-linha conc-aviso">
          Faltam lançamentos desde {fmtDiaMes(status.extratoAte!)}
          {onImportar && <button type="button" className="conc-link" onClick={onImportar}>importar extrato</button>}
        </div>
      )}
    </div>
  )
}
