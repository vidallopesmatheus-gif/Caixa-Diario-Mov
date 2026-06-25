import { useState, useEffect } from 'react'
import { obterOrcamentoDinamico } from '../../api/orcamentoDinamico'
import type { OrcamentoDinamico } from '../../api/orcamentoDinamico'
import { fmtBRL } from '../../utils/format'
import './OrcamentoDinamicoCard.css'

interface Props { clienteId: string }

export default function OrcamentoDinamicoCard({ clienteId }: Props) {
  const [dados, setDados] = useState<OrcamentoDinamico | null>(null)

  useEffect(() => {
    if (!clienteId) return
    obterOrcamentoDinamico(clienteId)
      .then(setDados)
      .catch(() => {})
  }, [clienteId])

  if (!dados) return null

  const status = dados.ultrapassado ? 'alerta' : 'ok'
  const pct = Math.min(100, Math.max(0, dados.saldoLivre > 0 ? dados.percentualUtilizado : 100))

  return (
    <div className="orcamento-card">
      <div className="orcamento-header">
        <h3 className="orcamento-titulo">💰 Quanto posso gastar?</h3>
        <span className="orcamento-estimativa">estimativa</span>
      </div>

      <p className={`orcamento-saldo-livre ${status}`}>
        {dados.saldoLivre > 0 ? fmtBRL(dados.saldoLivre) : '–'}
      </p>
      <p className="orcamento-sublabel">
        {dados.saldoLivre > 0
          ? 'disponível para gastos variáveis este mês'
          : 'sem margem — receita não cobre compromissos e aportes'}
      </p>

      <div className="orcamento-barra-wrap">
        <div
          className={`orcamento-barra-fill ${status}`}
          style={{ width: `${pct}%` }}
        />
      </div>

      <p className="orcamento-gasto-label">
        Gasto variável até agora:{' '}
        <strong>{fmtBRL(dados.gastoVariavelAtual)}</strong>
        {dados.saldoLivre > 0 && (
          <> ({dados.percentualUtilizado.toFixed(0)}% do limite)</>
        )}
      </p>

      <div className="orcamento-breakdown">
        <div className="orcamento-linha">
          <span>Receita esperada (média 3 meses)</span>
          <span>{fmtBRL(dados.receitaEsperada)}</span>
        </div>
        <div className="orcamento-linha">
          <span>Compromissos fixos</span>
          <span>− {fmtBRL(dados.compromissosFixos)}</span>
        </div>
        <div className="orcamento-linha">
          <span>Aporte p/ metas</span>
          <span>− {fmtBRL(dados.aporteNecessario)}</span>
        </div>
        <div className="orcamento-linha total">
          <span>Saldo livre</span>
          <span>{fmtBRL(dados.saldoLivre)}</span>
        </div>
      </div>
    </div>
  )
}
