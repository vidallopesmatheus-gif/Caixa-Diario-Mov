import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import Modal from '../shared/Modal'
import { diagnosticarSaldo } from '../../api/conciliacaoSaldo'
import { excluirLancamento, excluirLote } from '../../api/importacao'
import { ajustarSaldo } from '../../api/contasBancarias'
import { fmtBRL, fmtDate } from '../../utils/format'
import type { DiagnosticoDiferenca, LinhaDiagnostico } from '../../types'
import './Conciliacao.css'

interface Props {
  contaId: string
  data: string
  saldoBanco: number
  onClose: () => void
  /** Algo mudou no extrato (exclusão ou ajuste) — a tela recarrega saldo/extrato. */
  onAlterado: () => void
  onFiltrarPeriodo?: (de: string, ate: string) => void
}

const ehLinhaExcluivel = (sinais: LinhaDiagnostico[], l: LinhaDiagnostico) => !sinais.some(s => s.id === l.id)

/**
 * Money Vision: "Diferença de R$ 684,06 em 07/11. Possíveis causas: …" — sempre ANTES de qualquer
 * ajuste. O usuário corrige item a item; o ajuste só lança a diferença que sobrar no fim.
 */
export default function DiagnosticoDiferencaModal({ contaId, data, saldoBanco, onClose, onAlterado, onFiltrarPeriodo }: Props) {
  const [diagnostico, setDiagnostico] = useState<DiagnosticoDiferenca | null>(null)
  const [carregando, setCarregando] = useState(true)
  const [processando, setProcessando] = useState(false)
  const [erro, setErro] = useState('')

  const buscar = useCallback(() =>
    diagnosticarSaldo(contaId, data, saldoBanco)
      .then(setDiagnostico)
      .catch((e: unknown) => setErro(e instanceof Error ? e.message : 'Erro ao calcular o diagnóstico.'))
      .finally(() => setCarregando(false)),
  [contaId, data, saldoBanco])

  useEffect(() => { buscar() }, [buscar])

  async function carregar() {
    setCarregando(true)
    setErro('')
    await buscar()
  }

  async function executar(acao: () => Promise<unknown>) {
    setProcessando(true)
    setErro('')
    try {
      await acao()
      onAlterado()
      await carregar()
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao corrigir o lançamento.')
    } finally {
      setProcessando(false)
    }
  }

  async function lancarAjuste() {
    setProcessando(true)
    setErro('')
    try {
      await ajustarSaldo(contaId, { data, saldoReal: saldoBanco })
      onAlterado()
      onClose()
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao lançar o ajuste.')
      setProcessando(false)
    }
  }

  // Este modal sempre recebe o saldo do banco — a diferença vem calculada.
  const diferenca = diagnostico?.diferenca ?? 0
  const bate = diagnostico !== null && Math.abs(diferenca) <= 0.01
  // Contrapartida de transferência: excluir apagaria as DUAS pontas, inclusive a real — corrige na revisão.
  const contrapartidas = [...(diagnostico?.contrapartidasSemLancamento ?? []), ...(diagnostico?.contrapartidasDuplicadas ?? [])]
  const sinais = diagnostico?.sinalSuspeito ?? []
  const naoExcluiveis = [...sinais, ...contrapartidas]
  const ehContrapartida = (l: LinhaDiagnostico) => contrapartidas.some(c => c.id === l.id)

  function Linha({ l, rotuloAcao = 'Excluir' }: { l: LinhaDiagnostico; rotuloAcao?: string }) {
    return (
      <li className="conc-diag-linha">
        <span>{fmtDate(l.data)} · {l.descricao} · <strong className={l.valor >= 0 ? 'val-green' : 'val-red'}>{fmtBRL(l.valor)}</strong></span>
        {ehContrapartida(l) ? (
          <Link to="/banco/transferencias-sem-par" className="conc-link" onClick={onClose}>revisar em Transferências</Link>
        ) : ehLinhaExcluivel(naoExcluiveis, l) ? (
          <button type="button" className="conc-link" disabled={processando}
            onClick={() => executar(() => excluirLancamento(contaId, { id: l.id, data: l.data }))}>
            {rotuloAcao}
          </button>
        ) : (
          <span className="conc-dica">corrija no extrato (exclua e lance com o sinal certo)</span>
        )}
      </li>
    )
  }

  return (
    <Modal
      open
      title="⚖️ Diagnóstico da diferença"
      onClose={() => { if (!processando) onClose() }}
      footer={
        <>
          <button className="btn-cancel" onClick={onClose} disabled={processando}>Fechar</button>
          {diagnostico && !bate && (
            <button className="btn-confirm" onClick={lancarAjuste} disabled={processando}>
              Lançar ajuste da diferença restante ({fmtBRL(diferenca)})
            </button>
          )}
        </>
      }
    >
      {erro && <div className="cd-msg">{erro}</div>}
      {carregando && !diagnostico ? (
        <p className="conc-dica">Procurando as causas...</p>
      ) : diagnostico && (
        <>
          {bate ? (
            <p className="conc-ok">✅ Agora o saldo do app bate com o banco em {fmtDate(diagnostico.data)} ({fmtBRL(diagnostico.saldoBanco ?? saldoBanco)}).</p>
          ) : (
            <p>
              Diferença de <strong>{fmtBRL(Math.abs(diferenca))}</strong> em {fmtDate(diagnostico.data)}
              {' '}— banco {fmtBRL(diagnostico.saldoBanco ?? saldoBanco)}, app {fmtBRL(diagnostico.saldoApp)}.
              <br /><span className="conc-dica">Analisado desde {fmtDate(diagnostico.analisadoDesde)}. Possíveis causas:</span>
            </p>
          )}

          {!bate && diagnostico.combinacao && (
            <div className="conc-diag-destaque">
              <strong>💡 {diagnostico.combinacao.resumo}</strong>
              <ul>{diagnostico.combinacao.itens.map(l => <Linha key={l.id} l={l} />)}</ul>
              {diagnostico.combinacao.itens.every(l => ehLinhaExcluivel(naoExcluiveis, l)) && diagnostico.combinacao.itens.length > 1 && (
                <button type="button" className="btn-confirm" disabled={processando}
                  onClick={() => executar(() => excluirLote(contaId, diagnostico.combinacao!.itens.map(l => ({ id: l.id, data: l.data }))))}>
                  Excluir os {diagnostico.combinacao.itens.length}
                </button>
              )}
            </div>
          )}

          {diagnostico.lacunas.length > 0 && (
            <section className="conc-diag-secao">
              <h4>Dias sem extrato</h4>
              <ul>
                {diagnostico.lacunas.map(l => (
                  <li key={l.de} className="conc-diag-linha">
                    <span>Sem lançamentos de extrato de {fmtDate(l.de)} a {fmtDate(l.ate)}</span>
                    {onFiltrarPeriodo && <button type="button" className="conc-link" onClick={() => { onFiltrarPeriodo(l.de, l.ate); onClose() }}>ver período</button>}
                  </li>
                ))}
              </ul>
            </section>
          )}

          {diagnostico.duplicatas.length > 0 && (
            <section className="conc-diag-secao">
              <h4>Possíveis duplicatas</h4>
              {diagnostico.duplicatas.map(g => (
                <ul key={g.itens[0].id} className="conc-diag-grupo">
                  <li className="conc-diag-linha"><span>{fmtDate(g.itens[0].data)} · {g.itens[0].descricao} · {fmtBRL(g.itens[0].valor)} (fica)</span></li>
                  {g.itens.slice(1).map(l => <Linha key={l.id} l={l} rotuloAcao="Excluir duplicata" />)}
                </ul>
              ))}
            </section>
          )}

          {sinais.length > 0 && (
            <section className="conc-diag-secao">
              <h4>Sinal suspeito</h4>
              <ul>{sinais.map(l => <Linha key={l.id} l={l} />)}</ul>
            </section>
          )}

          {diagnostico.transferenciasSemPar.length > 0 && (
            <section className="conc-diag-secao">
              <h4>Transferências sem a outra ponta</h4>
              <ul>{diagnostico.transferenciasSemPar.map(l => <Linha key={l.id} l={l} />)}</ul>
            </section>
          )}

          {diagnostico.manuais.length > 0 && (
            <section className="conc-diag-secao">
              <h4>Lançados à mão (não vieram de extrato)</h4>
              <ul>{diagnostico.manuais.map(l => <Linha key={l.id} l={l} />)}</ul>
            </section>
          )}

          {diagnostico.contrapartidasDuplicadas.length > 0 && (
            <section className="conc-diag-secao">
              <h4>Transferência contada duas vezes</h4>
              <p className="conc-dica">O app criou a contrapartida e o lançamento real do extrato também está aqui, solto.</p>
              <ul>{diagnostico.contrapartidasDuplicadas.map(l => <Linha key={l.id} l={l} />)}</ul>
            </section>
          )}

          {diagnostico.contrapartidasSemLancamento.length > 0 && (
            <section className="conc-diag-secao">
              <h4>Transferências que o extrato não tem</h4>
              <p className="conc-dica">O app criou a contrapartida, mas o extrato desta conta cobre a data e não tem esse lançamento.</p>
              <ul>{diagnostico.contrapartidasSemLancamento.map(l => <Linha key={l.id} l={l} />)}</ul>
            </section>
          )}

          {diagnostico.ajustes.length > 0 && (
            <section className="conc-diag-secao">
              <h4>Ajustes de saldo no período</h4>
              <p className="conc-dica">
                Um ajuste esconde o que falta (mês sem extrato, transferência sem a outra ponta). Corrija as causas acima e desfaça o ajuste.
              </p>
              <ul>{diagnostico.ajustes.map(l => <Linha key={l.id} l={l} rotuloAcao="Desfazer ajuste" />)}</ul>
            </section>
          )}

          {!bate && !diagnostico.combinacao && diagnostico.lacunas.length === 0 && diagnostico.duplicatas.length === 0
            && sinais.length === 0 && diagnostico.transferenciasSemPar.length === 0 && diagnostico.manuais.length === 0
            && diagnostico.ajustes.length === 0 && contrapartidas.length === 0 && (
            <p className="conc-dica">Nenhuma causa provável encontrada no período — confira o saldo informado ou lance o ajuste.</p>
          )}
        </>
      )}
    </Modal>
  )
}
