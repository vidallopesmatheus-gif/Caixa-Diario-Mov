import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { fmtBRL, fmtDate } from '../../../utils/format'
import type { Registro, ContaBancaria } from '../../../types'

interface Props {
  registros: Registro[]
  contasBancarias: ContaBancaria[]
  limite?: number
}

interface LinhaAtividade {
  chave: string
  data: string
  descricao: string
  categoria?: string
  contaNome: string
  valor: number
  tipo: 'entrada' | 'saida'
}

export default function AtividadeRecente({ registros, contasBancarias, limite = 6 }: Props) {
  const nomesConta = useMemo(() => {
    const mapa: Record<string, string> = {}
    for (const c of contasBancarias) mapa[c.id] = c.nome
    return mapa
  }, [contasBancarias])

  const linhas = useMemo(() => {
    const todas: LinhaAtividade[] = []
    for (const r of registros) {
      const contaNome = r.contaBancariaId ? (nomesConta[r.contaBancariaId] ?? 'Caixa') : 'Caixa'
      r.entradas.forEach((e, i) => todas.push({
        chave: `${r.id}-e-${i}`, data: r.data, descricao: e.descricao, categoria: e.categoria,
        contaNome, valor: e.valor, tipo: 'entrada',
      }))
      r.saidas.forEach((s, i) => todas.push({
        chave: `${r.id}-s-${i}`, data: r.data, descricao: s.descricao, categoria: s.categoria,
        contaNome, valor: s.valor, tipo: 'saida',
      }))
    }
    return todas.sort((a, b) => b.data.localeCompare(a.data)).slice(0, limite)
  }, [registros, nomesConta, limite])

  return (
    <div className="atividade-card">
      <div className="atividade-header">
        <h3 className="atividade-titulo">🧾 Atividade recente</h3>
        <Link to="/relatorios/historico" className="atividade-ver-todos">Ver todos →</Link>
      </div>

      {linhas.length === 0 ? (
        <p className="atividade-vazio">
          Nenhum lançamento ainda. <Link to="/caixa">Comece registrando o caixa de hoje →</Link>
        </p>
      ) : (
        <div className="atividade-lista">
          {linhas.map(l => (
            <div key={l.chave} className="atividade-linha">
              <span className="atividade-data">{fmtDate(l.data)}</span>
              <span className="atividade-descricao">{l.descricao}</span>
              <span className="atividade-categoria">{l.categoria ?? '—'}</span>
              <span className="atividade-conta">{l.contaNome}</span>
              <span className={`atividade-valor ${l.tipo === 'entrada' ? 'val-green' : 'val-red'}`}>
                {l.tipo === 'entrada' ? '+' : '−'} {fmtBRL(l.valor)}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
