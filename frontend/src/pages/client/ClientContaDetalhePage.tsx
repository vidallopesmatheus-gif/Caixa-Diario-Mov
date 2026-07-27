import { useState, useEffect, useCallback, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import {
  listarContasBancarias,
  obterExtratoConta,
  obterPendenciasConta,
} from '../../api/contasBancarias'
import { importarExtrato } from '../../api/importacao'
import { fmtBRL, fmtDate, todayISO, addDays } from '../../utils/format'
import type { ContaBancaria, LancamentoExtrato, ContaProvisionada } from '../../types'
import './ClientContas.css'
import './ClientContaDetalhe.css'

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

  useEffect(() => {
    if (!clienteId || !contaId) return
    setLoadingConta(true)
    listarContasBancarias(clienteId)
      .then(lista => setConta(lista.find(c => c.id === contaId) ?? null))
      .catch(() => setMsg('Erro ao carregar dados da conta.'))
      .finally(() => setLoadingConta(false))
  }, [clienteId, contaId])

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

  async function handleImportar(arquivo: File) {
    if (!contaId) return
    try {
      await importarExtrato(contaId, arquivo)
      navigate(`/extrato/${contaId}`)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao importar extrato.')
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
        <button className="cd-btn-voltar" onClick={() => navigate('/contas-bancarias')}>← Voltar</button>
      </div>
    )
  }

  return (
    <>
      <div className="cd-header">
        <div>
          <button className="cd-btn-voltar" onClick={() => navigate('/contas-bancarias')}>← Voltar</button>
          <h2 className="cd-titulo">{conta.nome}</h2>
          <div className="cd-subtitulo">{TIPO_LABEL[conta.tipo] ?? conta.tipo}</div>
        </div>
        <div className="cd-saldo-box">
          <span className="cd-saldo-label">Saldo atual</span>
          <span className="cd-saldo-val val-green">{fmtBRL(conta.saldoAtual)}</span>
        </div>
      </div>

      <div className="cd-acoes">
        <input
          ref={fileInputRef}
          type="file"
          accept=".ofx,.csv,.xlsx"
          style={{ display: 'none' }}
          onChange={e => {
            const f = e.target.files?.[0]
            if (f) handleImportar(f)
            e.target.value = ''
          }}
        />
        <button className="cd-btn-importar" onClick={() => fileInputRef.current?.click()}>
          ⬆️ Importar extrato
        </button>
      </div>

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
                  <span className="cd-categoria">{l.categoria || '—'}</span>
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
    </>
  )
}
