import SaudeFinanceiraGauges from '../SaudeFinanceiraGauges'
import { leituraMargemDre } from '../../../utils/leituras'
import { fmtPct } from '../../../utils/format'

interface Props {
  clienteId: string
  margem: number | null | undefined // undefined = ainda carregando
}

export default function SaudeNegocioBlock({ clienteId, margem }: Props) {
  return (
    <div className="saude-negocio-bloco">
      <SaudeFinanceiraGauges clienteId={clienteId} />

      <div className="saude-margem-card">
        <span className="saude-margem-label">Margem do período</span>
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
