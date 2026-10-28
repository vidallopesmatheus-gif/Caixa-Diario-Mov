import { useState, useEffect } from 'react'
import { obterOrcamentoDinamico } from '../../api/orcamentoDinamico'
import type { OrcamentoDinamico } from '../../api/orcamentoDinamico'
import { fmtBRL, fmtPct } from '../../utils/format'
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
  // Money Vision: "números que se contradizem" (item 1) — "disponível" tem que ser o que AINDA
  // sobra (saldo livre − o que já foi gasto), nunca o teto do orçamento sozinho (saldoLivre), que
  // dava a impressão de ter dinheiro disponível mesmo já tendo passado do limite.
  const disponivel = dados.saldoLivre - dados.gastoVariavelAtual

  return (
    <div className="orcamento-card">
      <div className="orcamento-header">
        <h3 className="orcamento-titulo">💰 Quanto posso gastar?</h3>
        <span className="orcamento-estimativa">estimativa</span>
      </div>

      <p className={`orcamento-saldo-livre ${status}`}>
        {dados.saldoLivre > 0 ? fmtBRL(Math.abs(disponivel)) : '–'}
      </p>
      <p className="orcamento-sublabel">
        {dados.saldoLivre <= 0
          ? 'sem margem — receita não cobre compromissos e aportes'
          : disponivel >= 0
            ? 'disponível para gastos variáveis este mês'
            : 'você passou do limite de gastos variáveis este mês'}
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
          <> ({fmtPct(dados.percentualUtilizado)} do limite)</>
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
