import { useState } from 'react'
import Modal from '../shared/Modal'
import { converterLancamentoEmTransferencia, listarCandidatosContrapartida, type CandidatosContrapartida } from '../../api/transferencias'
import { fmtBRL, fmtDate } from '../../utils/format'
import type { ContaBancaria } from '../../types'
import './Conciliacao.css'

export interface LancamentoParaTransferencia {
  id: string
  data: string
  descricao: string
  /** Valor absoluto — o sentido vai em `tipo`. */
  valor: number
  tipo: 'Entrada' | 'Saida'
}

interface Props {
  contaId: string
  lancamento: LancamentoParaTransferencia
  contas: ContaBancaria[]
  onClose: () => void
  onConvertido: () => void
  /** "Não foi pra outra conta minha": a tela abre a categorização normal do lançamento. */
  onNaoETransferencia?: () => void
  classeBotaoSecundario?: string
  classeBotaoPrimario?: string
}

/**
 * Money Vision ("transferência cria contrapartida falsa"): antes de classificar, mostra com qual
 * lançamento real da outra conta vai parear. Sem par e com o extrato da outra conta cobrindo a data,
 * avisa em vez de inventar uma entrada; sem extrato na data, explica que fica uma provisória.
 */
export default function ClassificarTransferenciaModal({
  contaId, lancamento, contas, onClose, onConvertido, onNaoETransferencia,
  classeBotaoSecundario = 'btn-cancel', classeBotaoPrimario = 'btn-confirm',
}: Props) {
  const [contaContrapartidaId, setContaContrapartidaId] = useState('')
  const [info, setInfo] = useState<CandidatosContrapartida | null>(null)
  const [escolhidoId, setEscolhidoId] = useState('')
  const [carregando, setCarregando] = useState(false)
  const [convertendo, setConvertendo] = useState(false)
  const [erro, setErro] = useState('')

  const ehSaida = lancamento.tipo === 'Saida'
  const candidatos = info?.candidatos ?? []
  const nomeConta = info?.contaContrapartidaNome ?? ''

  function selecionarConta(id: string) {
    setContaContrapartidaId(id)
    setInfo(null)
    setEscolhidoId('')
    setErro('')
    if (!id) return
    setCarregando(true)
    listarCandidatosContrapartida({ contaId, lancamentoId: lancamento.id, data: lancamento.data, contaContrapartidaId: id })
      .then(r => {
        setInfo(r)
        if (r.candidatos.length === 1) setEscolhidoId(r.candidatos[0].id)
      })
      .catch((e: unknown) => setErro(e instanceof Error ? e.message : 'Erro ao procurar o lançamento na outra conta.'))
      .finally(() => setCarregando(false))
  }

  async function converter(criarMesmoAssim: boolean) {
    const par = candidatos.find(c => c.id === escolhidoId)
    setConvertendo(true)
    setErro('')
    try {
      await converterLancamentoEmTransferencia({
        contaId, lancamentoId: lancamento.id, data: lancamento.data, tipo: lancamento.tipo, contaContrapartidaId,
        lancamentoContrapartidaId: par?.id, dataContrapartida: par?.data, criarMesmoAssim,
      })
      onConvertido()
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao converter em transferência.')
    } finally {
      setConvertendo(false)
    }
  }

  const semParComExtrato = info !== null && candidatos.length === 0 && info.extratoCobreData
  const semParSemExtrato = info !== null && candidatos.length === 0 && !info.extratoCobreData

  return (
    <Modal
      open
      title="🔁 Classificar como Transferência"
      onClose={() => { if (!convertendo) onClose() }}
      footer={
        <>
          <button className={classeBotaoSecundario} onClick={onClose} disabled={convertendo}>Cancelar</button>
          {semParComExtrato ? (
            <>
              <button className={classeBotaoSecundario} onClick={() => selecionarConta('')} disabled={convertendo}>Escolher outra conta</button>
              {onNaoETransferencia && (
                <button className={classeBotaoSecundario} onClick={onNaoETransferencia} disabled={convertendo}>
                  {ehSaida ? 'É saída para terceiro' : 'Veio de terceiro'}
                </button>
              )}
              <button className={classeBotaoPrimario} onClick={() => converter(true)} disabled={convertendo}>Criar mesmo assim</button>
            </>
          ) : (
            <button className={classeBotaoPrimario} onClick={() => converter(false)}
              disabled={convertendo || carregando || !info || (candidatos.length > 0 && !escolhidoId)}>
              {convertendo ? 'Classificando...' : candidatos.length > 0 ? 'Parear' : 'Confirmar'}
            </button>
          )}
        </>
      }
    >
      <p className="conc-dica" style={{ marginBottom: 12 }}>
        "{lancamento.descricao}" · {fmtBRL(lancamento.valor)} · {fmtDate(lancamento.data)}
        <br />
        {ehSaida
          ? 'Não é despesa: o dinheiro saiu desta conta e foi para outra. Pra qual conta foi?'
          : 'Não é receita: o dinheiro veio de outra conta sua. De qual conta veio?'}
      </p>
      <div className="inp-group">
        <label htmlFor="conta-contrapartida">{ehSaida ? 'Conta de destino' : 'Conta de origem'}</label>
        <select id="conta-contrapartida" value={contaContrapartidaId} onChange={e => selecionarConta(e.target.value)}>
          <option value="">Selecione...</option>
          {contas.filter(c => c.ativa && c.id !== contaId).map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
        </select>
      </div>

      {erro && <div className="cd-msg">{erro}</div>}
      {carregando && <p className="conc-dica">Procurando o lançamento na outra conta...</p>}

      {candidatos.length === 1 && (
        <p className="conc-ok">
          Vai parear com "{candidatos[0].descricao}" de {fmtBRL(Math.abs(candidatos[0].valor))} em {fmtDate(candidatos[0].data)} no {nomeConta} — nenhum lançamento novo.
        </p>
      )}
      {candidatos.length > 1 && (
        <fieldset className="conc-diag-secao">
          <legend>Há {candidatos.length} lançamentos parecidos no {nomeConta}. Qual é a outra ponta?</legend>
          {candidatos.map(c => (
            <label key={c.id} className="conc-diag-linha">
              <input type="radio" name="candidato" value={c.id} checked={escolhidoId === c.id} onChange={() => setEscolhidoId(c.id)} />
              {' '}{fmtDate(c.data)} · {c.descricao} · {fmtBRL(Math.abs(c.valor))}
            </label>
          ))}
        </fieldset>
      )}
      {semParComExtrato && (
        <p className="conc-alerta" role="alert">
          O extrato do {nomeConta} não tem esse {ehSaida ? 'recebimento' : 'envio'} em {fmtDate(lancamento.data)}. Tem certeza que foi {ehSaida ? 'para o' : 'do'} {nomeConta}?
        </p>
      )}
      {semParSemExtrato && (
        <p className="conc-dica">
          O extrato do {nomeConta} ainda não cobre {fmtDate(lancamento.data)}. Fica uma contrapartida provisória, trocada pelo lançamento real quando o extrato for importado.
        </p>
      )}
    </Modal>
  )
}
