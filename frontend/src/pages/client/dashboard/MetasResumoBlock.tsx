import { useState, useEffect, useMemo } from 'react'
import { listarMetas } from '../../../api/metas'
import { fmtBRL, fmtPct } from '../../../utils/format'
import { calcularRitmoMeta } from '../../../utils/metaRitmo'
import type { MetaAnual } from '../../../types'

interface Props { clienteId: string }

function chaveStorage(clienteId: string) {
  return `caixaDiario:dashboardMetaSelecionada:${clienteId}`
}

function scrollParaMetas() {
  document.getElementById('metas-investimentos')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function fmtDataAlvo(d: Date | null): string {
  if (!d) return '—'
  return d.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' })
}

/** Camada 0 do Dashboard: resumo da meta selecionada (ou convite pra criar a primeira). Só exibe e navega. */
export default function MetasResumoBlock({ clienteId }: Props) {
  const [metas, setMetas] = useState<MetaAnual[]>([])
  const [loading, setLoading] = useState(true)
  const [selecionadaId, setSelecionadaId] = useState<string | null>(null)

  useEffect(() => {
    if (!clienteId) return
    setLoading(true)
    listarMetas(clienteId).then(setMetas).catch(() => setMetas([])).finally(() => setLoading(false))
  }, [clienteId])

  // Elegíveis: metas com um "sonho" de investimento configurado (método com valor-alvo).
  // Uma meta "simples" (só receita/lucro do ano) não tem valor-alvo/acumulado pra mostrar aqui.
  const elegiveis = useMemo(
    () => metas.filter(m => m.modoMeta === 'metodo' && m.valorSonho > 0).sort((a, b) => b.ano - a.ano),
    [metas]
  )
  const chaveElegiveis = elegiveis.map(m => m.id).join(',')

  useEffect(() => {
    if (elegiveis.length === 0) return
    const salva = localStorage.getItem(chaveStorage(clienteId))
    setSelecionadaId(salva && elegiveis.some(m => m.id === salva) ? salva : elegiveis[0].id)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [chaveElegiveis, clienteId])

  function selecionar(id: string) {
    setSelecionadaId(id)
    try { localStorage.setItem(chaveStorage(clienteId), id) } catch { /* ignora storage indisponível */ }
  }

  if (loading) {
    return <div className="meta-resumo-bloco meta-resumo-vazio"><div className="resumo-skeleton" /></div>
  }

  if (elegiveis.length === 0) {
    return (
      <button className="meta-resumo-bloco meta-resumo-vazio" onClick={scrollParaMetas} type="button">
        <span className="meta-resumo-vazio-icone">🎯</span>
        <span className="meta-resumo-vazio-texto">
          Você ainda não tem uma meta de investimento cadastrada. <strong>Criar a primeira meta →</strong>
        </span>
      </button>
    )
  }

  const meta = elegiveis.find(m => m.id === selecionadaId) ?? elegiveis[0]
  const ritmo = calcularRitmoMeta(meta)
  const pctBarra = Math.max(0, Math.min(100, ritmo.percentual))

  const leitura = (() => {
    switch (ritmo.status) {
      case 'atingida': return '🎉 Meta atingida!'
      case 'adiantada': return '🟢 Adiantado em relação ao ritmo planejado.'
      case 'no-ritmo': return '🔵 No ritmo — continue com o aporte planejado.'
      case 'atrasada': return `🟡 Atrasado — para voltar ao prazo, o aporte precisa subir para ${fmtBRL(ritmo.aporteNecessarioAgora ?? 0)}/mês.`
      default: return 'Defina prazo e taxa de retorno para acompanhar o ritmo.'
    }
  })()

  return (
    <div
      className="meta-resumo-bloco"
      role="button"
      tabIndex={0}
      onClick={scrollParaMetas}
      onKeyDown={e => { if (e.key === 'Enter') scrollParaMetas() }}
    >
      <div className="meta-resumo-header">
        <div className="meta-resumo-titulo">
          {meta.iconeSonho ? `${meta.iconeSonho} ` : '🎯 '}{meta.sonho || `Meta ${meta.ano}`}
        </div>
        {elegiveis.length > 1 && (
          <div className="meta-resumo-chips" onClick={e => e.stopPropagation()}>
            {elegiveis.map(m => (
              <button
                key={m.id}
                type="button"
                className={`meta-resumo-chip${m.id === selecionadaId ? ' active' : ''}`}
                onClick={() => selecionar(m.id)}
              >
                {m.iconeSonho ? `${m.iconeSonho} ` : ''}{m.sonho ? m.sonho.slice(0, 14) : m.ano}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="meta-resumo-valores">
        <span>{fmtBRL(meta.totalInvestido)} <span className="meta-resumo-de">de {fmtBRL(meta.valorSonho)}</span></span>
        <span className="meta-resumo-pct">{fmtPct(ritmo.percentual)}</span>
      </div>

      <div className="meta-resumo-barra">
        <div className="meta-resumo-barra-fill" style={{ width: `${pctBarra}%` }} />
      </div>

      <div className="meta-resumo-rodape">
        <span className="meta-resumo-leitura">{leitura}</span>
        <span className="meta-resumo-prazo">Alvo: {fmtDataAlvo(ritmo.dataAlvo)}</span>
      </div>

      {meta.contaInvestimentoId && (
        <div className="meta-resumo-vinculo">
          🔗 Vinculada a uma conta de investimento — progresso atualizado automaticamente pelo saldo da conta.
        </div>
      )}
    </div>
  )
}
