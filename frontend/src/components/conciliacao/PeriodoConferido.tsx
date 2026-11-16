import { useEffect, useState } from 'react'
import Modal from '../shared/Modal'
import { listarConferencias, reabrirPeriodo } from '../../api/contasBancarias'
import { useAuth } from '../../contexts/AuthContext'
import { fmtBRL, fmtDate } from '../../utils/format'
import type { ConferenciaSaldo, ContaBancaria } from '../../types'
import type { ErroPeriodoConferido } from '../../utils/periodoConferido'
import './Conciliacao.css'

type ContaComTrava = Pick<ContaBancaria, 'id' | 'nome' | 'travadoAte' | 'travaSaldoBanco' | 'travadoPorConsultor'>

function podeReabrir(peloConsultor: boolean | undefined, perfil: string | undefined) {
  return !(peloConsultor && perfil === 'cliente')
}

/** "🔒 Conferido até 07/11/2026 · Banco R$ 86,40 ✅" + "Reabrir período". */
export function SeloPeriodoConferido({ conta, onReabrir }: { conta: ContaComTrava; onReabrir?: () => void }) {
  const { user } = useAuth()
  if (!conta.travadoAte) return null
  return (
    <div className="conc-trava" role="status">
      <span>
        🔒 Conferido até {fmtDate(conta.travadoAte)}
        {conta.travaSaldoBanco != null && <> · Banco {fmtBRL(conta.travaSaldoBanco)} ✅</>}
      </span>
      {onReabrir && (podeReabrir(conta.travadoPorConsultor, user?.perfil)
        ? <button type="button" className="conc-link" onClick={onReabrir}>Reabrir período</button>
        : <span className="conc-dica">Período conferido pelo consultor — peça para reabrir</span>)}
    </div>
  )
}

/** Aviso de um 409 PERIODO_CONFERIDO com o botão "Reabrir período". */
export function AvisoPeriodoConferido({ erro, onReabrir }: { erro: ErroPeriodoConferido; onReabrir: () => void }) {
  const { user } = useAuth()
  return (
    <div className="conc-trava" role="alert">
      <span>🔒 {erro.mensagem}</span>
      {podeReabrir(erro.peloConsultor, user?.perfil) && (
        <button type="button" className="conc-link" onClick={onReabrir}>Reabrir período</button>
      )}
    </div>
  )
}

interface ReabrirProps {
  conta: ContaComTrava
  /** Padrão do "a partir de": a data do lançamento que o usuário quer mexer. */
  dataSugerida?: string
  onClose: () => void
  onReaberto: () => void
}

export default function ReabrirPeriodoModal({ conta, dataSugerida, onClose, onReaberto }: ReabrirProps) {
  const travadoAte = conta.travadoAte ?? ''
  const [tudo, setTudo] = useState(false)
  const [aPartirDe, setAPartirDe] = useState(dataSugerida && dataSugerida <= travadoAte ? dataSugerida : travadoAte)
  const [motivo, setMotivo] = useState('')
  const [historico, setHistorico] = useState<ConferenciaSaldo[]>([])
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState('')

  useEffect(() => {
    listarConferencias(conta.id).then(setHistorico).catch(() => setHistorico([]))
  }, [conta.id])

  async function reabrir() {
    setSalvando(true)
    setErro('')
    try {
      await reabrirPeriodo(conta.id, tudo ? null : aPartirDe, motivo)
      onReaberto()
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao reabrir o período.')
    } finally {
      setSalvando(false)
    }
  }

  return (
    <Modal
      open
      title={`🔓 Reabrir período · ${conta.nome}`}
      onClose={() => { if (!salvando) onClose() }}
      footer={
        <>
          <button className="btn-cancel" onClick={onClose} disabled={salvando}>Cancelar</button>
          <button className="btn-confirm" onClick={reabrir} disabled={salvando || (!tudo && !aPartirDe)}>
            {salvando ? 'Reabrindo...' : 'Reabrir'}
          </button>
        </>
      }
    >
      <p className="conc-dica" style={{ marginBottom: 12 }}>Conferido até {fmtDate(travadoAte)}. A partir de qual data reabrir?</p>
      <div className="inp-group">
        <label>
          <input type="radio" name="reabrir-modo" checked={!tudo} onChange={() => setTudo(false)} /> A partir de
        </label>
        <input type="date" aria-label="Reabrir a partir de" value={aPartirDe} max={travadoAte} disabled={tudo}
          onChange={e => setAPartirDe(e.target.value)} />
        {!tudo && aPartirDe && (
          <p className="conc-dica">Fica conferido até o dia anterior; de {fmtDate(aPartirDe)} em diante, pode importar e editar.</p>
        )}
        <label>
          <input type="radio" name="reabrir-modo" checked={tudo} onChange={() => setTudo(true)} /> Reabrir tudo
        </label>
      </div>
      <div className="inp-group">
        <label htmlFor="reabrir-motivo">Motivo (opcional)</label>
        <input id="reabrir-motivo" type="text" placeholder="Ex.: banco lançou estorno com data de 03/11"
          value={motivo} onChange={e => setMotivo(e.target.value)} />
      </div>
      <p className="conc-dica">Depois de corrigir, confira o saldo de novo para travar.</p>
      {erro && <div className="cd-msg">{erro}</div>}

      {historico.length > 0 && (
        <section className="conc-diag-secao" aria-label="Histórico de conferências">
          <h4>Histórico</h4>
          <ul>
            {historico.map((h, i) => (
              <li key={i} className="conc-diag-linha">
                <span>
                  {fmtDate(h.ocorridoEm.slice(0, 10))} · {h.quem} ·{' '}
                  {h.tipo === 'Conferencia'
                    ? <>conferiu até {fmtDate(h.data!)}{h.saldoBanco != null && <> (banco {fmtBRL(h.saldoBanco)})</>}</>
                    : <>reabriu {h.data ? `a partir de ${fmtDate(h.data)}` : 'tudo'}{h.motivo && <> — “{h.motivo}”</>}</>}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </Modal>
  )
}
