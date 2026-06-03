import { useMemo, useState } from 'react'
import type { Registro } from '../../types'
import { LISTA_CATEGORIAS } from '../../config/categorias'
import { fmtBRL } from '../../utils/format'
import './RelatorioCategoriasCard.css'

interface Props {
  registros: Registro[]
}

export default function RelatorioCategoriasCard({ registros }: Props) {
  const hoje = new Date()
  const primeiroDiaMes = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}-01`
  const ultimoDiaMes = new Date(hoje.getFullYear(), hoje.getMonth() + 1, 0).toISOString().slice(0, 10)

  const [de, setDe] = useState(primeiroDiaMes)
  const [ate, setAte] = useState(ultimoDiaMes)

  const totais = useMemo(() => {
    const acc: Record<string, number> = {}
    for (const cat of LISTA_CATEGORIAS) acc[cat] = 0
    for (const reg of registros) {
      if (reg.data < de || reg.data > ate) continue
      for (const s of reg.saidas) {
        const cat = s.categoria ?? 'Administrativas'
        if (cat in acc) acc[cat] += s.valor
      }
    }
    return acc
  }, [registros, de, ate])

  const totalGeral = Object.values(totais).reduce((a, b) => a + b, 0)
  const maxValor = Math.max(...Object.values(totais), 1)

  return (
    <div className="relcat-card">
      <h3 className="relcat-titulo">Gastos por Categoria</h3>
      <div className="relcat-periodo">
        <label>De <input type="date" value={de} onChange={e => setDe(e.target.value)} /></label>
        <label>Até <input type="date" value={ate} onChange={e => setAte(e.target.value)} /></label>
      </div>
      <div className="relcat-lista">
        {LISTA_CATEGORIAS.map(cat => {
          const valor = totais[cat]
          const pct = totalGeral > 0 ? Math.round((valor / totalGeral) * 100) : 0
          const largura = Math.round((valor / maxValor) * 100)
          return (
            <div key={cat} className={`relcat-row ${valor === 0 ? 'relcat-zero' : ''}`}>
              <span className="relcat-cat">{cat}</span>
              <div className="relcat-barra-bg">
                <div className="relcat-barra" style={{ width: `${largura}%` }} />
              </div>
              <span className="relcat-valor">{fmtBRL(valor)}</span>
              <span className="relcat-pct">{pct}%</span>
            </div>
          )
        })}
      </div>
      <div className="relcat-total">
        <span>Total</span>
        <span>{fmtBRL(totalGeral)}</span>
      </div>
    </div>
  )
}
