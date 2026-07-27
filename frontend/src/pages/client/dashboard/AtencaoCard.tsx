import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { obterInsights } from '../../../api/insights'
import type { Insight, InsightCategoria } from '../../../api/insights'
import { getContasEmRisco } from '../../../utils/alertas'
import { fmtBRL } from '../../../utils/format'
import type { Registro } from '../../../types'

interface Props {
  clienteId: string
  registros: Registro[]
}

interface ItemAtencao {
  chave: string
  texto: string
  detalhe?: string
  urgente: boolean
  onClick: () => void
}

const ROTA_POR_CATEGORIA: Record<InsightCategoria, string | 'metas'> = {
  saldo: '/projecao',
  gasto: '/grafico',
  lucro: '/grafico',
  meta: 'metas',
  geral: '/grafico',
}

export default function AtencaoCard({ clienteId, registros }: Props) {
  const [insights, setInsights] = useState<Insight[]>([])
  const [carregado, setCarregado] = useState(false)
  const navigate = useNavigate()

  useEffect(() => {
    if (!clienteId) return
    obterInsights(clienteId)
      .then(insights => { setInsights(insights); setCarregado(true) })
      .catch(() => { setInsights([]); setCarregado(true) })
  }, [clienteId])

  function irPara(categoria: InsightCategoria) {
    const rota = ROTA_POR_CATEGORIA[categoria]
    if (rota === 'metas') {
      document.getElementById('metas-investimentos')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    } else {
      navigate(rota)
    }
  }

  if (!carregado) {
    return (
      <div className="atencao-card">
        <h3 className="atencao-titulo">🔎 Precisa de atenção</h3>
        <div className="atencao-skeleton" />
      </div>
    )
  }

  const vencimentos = getContasEmRisco(registros, 3)
  const vencidas = vencimentos.filter(v => v.vencida)
  const aVencer = vencimentos.filter(v => !v.vencida)
  const alertas = insights.filter(i => i.tipo === 'alerta')

  const itens: ItemAtencao[] = [
    ...vencidas.map((v): ItemAtencao => ({
      chave: `venc-${v.registroData}-${v.tipo}-${v.index}`,
      texto: `${v.tipo === 'pagar' ? 'Conta vencida' : 'Recebimento atrasado'}: ${v.conta.descricao}`,
      detalhe: `${fmtBRL(v.conta.valor)} · venceu em ${v.conta.dataVencimento?.split('-').reverse().join('/')}`,
      urgente: true,
      onClick: () => navigate('/contas'),
    })),
    ...alertas.map((a): ItemAtencao => ({
      chave: `insight-${a.prioridade}-${a.texto}`,
      texto: a.texto,
      detalhe: a.detalhe,
      urgente: true,
      onClick: () => irPara(a.categoria),
    })),
    ...aVencer.map((v): ItemAtencao => ({
      chave: `avencer-${v.registroData}-${v.tipo}-${v.index}`,
      texto: `${v.tipo === 'pagar' ? 'Conta a pagar' : 'Recebimento previsto'}: ${v.conta.descricao}`,
      detalhe: `${fmtBRL(v.conta.valor)} · vence em ${v.conta.dataVencimento?.split('-').reverse().join('/')}`,
      urgente: false,
      onClick: () => navigate('/contas'),
    })),
  ].slice(0, 3)

  return (
    <div className="atencao-card">
      <h3 className="atencao-titulo">🔎 Precisa de atenção</h3>
      {itens.length === 0 ? (
        <p className="atencao-vazio">✅ Nada exigindo atenção nos próximos 30 dias.</p>
      ) : (
        <div className="atencao-lista">
          {itens.map(item => (
            <button key={item.chave} type="button" className={`atencao-item${item.urgente ? ' urgente' : ''}`} onClick={item.onClick}>
              <span className="atencao-item-texto">{item.texto}</span>
              {item.detalhe && <span className="atencao-item-detalhe">{item.detalhe}</span>}
              <span className="atencao-item-seta">→</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
