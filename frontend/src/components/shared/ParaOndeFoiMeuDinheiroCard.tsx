import type { ParaOndeFoiMeuDinheiro } from '../../api/metricas'
import { fmtBRL, fmtPct } from '../../utils/format'
import './ParaOndeFoiMeuDinheiroCard.css'

interface Props {
  dados: ParaOndeFoiMeuDinheiro
}

/**
 * Money Vision — Bloco 5, item 2 da Parte 3: por que o saldo em conta corrente parece baixo
 * mesmo com receita boa — porque a diferença foi guardada em investimento, não gasta.
 */
export default function ParaOndeFoiMeuDinheiroCard({ dados }: Props) {
  const { entrou, gastou, guardou, ficou, taxaPoupancaPercentual } = dados

  const corTaxa = taxaPoupancaPercentual == null ? 'var(--tx3)'
    : taxaPoupancaPercentual < 0 ? 'var(--danger)'
    : taxaPoupancaPercentual < 10 ? 'var(--warning)'
    : 'var(--success)'

  const frase = guardou > 0
    ? `Seu saldo em conta está baixo porque você guardou ${fmtBRL(guardou)} este mês. Seu patrimônio cresceu ${fmtBRL(guardou)}.`
    : guardou < 0
      ? `Você resgatou ${fmtBRL(-guardou)} da reserva este mês.`
      : 'Nenhuma aplicação ou resgate em investimento este mês.'

  return (
    <div className="pofmd-card">
      <div className="pofmd-titulo">💸 Para onde foi meu dinheiro</div>
      <div className="pofmd-fluxo">
        <span className="pofmd-passo"><span className="pofmd-rotulo">Entrou</span><span className="val-green">{fmtBRL(entrou)}</span></span>
        <span className="pofmd-seta">→</span>
        <span className="pofmd-passo"><span className="pofmd-rotulo">Gastou</span><span className="val-red">{fmtBRL(gastou)}</span></span>
        <span className="pofmd-seta">→</span>
        <span className="pofmd-passo">
          <span className="pofmd-rotulo">{guardou >= 0 ? 'Guardou' : 'Resgatou'}</span>
          <span className={guardou >= 0 ? 'val-green' : 'val-red'}>{fmtBRL(Math.abs(guardou))}</span>
        </span>
        <span className="pofmd-seta">→</span>
        <span className="pofmd-passo"><span className="pofmd-rotulo">Ficou na conta</span><span>{fmtBRL(ficou)}</span></span>
      </div>
      <p className="pofmd-frase">{frase}</p>
      {taxaPoupancaPercentual != null && (
        <div className="pofmd-taxa">
          Taxa de poupança: <strong style={{ color: corTaxa }}>{fmtPct(taxaPoupancaPercentual)}</strong>
          {taxaPoupancaPercentual < 0 && <span style={{ color: 'var(--danger)' }}> — você usou a reserva</span>}
        </div>
      )}
    </div>
  )
}
