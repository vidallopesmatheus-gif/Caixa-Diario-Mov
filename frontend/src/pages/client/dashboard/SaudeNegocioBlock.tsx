import SaudeFinanceiraGauges from '../SaudeFinanceiraGauges'
import { leituraMargemDre } from '../../../utils/leituras'
import { fmtPct, fmtDate } from '../../../utils/format'
import type { JanelaPeriodo } from '../../../utils/periodo'

interface Props {
  clienteId: string
  margem: number | null | undefined // undefined = ainda carregando
  janela: JanelaPeriodo
}

export default function SaudeNegocioBlock({ clienteId, margem, janela }: Props) {
  return (
    <div className="saude-negocio-bloco">
      <SaudeFinanceiraGauges clienteId={clienteId} />

      <div className="saude-margem-card">
        <div className="saude-margem-cabecalho">
          <span className="saude-margem-label">Margem do período</span>
          {/* Mesmo seletor de período do topo do Dashboard (Camada 1) — deixado explícito aqui
              porque "—" sem contexto parece erro de cálculo, principalmente quando o período é
              "mês atual" e ainda não há lançamentos nos primeiros dias do mês. */}
          <span className="saude-margem-periodo">{fmtDate(janela.de)} – {fmtDate(janela.ate)}</span>
        </div>
        <span className="saude-margem-valor">
          {margem === undefined ? '' : margem === null ? '—' : fmtPct(margem)}
        </span>
        {margem !== undefined && (
          <p className="saude-margem-leitura">{leituraMargemDre(margem)}</p>
        )}
      </div>
    </div>
  )
}
