import { useCallback, useEffect, useState } from 'react'
import { confirmarAjusteSaldo, listarAjustesSaldo } from '../../api/conciliacaoSaldo'
import { excluirLancamento } from '../../api/importacao'
import { fmtBRL, fmtDate } from '../../utils/format'
import type { AjusteSaldo } from '../../types'
import './Conciliacao.css'

interface Props {
  contaId: string
  /** Desfazer mudou o saldo — a tela recarrega conta/extrato. */
  onAlterado: () => void
}

/**
 * Money Vision ("ajustes da migração que o usuário não entende"): dentro do "Conferir saldo", cada
 * ajuste aparece com o que fez com o saldo do dia, pra confirmar (fica) ou desfazer (sai).
 */
export default function AjustesSaldoLista({ contaId, onAlterado }: Props) {
  const [ajustes, setAjustes] = useState<AjusteSaldo[]>([])
  const [processandoId, setProcessandoId] = useState<string | null>(null)
  const [erro, setErro] = useState('')

  const carregar = useCallback(() =>
    listarAjustesSaldo(contaId)
      .then(setAjustes)
      .catch((e: unknown) => setErro(e instanceof Error ? e.message : 'Erro ao carregar os ajustes.')),
  [contaId])

  useEffect(() => { carregar() }, [carregar])

  async function executar(id: string, acao: () => Promise<unknown>, alterouSaldo: boolean) {
    setProcessandoId(id)
    setErro('')
    try {
      await acao()
      if (alterouSaldo) onAlterado()
      await carregar()
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao atualizar o ajuste.')
    } finally {
      setProcessandoId(null)
    }
  }

  const aRevisar = ajustes.filter(a => !a.confirmado)
  if (ajustes.length === 0 && !erro) return null

  return (
    <section className="conc-diag-secao" aria-label="Ajustes de saldo desta conta">
      <h4>Ajustes de saldo desta conta</h4>
      {erro && <div className="cd-msg">{erro}</div>}
      {aRevisar.length === 0 ? (
        <p className="conc-dica">✅ Todos os {ajustes.length} ajuste(s) já foram conferidos.</p>
      ) : (
        <ul>
          {aRevisar.map(a => (
            <li key={a.id} className="conc-diag-linha">
              <span>
                {fmtDate(a.data)} · {a.descricao} · <strong className={a.valor >= 0 ? 'val-green' : 'val-red'}>{fmtBRL(a.valor)}</strong>
                <br />
                <span className="conc-dica">Saldo do dia: {fmtBRL(a.saldoDoDiaSemAjuste)} → {fmtBRL(a.saldoDoDia)}</span>
              </span>
              <span>
                <button type="button" className="conc-link" disabled={processandoId !== null}
                  onClick={() => executar(a.id, () => confirmarAjusteSaldo(contaId, a.id), false)}>
                  Está certo
                </button>
                {' · '}
                <button type="button" className="conc-link" disabled={processandoId !== null}
                  onClick={() => executar(a.id, () => excluirLancamento(contaId, { id: a.id, data: a.data }), true)}>
                  Desfazer
                </button>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
