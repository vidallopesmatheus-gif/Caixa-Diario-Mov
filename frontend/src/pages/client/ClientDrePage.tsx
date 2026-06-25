import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterDre } from '../../api/metricas'
import { listarContasBancarias } from '../../api/contasBancarias'
import { fmtBRL } from '../../utils/format'
import type { Dre } from '../../api/metricas'
import type { ContaBancaria } from '../../types'
import './ClientDre.css'

type TipoPeriodo = 'mes' | 'trimestre' | 'ano'

const MESES = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']

function periodoParaDatas(tipo: TipoPeriodo, ano: number, mes: number): { de: string; ate: string } {
  if (tipo === 'mes') {
    const ultimo = new Date(ano, mes, 0).getDate()
    return {
      de: `${ano}-${String(mes).padStart(2, '0')}-01`,
      ate: `${ano}-${String(mes).padStart(2, '0')}-${ultimo}`,
    }
  }
  if (tipo === 'trimestre') {
    const q = Math.ceil(mes / 3)
    const mesInicio = (q - 1) * 3 + 1
    const mesFim = q * 3
    const ultimoDia = new Date(ano, mesFim, 0).getDate()
    return {
      de: `${ano}-${String(mesInicio).padStart(2, '0')}-01`,
      ate: `${ano}-${String(mesFim).padStart(2, '0')}-${ultimoDia}`,
    }
  }
  return { de: `${ano}-01-01`, ate: `${ano}-12-31` }
}

function labelPeriodo(tipo: TipoPeriodo, ano: number, mes: number): string {
  if (tipo === 'mes') return `${MESES[mes - 1]}/${ano}`
  if (tipo === 'trimestre') return `T${Math.ceil(mes / 3)}/${ano}`
  return `${ano}`
}

export default function ClientDrePage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''

  const hoje = new Date()
  const [tipo, setTipo] = useState<TipoPeriodo>('mes')
  const [ano, setAno] = useState(hoje.getFullYear())
  const [mes, setMes] = useState(hoje.getMonth() + 1)
  const [contaFiltro, setContaFiltro] = useState<string>('')
  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [dre, setDre] = useState<Dre | null>(null)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')
  const [expanded, setExpanded] = useState<Record<string, boolean>>({})

  useEffect(() => {
    if (!clienteId) return
    listarContasBancarias(clienteId)
      .then(cs => setContas(cs.filter(c => c.ativa)))
      .catch(() => {})
  }, [clienteId])

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true)
    setErro('')
    try {
      const { de, ate } = periodoParaDatas(tipo, ano, mes)
      const data = await obterDre(clienteId, de, ate, contaFiltro || undefined)
      setDre(data)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar DRE.')
    } finally {
      setLoading(false)
    }
  }, [clienteId, tipo, ano, mes, contaFiltro])

  useEffect(() => { carregar() }, [carregar])

  function toggleGrupo(grupo: string) {
    setExpanded(prev => ({ ...prev, [grupo]: !prev[grupo] }))
  }

  const anosOpcoes = Array.from({ length: 5 }, (_, i) => hoje.getFullYear() - 2 + i)
  const trimestresOpcoes = [1, 2, 3, 4]

  return (
    <>
      {/* Cabeçalho + controles */}
      <div className="dre-header">
        <div>
          <h2 className="dre-titulo">📑 DRE Simplificado</h2>
          <div className="dre-subtitulo">
            Demonstrativo de Resultado · {labelPeriodo(tipo, ano, mes)}
          </div>
        </div>
      </div>

      {/* Seletor de período */}
      <div className="dre-controles">
        <div className="dre-tipo-tabs">
          {(['mes', 'trimestre', 'ano'] as TipoPeriodo[]).map(t => (
            <button
              key={t}
              className={`dre-tipo-btn${tipo === t ? ' active' : ''}`}
              onClick={() => setTipo(t)}
            >
              {t === 'mes' ? 'Mês' : t === 'trimestre' ? 'Trimestre' : 'Ano'}
            </button>
          ))}
        </div>

        <div className="dre-filtros">
          <select className="dre-select" value={ano} onChange={e => setAno(+e.target.value)}>
            {anosOpcoes.map(a => <option key={a} value={a}>{a}</option>)}
          </select>

          {tipo === 'mes' && (
            <select className="dre-select" value={mes} onChange={e => setMes(+e.target.value)}>
              {MESES.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
            </select>
          )}

          {tipo === 'trimestre' && (
            <select className="dre-select" value={Math.ceil(mes / 3)} onChange={e => setMes((+e.target.value - 1) * 3 + 1)}>
              {trimestresOpcoes.map(q => <option key={q} value={q}>T{q}</option>)}
            </select>
          )}

          {contas.length > 1 && (
            <select className="dre-select" value={contaFiltro} onChange={e => setContaFiltro(e.target.value)}>
              <option value="">Todas as contas</option>
              {contas.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          )}
        </div>
      </div>

      {loading && <p className="dre-loading">Calculando...</p>}
      {erro && <p className="dre-erro">{erro}</p>}

      {dre && !loading && (
        <div className="dre-corpo">
          {/* Receita Bruta */}
          <div className="dre-linha dre-linha-receita">
            <span className="dre-linha-label">Receita Bruta</span>
            <span className="dre-linha-valor val-green">{fmtBRL(dre.receitaBruta)}</span>
          </div>

          {/* Divisor */}
          <div className="dre-divisor" />

          {/* Grupos de despesa */}
          {dre.gruposDespesa.length === 0 && (
            <div className="dre-vazio">Nenhuma despesa registrada no período.</div>
          )}
          {dre.gruposDespesa.map(linha => (
            <div key={linha.grupo}>
              <div
                className="dre-linha dre-linha-grupo"
                onClick={() => toggleGrupo(linha.grupo)}
                role="button"
              >
                <span className="dre-linha-label">
                  <span className="dre-expand">{expanded[linha.grupo] ? '▾' : '▸'}</span>
                  (-) {linha.grupo}
                </span>
                <span className="dre-linha-valor val-red">({fmtBRL(linha.total)})</span>
              </div>

              {expanded[linha.grupo] && linha.categorias.map(cat => (
                <div key={cat.nome} className="dre-linha dre-linha-cat">
                  <span className="dre-linha-label dre-cat-nome">{cat.nome}</span>
                  <span className="dre-linha-valor dre-cat-valor">({fmtBRL(cat.total)})</span>
                </div>
              ))}
            </div>
          ))}

          <div className="dre-divisor" />

          {/* Total Despesas */}
          <div className="dre-linha dre-linha-total-desp">
            <span className="dre-linha-label">Total Custos e Despesas</span>
            <span className="dre-linha-valor val-red">({fmtBRL(dre.totalDespesas)})</span>
          </div>

          <div className="dre-divisor dre-divisor-duplo" />

          {/* Resultado */}
          <div className="dre-linha dre-linha-resultado">
            <span className="dre-linha-label">
              {dre.resultado >= 0 ? '= Lucro do Período' : '= Prejuízo do Período'}
            </span>
            <span className={`dre-linha-valor dre-resultado-val ${dre.resultado >= 0 ? 'val-green' : 'val-red'}`}>
              {fmtBRL(dre.resultado)}
            </span>
          </div>

          {dre.margem !== null && (
            <div className="dre-margem">
              Margem:{' '}
              <span className={dre.margem >= 0 ? 'val-green' : 'val-red'}>
                {dre.margem.toFixed(1)}%
              </span>
            </div>
          )}
        </div>
      )}
    </>
  )
}
