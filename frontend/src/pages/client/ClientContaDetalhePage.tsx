import { useState, useEffect, useCallback, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import {
  listarContasBancarias,
  obterExtratoConta,
  obterPendenciasConta,
  registrarRendimento,
  vincularMeta,
  desvincularMeta,
} from '../../api/contasBancarias'
import { previewExtrato, importarExtrato } from '../../api/importacao'
import { listarMetas, salvarMeta } from '../../api/metas'
import { fmtBRL, fmtDate, todayISO, addDays } from '../../utils/format'
import Modal from '../../components/shared/Modal'
import type { ContaBancaria, LancamentoExtrato, ContaProvisionada, MetaAnual, PreviewTransacao, ResultadoImportacao } from '../../types'
import './ClientContas.css'
import './ClientContaDetalhe.css'
import './ClientContasBancarias.css'

type Aba = 'extrato' | 'recebiveis' | 'pagamentos'

const TIPO_LABEL: Record<string, string> = {
  Caixa: '💵 Caixa',
  ContaCorrente: '🏦 Conta Corrente',
  Investimento: '📈 Investimento',
}

export default function ClientContaDetalhePage() {
  const { contaId } = useParams<{ contaId: string }>()
  const { user } = useAuth()
  const navigate = useNavigate()
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const clienteId = user?.usuarioId ?? ''

  const [conta, setConta] = useState<ContaBancaria | null>(null)
  const [loadingConta, setLoadingConta] = useState(true)
  const [aba, setAba] = useState<Aba>('extrato')
  const [msg, setMsg] = useState('')

  const [de, setDe] = useState(addDays(todayISO(), -30))
  const [ate, setAte] = useState(todayISO())
  const [lancamentos, setLancamentos] = useState<LancamentoExtrato[]>([])
  const [loadingExtrato, setLoadingExtrato] = useState(true)

  const [recebiveis, setRecebiveis] = useState<ContaProvisionada[]>([])
  const [pagamentos, setPagamentos] = useState<ContaProvisionada[]>([])
  const [loadingPendencias, setLoadingPendencias] = useState(true)

  // ── Importação de extrato: preview + intervalo de datas + resolução de duplicatas ────
  const [arquivoImportar, setArquivoImportar] = useState<File | null>(null)
  const [previewTransacoes, setPreviewTransacoes] = useState<PreviewTransacao[]>([])
  const [previewCarregando, setPreviewCarregando] = useState(false)
  const [modalImportar, setModalImportar] = useState(false)
  const [dataInicioImport, setDataInicioImport] = useState('')
  const [dataFimImport, setDataFimImport] = useState('')
  const [forcarInclusao, setForcarInclusao] = useState<Set<number>>(new Set())
  const [importando, setImportando] = useState(false)
  const [resultadoImportacao, setResultadoImportacao] = useState<ResultadoImportacao | null>(null)

  // ── Investimento: rendimento e vínculo com meta ──────────────────────────
  const [modalRendimento, setModalRendimento] = useState(false)
  const [rendValorDisplay, setRendValorDisplay] = useState('')
  const [rendValor, setRendValor] = useState(0)
  const [rendPositivo, setRendPositivo] = useState(true)
  const [rendData, setRendData] = useState(todayISO())
  const [rendDescricao, setRendDescricao] = useState('')
  const [salvandoRendimento, setSalvandoRendimento] = useState(false)

  const [modalVincular, setModalVincular] = useState(false)
  const [metasDisponiveis, setMetasDisponiveis] = useState<MetaAnual[]>([])
  const [metaSelecionadaId, setMetaSelecionadaId] = useState('')
  const [novoAnoMeta, setNovoAnoMeta] = useState(new Date().getFullYear())
  const [novoValorSonho, setNovoValorSonho] = useState('')
  const [salvandoVinculo, setSalvandoVinculo] = useState(false)

  const carregarConta = useCallback(() => {
    if (!clienteId || !contaId) return
    setLoadingConta(true)
    listarContasBancarias(clienteId)
      .then(lista => setConta(lista.find(c => c.id === contaId) ?? null))
      .catch(() => setMsg('Erro ao carregar dados da conta.'))
      .finally(() => setLoadingConta(false))
  }, [clienteId, contaId])

  useEffect(() => { carregarConta() }, [carregarConta])

  const carregarExtrato = useCallback(async () => {
    if (!contaId) return
    setLoadingExtrato(true)
    try {
      setLancamentos(await obterExtratoConta(contaId, de, ate))
    } catch {
      setMsg('Erro ao carregar extrato.')
    } finally {
      setLoadingExtrato(false)
    }
  }, [contaId, de, ate])

  useEffect(() => { carregarExtrato() }, [carregarExtrato])

  useEffect(() => {
    if (!contaId) return
    setLoadingPendencias(true)
    obterPendenciasConta(contaId)
      .then(p => { setRecebiveis(p.recebiveis); setPagamentos(p.pagamentos) })
      .catch(() => setMsg('Erro ao carregar pendências.'))
      .finally(() => setLoadingPendencias(false))
  }, [contaId])

  async function handleArquivoSelecionado(arquivo: File) {
    if (!contaId) return
    setMsg('')
    setPreviewCarregando(true)
    setArquivoImportar(arquivo)
    setResultadoImportacao(null)
    setForcarInclusao(new Set())
    try {
      const transacoes = await previewExtrato(contaId, arquivo)
      setPreviewTransacoes(transacoes)
      const datas = transacoes.map(t => t.data).sort()
      setDataInicioImport(datas[0] ?? todayISO())
      setDataFimImport(datas[datas.length - 1] ?? todayISO())
      setModalImportar(true)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao ler o arquivo.')
    } finally {
      setPreviewCarregando(false)
    }
  }

  const transacoesNoIntervalo = previewTransacoes.filter(
    t => t.data >= dataInicioImport && t.data <= dataFimImport
  )
  const jaImportadasNoIntervalo = transacoesNoIntervalo.filter(t => t.jaImportada)
  const novasNoIntervalo = transacoesNoIntervalo.filter(t => !t.jaImportada || forcarInclusao.has(t.indice))

  function toggleForcarInclusao(indice: number) {
    setForcarInclusao(prev => {
      const next = new Set(prev)
      if (next.has(indice)) next.delete(indice)
      else next.add(indice)
      return next
    })
  }

  async function handleConfirmarImportacao() {
    if (!contaId || !arquivoImportar) return
    setImportando(true)
    setMsg('')
    try {
      const resultado = await importarExtrato(contaId, arquivoImportar, {
        dataInicio: dataInicioImport,
        dataFim: dataFimImport,
        indicesForcarInclusao: Array.from(forcarInclusao),
      })
      setResultadoImportacao(resultado)
      setModalImportar(false)
      setArquivoImportar(null)
      carregarConta()
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao importar extrato.')
    } finally {
      setImportando(false)
    }
  }

  function abrirModalRendimento() {
    setRendValorDisplay(''); setRendValor(0); setRendPositivo(true); setRendData(todayISO()); setRendDescricao('')
    setModalRendimento(true)
  }

  async function handleSalvarRendimento() {
    if (!contaId || rendValor <= 0) return
    setSalvandoRendimento(true)
    try {
      await registrarRendimento(contaId, {
        data: rendData,
        valor: rendPositivo ? rendValor : -rendValor,
        descricao: rendDescricao || undefined,
      })
      setModalRendimento(false)
      carregarConta()
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao registrar rendimento.')
    } finally {
      setSalvandoRendimento(false)
    }
  }

  function abrirModalVincular() {
    setMetaSelecionadaId(''); setNovoAnoMeta(new Date().getFullYear()); setNovoValorSonho('')
    if (clienteId) listarMetas(clienteId).then(setMetasDisponiveis).catch(() => {})
    setModalVincular(true)
  }

  async function handleVincularExistente() {
    if (!contaId || !metaSelecionadaId) return
    setSalvandoVinculo(true)
    try {
      await vincularMeta(contaId, metaSelecionadaId)
      setModalVincular(false)
      carregarConta()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao vincular meta.')
    } finally {
      setSalvandoVinculo(false)
    }
  }

  async function handleCriarEVincular() {
    if (!contaId || !clienteId) return
    const valorSonho = parseFloat(novoValorSonho.replace(/\./g, '').replace(',', '.')) || 0
    if (valorSonho <= 0) return
    setSalvandoVinculo(true)
    try {
      const novaMeta = await salvarMeta({
        clienteId, ano: novoAnoMeta, metaReceita: 0, metaLucro: 0, mesInicio: 1, periodoMeses: 12,
        modoMeta: 'metodo', valorSonho,
      })
      await vincularMeta(contaId, novaMeta.id)
      setModalVincular(false)
      carregarConta()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao criar/vincular meta.')
    } finally {
      setSalvandoVinculo(false)
    }
  }

  async function handleDesvincular(metaId: string) {
    if (!contaId) return
    if (!confirm('Desvincular esta meta da conta? O progresso volta a ser o valor manual anterior.')) return
    try {
      await desvincularMeta(contaId, metaId)
      carregarConta()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao desvincular meta.')
    }
  }

  const renderPendencia = (item: ContaProvisionada, tipo: 'receber' | 'pagar') => (
    <div key={`${item.descricao}-${item.dataVencimento}-${item.valor}`} className="conta-item">
      <div className="conta-info">
        <div className="conta-desc">{item.descricao}</div>
        <div className="conta-meta">
          {item.dataVencimento ? `Vence: ${fmtDate(item.dataVencimento)}` : 'Sem vencimento'}
          {item.categoria ? ` · ${item.categoria}` : ''}
        </div>
      </div>
      <div className={`conta-valor ${tipo}`}>{fmtBRL(item.valor)}</div>
    </div>
  )

  if (loadingConta) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  if (!conta) {
    return (
      <div className="cd-vazio">
        <p>Conta bancária não encontrada.</p>
        <button className="cd-btn-voltar" onClick={() => navigate('/banco')}>← Voltar</button>
      </div>
    )
  }

  return (
    <>
      <div className="cd-header">
        <div>
          <button className="cd-btn-voltar" onClick={() => navigate('/banco')}>← Voltar</button>
          <h2 className="cd-titulo">{conta.nome}</h2>
          <div className="cd-subtitulo">{TIPO_LABEL[conta.tipo] ?? conta.tipo}</div>
        </div>
        <div className="cd-saldo-box">
          <span className="cd-saldo-label">Saldo atual</span>
          <span className="cd-saldo-val val-green">{fmtBRL(conta.saldoAtual)}</span>
        </div>
      </div>

      {conta.pendentesCategorizacao > 0 && (
        <div className="cd-msg" style={{ cursor: 'pointer' }} onClick={() => navigate(`/banco/extrato/${contaId}`)}>
          🏷️ {conta.pendentesCategorizacao} lançamento(s) aguardando categoria — clique para categorizar
        </div>
      )}

      <div className="cd-acoes">
        <input
          ref={fileInputRef}
          type="file"
          accept=".ofx,.csv,.xlsx"
          style={{ display: 'none' }}
          onChange={e => {
            const f = e.target.files?.[0]
            if (f) handleArquivoSelecionado(f)
            e.target.value = ''
          }}
        />
        <button className="cd-btn-importar" onClick={() => fileInputRef.current?.click()} disabled={previewCarregando}>
          {previewCarregando ? 'Lendo arquivo...' : '⬆️ Importar extrato'}
        </button>
        {conta.tipo === 'Investimento' && (
          <button className="cd-btn-importar" onClick={abrirModalRendimento}>
            📈 Registrar rendimento
          </button>
        )}
      </div>

      {resultadoImportacao && (
        <div className="cd-msg">
          ✅ {resultadoImportacao.totalImportadas} lançamento(s) importado(s)
          {resultadoImportacao.totalPendentesCategorizacao > 0 && (
            <> — {resultadoImportacao.totalPendentesCategorizacao} sem categoria sugerida (
              <button
                type="button"
                onClick={() => navigate(`/banco/extrato/${contaId}`)}
                style={{ background: 'none', border: 'none', color: 'var(--accent)', cursor: 'pointer', padding: 0, font: 'inherit', textDecoration: 'underline' }}
              >
                categorizar agora
              </button>
              )
            </>
          )}
        </div>
      )}

      {conta.tipo === 'Investimento' && (
        <div className="stats-grid" style={{ marginBottom: 16 }}>
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Total aportado</span>
            <span className="cb-resumo-val">{fmtBRL(conta.totalAportado ?? 0)}</span>
          </div>
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Rendimento acumulado</span>
            <span className={`cb-resumo-val ${(conta.rendimentoAcumulado ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
              {fmtBRL(conta.rendimentoAcumulado ?? 0)}
            </span>
          </div>
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Saldo atual</span>
            <span className="cb-resumo-val val-green">{fmtBRL(conta.saldoAtual)}</span>
          </div>
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Rentabilidade no período</span>
            <span className={`cb-resumo-val ${(conta.rentabilidadePercentual ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
              {conta.rentabilidadePercentual != null ? `${conta.rentabilidadePercentual.toFixed(2)}%` : '—'}
            </span>
          </div>
        </div>
      )}

      {conta.tipo === 'Investimento' && (
        <div className="contas-section">
          <h3>🎯 Meta(s) vinculada(s)</h3>
          {conta.metasVinculadas && conta.metasVinculadas.length > 0 ? (
            <>
              {conta.progressoCombinadoPercentual !== null && conta.progressoCombinadoPercentual !== undefined && (
                <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 8 }}>
                  Progresso combinado: <strong style={{ color: 'var(--tx1)' }}>{conta.progressoCombinadoPercentual.toFixed(1)}%</strong> do saldo desta conta em relação à soma das metas vinculadas.
                </p>
              )}
              {conta.metasVinculadas.map(m => (
                <div key={m.id} className="conta-item">
                  <div className="conta-info">
                    <div className="conta-desc">{m.sonho || `Meta ${m.ano}`}</div>
                    <div className="conta-meta">Ano {m.ano} · Alvo: {fmtBRL(m.valorSonho)}</div>
                  </div>
                  <button className="cb-btn-inativar" onClick={() => handleDesvincular(m.id)}>Desvincular</button>
                </div>
              ))}
            </>
          ) : (
            <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma meta vinculada ainda.</p>
          )}
          <button className="btn-add-conta" style={{ marginTop: 8 }} onClick={abrirModalVincular}>＋ Vincular meta</button>
        </div>
      )}

      {msg && <div className="cd-msg">{msg}</div>}

      <div className="cd-tabs">
        <button className={`cd-tab ${aba === 'extrato' ? 'active' : ''}`} onClick={() => setAba('extrato')}>
          Extrato
        </button>
        <button className={`cd-tab ${aba === 'recebiveis' ? 'active' : ''}`} onClick={() => setAba('recebiveis')}>
          Recebíveis {recebiveis.length > 0 && `(${recebiveis.length})`}
        </button>
        <button className={`cd-tab ${aba === 'pagamentos' ? 'active' : ''}`} onClick={() => setAba('pagamentos')}>
          Pagamentos {pagamentos.length > 0 && `(${pagamentos.length})`}
        </button>
      </div>

      {aba === 'extrato' && (
        <>
          <div className="cd-filtro-periodo">
            <label>De <input type="date" value={de} onChange={e => setDe(e.target.value)} /></label>
            <label>Até <input type="date" value={ate} onChange={e => setAte(e.target.value)} /></label>
          </div>

          {loadingExtrato ? (
            <p style={{ color: 'var(--tx3)' }}>Carregando extrato...</p>
          ) : lancamentos.length === 0 ? (
            <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum lançamento no período selecionado.</p>
          ) : (
            <div className="cd-extrato-lista">
              <div className="cd-extrato-cabecalho">
                <span>Data</span>
                <span>Descrição</span>
                <span>Categoria</span>
                <span className="cd-col-valor">Valor</span>
                <span className="cd-col-valor">Saldo</span>
              </div>
              {lancamentos.map((l, i) => (
                <div key={`${l.data}-${i}`} className="cd-extrato-linha">
                  <span>{fmtDate(l.data)}</span>
                  <span>{l.descricao}</span>
                  <span className="cd-categoria">
                    {l.pendenteCategorizacao ? (
                      <span style={{ color: 'var(--warning)' }} title="Sem categoria — afeta o saldo normalmente, só falta classificar">
                        🏷️ Pendente
                      </span>
                    ) : (l.categoria || '—')}
                  </span>
                  <span className={`cd-col-valor ${l.valor >= 0 ? 'val-green' : 'val-red'}`}>
                    {l.valor >= 0 ? '+' : ''}{fmtBRL(l.valor)}
                  </span>
                  <span className="cd-col-valor">{fmtBRL(l.saldoAcumulado)}</span>
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {aba === 'recebiveis' && (
        loadingPendencias ? <p style={{ color: 'var(--tx3)' }}>Carregando...</p> :
        recebiveis.length === 0 ? <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum recebível pendente vinculado a esta conta.</p> :
        <div className="contas-section">{recebiveis.map(r => renderPendencia(r, 'receber'))}</div>
      )}

      {aba === 'pagamentos' && (
        loadingPendencias ? <p style={{ color: 'var(--tx3)' }}>Carregando...</p> :
        pagamentos.length === 0 ? <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum pagamento pendente vinculado a esta conta.</p> :
        <div className="contas-section">{pagamentos.map(p => renderPendencia(p, 'pagar'))}</div>
      )}

      <Modal
        open={modalRendimento}
        title="📈 Registrar rendimento"
        onClose={() => setModalRendimento(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setModalRendimento(false)}>Cancelar</button>
            <button className="btn-confirm" onClick={handleSalvarRendimento} disabled={rendValor <= 0 || salvandoRendimento}>
              {salvandoRendimento ? 'Salvando...' : 'Registrar'}
            </button>
          </>
        }
      >
        <div style={{ marginBottom: 12, display: 'flex', gap: 8 }}>
          <button
            type="button"
            onClick={() => setRendPositivo(true)}
            style={{
              padding: '6px 14px', borderRadius: 8, cursor: 'pointer', fontSize: 13,
              border: rendPositivo ? '1px solid var(--accent)' : '1px solid var(--bd)',
              background: rendPositivo ? 'var(--accent)' : 'var(--bg-card)',
              color: rendPositivo ? '#fff' : 'var(--tx1)',
            }}>
            Ganho
          </button>
          <button
            type="button"
            onClick={() => setRendPositivo(false)}
            style={{
              padding: '6px 14px', borderRadius: 8, cursor: 'pointer', fontSize: 13,
              border: !rendPositivo ? '1px solid var(--accent)' : '1px solid var(--bd)',
              background: !rendPositivo ? 'var(--accent)' : 'var(--bg-card)',
              color: !rendPositivo ? '#fff' : 'var(--tx1)',
            }}>
            Perda
          </button>
        </div>
        <div className="inp-group">
          <label>Valor</label>
          <div className="val-input-wrap">
            <span className="val-prefix">R$</span>
            <input
              type="text" inputMode="decimal" placeholder="0,00"
              value={rendValorDisplay}
              onChange={e => {
                const raw = e.target.value.replace(/[^\d,]/g, '')
                setRendValorDisplay(raw)
                setRendValor(parseFloat(raw.replace(',', '.')) || 0)
              }}
            />
          </div>
        </div>
        <div className="inp-group">
          <label>Data</label>
          <input type="date" value={rendData} max={todayISO()} onChange={e => setRendData(e.target.value)} />
        </div>
        <div className="inp-group">
          <label>Descrição (opcional)</label>
          <input value={rendDescricao} onChange={e => setRendDescricao(e.target.value)} placeholder="Ex: Rendimento do mês" />
        </div>
      </Modal>

      <Modal
        open={modalVincular}
        title="🎯 Vincular meta"
        onClose={() => setModalVincular(false)}
      >
        <div className="inp-group">
          <label>Meta existente</label>
          <select value={metaSelecionadaId} onChange={e => setMetaSelecionadaId(e.target.value)}>
            <option value="">Selecione...</option>
            {metasDisponiveis.filter(m => m.contaInvestimentoId !== conta?.id).map(m => (
              <option key={m.id} value={m.id}>{m.sonho || `Meta ${m.ano}`} — {fmtBRL(m.valorSonho)}</option>
            ))}
          </select>
          <button className="btn-add-conta" style={{ marginTop: 8 }} onClick={handleVincularExistente} disabled={!metaSelecionadaId || salvandoVinculo}>
            {salvandoVinculo ? 'Vinculando...' : 'Vincular esta meta'}
          </button>
        </div>

        <div style={{ borderTop: '1px solid var(--bd)', margin: '16px 0', paddingTop: 16 }}>
          <h4 style={{ marginBottom: 8 }}>Ou criar uma meta nova</h4>
          <div className="inp-group">
            <label>Ano</label>
            <input type="number" value={novoAnoMeta} onChange={e => setNovoAnoMeta(+e.target.value)} />
          </div>
          <div className="inp-group">
            <label>Valor alvo</label>
            <div className="val-input-wrap">
              <span className="val-prefix">R$</span>
              <input type="text" inputMode="decimal" placeholder="0,00" value={novoValorSonho}
                onChange={e => setNovoValorSonho(e.target.value.replace(/[^\d,]/g, ''))} />
            </div>
          </div>
          <button className="btn-add-conta" onClick={handleCriarEVincular} disabled={!novoValorSonho || salvandoVinculo}>
            {salvandoVinculo ? 'Criando...' : '＋ Criar e vincular'}
          </button>
        </div>
      </Modal>

      <Modal
        open={modalImportar}
        title="⬆️ Importar extrato"
        onClose={() => setModalImportar(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setModalImportar(false)}>Cancelar</button>
            <button className="btn-confirm" onClick={handleConfirmarImportacao} disabled={importando || novasNoIntervalo.length === 0}>
              {importando ? 'Importando...' : `Importar ${novasNoIntervalo.length} transação(ões)`}
            </button>
          </>
        }
      >
        <div className="inp-group">
          <label>Importar do dia</label>
          <input type="date" value={dataInicioImport} onChange={e => setDataInicioImport(e.target.value)} />
        </div>
        <div className="inp-group">
          <label>Até o dia</label>
          <input type="date" value={dataFimImport} onChange={e => setDataFimImport(e.target.value)} />
        </div>

        <p style={{ fontSize: 13, color: 'var(--tx3)', margin: '12px 0' }}>
          {transacoesNoIntervalo.length} transação(ões) encontrada(s) no intervalo selecionado
          {jaImportadasNoIntervalo.length > 0 && ` — ${jaImportadasNoIntervalo.length} já importada(s) antes`}.
          {' '}<strong style={{ color: 'var(--tx1)' }}>{novasNoIntervalo.length}</strong> serão importadas agora.
        </p>

        {jaImportadasNoIntervalo.length > 0 && (
          <div style={{ maxHeight: 220, overflowY: 'auto', border: '1px solid var(--bd)', borderRadius: 8, padding: 8 }}>
            <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--tx3)', textTransform: 'uppercase', marginBottom: 6 }}>
              Já importadas antes — desmarcadas por padrão
            </div>
            {jaImportadasNoIntervalo.map(t => (
              <label key={t.indice} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 0', fontSize: 13 }}>
                <input
                  type="checkbox"
                  checked={forcarInclusao.has(t.indice)}
                  onChange={() => toggleForcarInclusao(t.indice)}
                />
                <span style={{ color: 'var(--tx3)', minWidth: 70 }}>{fmtDate(t.data)}</span>
                <span style={{ flex: 1 }}>{t.descricao}</span>
                <span className={t.tipo === 'Entrada' ? 'val-green' : 'val-red'}>
                  {t.tipo === 'Entrada' ? '+' : '-'}{fmtBRL(t.valor)}
                </span>
              </label>
            ))}
            <p style={{ fontSize: 11, color: 'var(--tx3)', marginTop: 6 }}>
              Marque só se tiver certeza de que é uma transação diferente (ex: duas compras idênticas no mesmo dia).
            </p>
          </div>
        )}
      </Modal>
    </>
  )
}
