import { useCallback, useEffect, useState } from 'react'
import {
  confirmarContrapartidaTransferencia, corrigirContrapartidasTransferencia, descartarContrapartidaTransferencia,
  listarTransferenciasParaRevisar, type RevisaoTransferencias,
} from '../../api/transferencias'
import { fmtBRL, fmtDate } from '../../utils/format'
import './Conciliacao.css'

interface Props {
  clienteId: string
  /** Algo mudou nos saldos — a tela recarrega o que depende deles. */
  onAlterado?: () => void
}

/**
 * Money Vision ("transferência cria contrapartida falsa"): contrapartidas que o app inventou. As que
 * repetem um lançamento real (ou duplicam o par) se corrigem com um clique; as que o extrato da
 * conta não tem, o usuário decide pra onde o dinheiro foi.
 */
export default function TransferenciasParaRevisar({ clienteId, onAlterado }: Props) {
  const [revisao, setRevisao] = useState<RevisaoTransferencias | null>(null)
  const [processando, setProcessando] = useState<string | null>(null)
  const [erro, setErro] = useState('')

  const carregar = useCallback(() =>
    listarTransferenciasParaRevisar(clienteId)
      .then(setRevisao)
      .catch((e: unknown) => setErro(e instanceof Error ? e.message : 'Erro ao carregar as transferências para revisar.')),
  [clienteId])

  useEffect(() => { carregar() }, [carregar])

  async function executar(chave: string, acao: () => Promise<unknown>) {
    setProcessando(chave)
    setErro('')
    try {
      await acao()
      onAlterado?.()
      await carregar()
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao corrigir a transferência.')
    } finally {
      setProcessando(null)
    }
  }

  if (!revisao || (revisao.corrigiveis.length === 0 && revisao.paraDecidir.length === 0)) {
    return erro ? <div className="cd-msg">{erro}</div> : null
  }

  // Só as que mudam o saldo aparecem como "duplicadas"; provisórias são só de rótulo.
  const duplicadas = revisao.corrigiveis.filter(c => c.tipo === 'DuplicadaComReal' || c.tipo === 'ParDuplicado')
  const soRotulo = revisao.corrigiveis.length - duplicadas.length

  return (
    <section className="conc-diag-secao" aria-label="Transferências para revisar" style={{ marginBottom: 20 }}>
      <h3>🔎 Transferências para revisar</h3>
      {erro && <div className="cd-msg">{erro}</div>}

      {revisao.corrigiveis.length > 0 && (
        <div className="conc-diag-destaque">
          {duplicadas.length > 0 && (
            <>
              <strong>{duplicadas.length} transferência(s) contada(s) duas vezes</strong>
              <ul>
                {duplicadas.map(c => (
                  <li key={`${c.transferenciaId}-${c.data}`} className="conc-diag-linha">
                    <span>{c.contaNome} · {fmtDate(c.data)} · {fmtBRL(c.valor)} — {c.acao}</span>
                  </li>
                ))}
              </ul>
            </>
          )}
          {soRotulo > 0 && (
            <p className="conc-dica">{soRotulo} contrapartida(s) criada(s) pelo app ficam provisórias, com descrição própria, até o extrato chegar.</p>
          )}
          <button type="button" className="btn-confirm" disabled={processando !== null}
            onClick={() => executar('corrigir', () => corrigirContrapartidasTransferencia(clienteId))}>
            {processando === 'corrigir' ? 'Corrigindo...' : 'Corrigir agora'}
          </button>
        </div>
      )}

      {revisao.paraDecidir.length > 0 && (
        <>
          <p className="conc-dica">O extrato destas contas não tem o lançamento — pra onde o dinheiro foi?</p>
          <ul>
            {revisao.paraDecidir.map(p => (
              <li key={p.transferenciaId} className="conc-diag-linha">
                <span>
                  {p.contaCriadaNome} · {fmtDate(p.data)} · <strong>{fmtBRL(p.valor)}</strong>
                  <br /><span className="conc-dica">Veio de {p.contaOutraNome}: "{p.descricaoOriginal}"</span>
                </span>
                <span>
                  <button type="button" className="conc-link" disabled={processando !== null}
                    onClick={() => executar(p.transferenciaId, () => confirmarContrapartidaTransferencia(p.transferenciaId))}>
                    Foi para o {p.contaCriadaNome} mesmo
                  </button>
                  {' · '}
                  <button type="button" className="conc-link" disabled={processando !== null}
                    onClick={() => executar(p.transferenciaId, () => descartarContrapartidaTransferencia(p.transferenciaId))}>
                    Não foi para o {p.contaCriadaNome}
                  </button>
                </span>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}
