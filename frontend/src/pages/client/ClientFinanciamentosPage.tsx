import { useState, useEffect, useCallback } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { fmtBRL, fmtDate, fmtPct, todayISO } from '../../utils/format'
import {
  listarFinanciamentos, criarFinanciamento, listarParcelasFinanciamento, baixarParcelaFinanciamento,
  adiantarFinanciamento, excluirFinanciamento, type AdiantamentoResultado,
} from '../../api/financiamentos'
import { listarContasBancarias } from '../../api/contasBancarias'
import Modal from '../../components/shared/Modal'
import type { Financiamento, ParcelaFinanciamento, ContaBancaria } from '../../types'
import './ClientFinanciamentosPage.css'

interface Props { clienteIdOverride?: string }

function fmtNum(n: number) {
  if (!n) return ''
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  const limpo = s.replace(/[^\d,.-]/g, '').replace(/\./g, '').replace(',', '.')
  const n = parseFloat(limpo)
  return Number.isFinite(n) ? n : 0
}

const TIPO_LABEL: Record<Financiamento['tipo'], string> = {
  Financiamento: 'Financiamento', Emprestimo: 'Empréstimo', Consorcio: 'Consórcio', Parcelamento: 'Parcelamento',
}

// Item B1: "Taxa: 1,67% a.m. (22,0% a.a.)" — taxa anual equivalente (juros compostos), não a
// taxa mensal × 12 (isso subestimaria o custo real do contrato).
function taxaAnualDe(taxaMensal: number): number {
  return (Math.pow(1 + taxaMensal, 12) - 1) * 100
}

export interface PrefillFinanciamento {
  descricao: string
  valorBem: number
  contaBancariaId: string
  origemMetaId: string
}

export default function ClientFinanciamentosPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const location = useLocation()
  const navigate = useNavigate()

  const [financiamentos, setFinanciamentos] = useState<Financiamento[]>([])
  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')
  const [expandidoId, setExpandidoId] = useState<string | null>(null)
  const [parcelas, setParcelas] = useState<ParcelaFinanciamento[]>([])
  const [novoOpen, setNovoOpen] = useState(false)
  const [prefill, setPrefill] = useState<PrefillFinanciamento | null>(null)
  const [adiantarAlvo, setAdiantarAlvo] = useState<Financiamento | null>(null)
  const [excluirAlvo, setExcluirAlvo] = useState<Financiamento | null>(null)

  // Fase 5G: "🏆 Conquistei" com forma "Financiado" manda o usuário pra aqui já com o cadastro
  // aberto e pré-preenchido (sonho = descrição, valorSonho = valor do bem) — limpa o state da
  // navegação em seguida pra não reabrir de novo se o usuário atualizar a página ou voltar.
  useEffect(() => {
    const novo = (location.state as { novoFinanciamento?: PrefillFinanciamento } | null)?.novoFinanciamento
    if (novo) {
      setPrefill(novo)
      setNovoOpen(true)
      navigate(location.pathname, { replace: true, state: null })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true)
    try {
      const [fs, cs] = await Promise.all([listarFinanciamentos(clienteId), listarContasBancarias(clienteId)])
      setFinanciamentos(fs)
      setContas(cs.filter(c => c.ativa))
    } finally {
      setLoading(false)
    }
  }, [clienteId])

  useEffect(() => { carregar() }, [carregar])

  const toggleExpandir = async (f: Financiamento) => {
    if (expandidoId === f.id) { setExpandidoId(null); return }
    const lista = await listarParcelasFinanciamento(f.id)
    setParcelas(lista)
    setExpandidoId(f.id)
  }

  const handleBaixar = async (financiamentoId: string, parcelaId: string) => {
    setMsg('')
    try {
      await baixarParcelaFinanciamento(financiamentoId, { parcelaId, dataPagamento: todayISO() })
      const lista = await listarParcelasFinanciamento(financiamentoId)
      setParcelas(lista)
      await carregar()
    } catch (e) {
      setMsg(e instanceof Error ? e.message : 'Erro ao baixar parcela.')
    }
  }

  const handleExcluir = async (f: Financiamento, removerAdiantamentos: boolean) => {
    setMsg('')
    try {
      await excluirFinanciamento(f.id, removerAdiantamentos)
      setExcluirAlvo(null)
      await carregar()
    } catch (e) {
      setMsg(e instanceof Error ? e.message : 'Erro ao excluir.')
    }
  }

  if (!clienteId) return null

  return (
    <div className="financiamentos-page">
      {msg && <div className="financiamentos-msg">{msg}</div>}

      <div className="financiamentos-header">
        <button className="btn-primary" onClick={() => setNovoOpen(true)}>+ Novo financiamento</button>
      </div>

      {loading && <p>Carregando...</p>}
      {!loading && financiamentos.length === 0 && (
        <p className="financiamentos-vazio">Nenhum financiamento, empréstimo, consórcio ou parcelamento cadastrado.</p>
      )}

      <div className="financiamentos-lista">
        {financiamentos.map(f => (
          <div key={f.id} className={`financiamento-card ${f.status === 'Quitado' ? 'quitado' : ''}`}>
            <div className="financiamento-card-topo">
              <div>
                <strong>{f.descricao}</strong>
                <span className="financiamento-tipo"> · {TIPO_LABEL[f.tipo]} · {f.contaBancariaNome}</span>
                {f.status === 'Quitado' && <span className="financiamento-badge-quitado">Quitado</span>}
              </div>
              <div className="financiamento-card-acoes">
                {f.status === 'Ativo' && (
                  <button className="btn-fin-secundario" onClick={() => setAdiantarAlvo(f)}>Adiantar</button>
                )}
                <button className="btn-fin-secundario" onClick={() => toggleExpandir(f)}>
                  {expandidoId === f.id ? 'Ocultar parcelas' : 'Ver parcelas'}
                </button>
                {f.parcelasPagas === 0 && (
                  <button className="btn-fin-danger" onClick={() => setExcluirAlvo(f)}>Excluir</button>
                )}
              </div>
            </div>

            {f.taxaJurosMensal != null && f.taxaJurosMensal > 0 && (
              <span className="financiamento-taxa">
                Taxa: {fmtPct(f.taxaJurosMensal * 100, 2)} a.m. ({fmtPct(taxaAnualDe(f.taxaJurosMensal), 1)} a.a.)
              </span>
            )}

            <div className="financiamento-barras" title="Dívida quitada compara o valor em dinheiro já amortizado; Parcelas pagas é só a contagem — podem divergir por juros e adiantamentos.">
              <div className="financiamento-barra-linha">
                <span>Dívida quitada</span>
                <div className="financiamento-barra"><div className="financiamento-barra-fill" style={{ width: `${Math.min(100, f.percentualDividaQuitada)}%` }} /></div>
                <span>{fmtPct(f.percentualDividaQuitada)}</span>
              </div>
              <div className="financiamento-barra-linha">
                <span>Parcelas pagas</span>
                <div className="financiamento-barra"><div className="financiamento-barra-fill alt" style={{ width: `${Math.min(100, f.percentualParcelasPagas)}%` }} /></div>
                <span>{f.parcelasPagas}/{f.totalParcelasAtual} ({fmtPct(f.percentualParcelasPagas)})</span>
              </div>
            </div>

            <div className="financiamento-stats">
              <div><span>Saldo devedor</span><strong>{fmtBRL(f.saldoDevedor)}</strong></div>
              <div><span>Juros pagos</span><strong>{fmtBRL(f.jurosPagos)}</strong></div>
              <div><span>Juros a pagar</span><strong>{fmtBRL(f.jurosAPagar)}</strong></div>
              <div><span>Juros totais</span><strong>{fmtBRL(f.jurosTotais)}</strong></div>
              {f.status === 'Ativo' && f.proximaParcelaValor != null && (
                <div><span>Próxima parcela</span><strong>{fmtBRL(f.proximaParcelaValor)} em {fmtDate(f.proximaParcelaVencimento!)}</strong></div>
              )}
              {f.status === 'Ativo' && f.dataPrevistaQuitacao && (
                <div><span>Previsão de quitação</span><strong>{fmtDate(f.dataPrevistaQuitacao)}</strong></div>
              )}
              {f.valorAdiantado > 0 && (
                <div><span>Adiantado</span><strong>{fmtBRL(f.valorAdiantado)} (economia de {fmtBRL(f.economiaJurosAcumulada)})</strong></div>
              )}
            </div>

            {expandidoId === f.id && (
              <table className="financiamento-tabela">
                <thead>
                  <tr><th>#</th><th>Vencimento</th><th>Valor</th><th>Juros</th><th>Amortização</th><th>Saldo devedor</th><th></th></tr>
                </thead>
                <tbody>
                  {parcelas.map(p => p.tipo === 'Adiantamento' ? (
                    <tr key={p.id} className="parcela-adiantamento">
                      <td colSpan={2}>💰 Adiantamento em {fmtDate(p.vencimento)}</td>
                      <td>{fmtBRL(p.valor)}</td>
                      <td>—</td>
                      <td>{fmtBRL(p.amortizacao)}</td>
                      <td>{fmtBRL(p.saldoDevedor)}</td>
                      <td><span className="parcela-paga-selo">aplicado</span></td>
                    </tr>
                  ) : (
                    <tr key={p.id} className={p.pago ? 'parcela-paga' : p.atrasada ? 'parcela-atrasada' : ''}>
                      <td>{p.numero}</td>
                      <td>{fmtDate(p.vencimento)}</td>
                      <td>{fmtBRL(p.valor)}</td>
                      <td>{fmtBRL(p.juros)}</td>
                      <td>{fmtBRL(p.amortizacao)}</td>
                      <td>{fmtBRL(p.saldoDevedor)}</td>
                      <td>
                        {!p.pago && <button className="financiamento-link-btn" onClick={() => handleBaixar(f.id, p.id)}>Baixar</button>}
                        {p.pago && <span className="parcela-paga-selo">pago {p.dataBaixa ? fmtDate(p.dataBaixa) : ''}</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        ))}
      </div>

      {novoOpen && (
        <NovoFinanciamentoModal
          clienteId={clienteId}
          contas={contas}
          prefill={prefill}
          onClose={() => { setNovoOpen(false); setPrefill(null) }}
          onCriado={async () => { setNovoOpen(false); setPrefill(null); await carregar() }}
        />
      )}

      {adiantarAlvo && (
        <AdiantarModal
          financiamento={adiantarAlvo}
          onClose={() => setAdiantarAlvo(null)}
          onAplicado={async () => { setAdiantarAlvo(null); await carregar() }}
        />
      )}

      {excluirAlvo && (
        <ExcluirFinanciamentoModal
          financiamento={excluirAlvo}
          onClose={() => setExcluirAlvo(null)}
          onConfirmar={removerAdiantamentos => handleExcluir(excluirAlvo, removerAdiantamentos)}
        />
      )}
    </div>
  )
}

function ExcluirFinanciamentoModal({ financiamento, onClose, onConfirmar }: {
  financiamento: Financiamento
  onClose: () => void
  onConfirmar: (removerAdiantamentos: boolean) => void
}) {
  const [removerAdiantamentos, setRemoverAdiantamentos] = useState(true)
  const temAdiantamento = financiamento.valorAdiantado > 0

  return (
    <Modal open title={`Excluir — ${financiamento.descricao}`} onClose={onClose} footer={
      <>
        <button className="btn-cancel" onClick={onClose}>Cancelar</button>
        <button className="btn-fin-danger" onClick={() => onConfirmar(removerAdiantamentos)}>Excluir</button>
      </>
    }>
      <p>Excluir "{financiamento.descricao}"? Isso só é possível porque nenhuma parcela foi paga.</p>
      {temAdiantamento && (
        <label className="financiamentos-checkbox">
          <input
            type="checkbox"
            checked={removerAdiantamentos}
            onChange={e => setRemoverAdiantamentos(e.target.checked)}
          />
          Remover também os adiantamentos lançados no caixa ({fmtBRL(financiamento.valorAdiantado)}) e devolver o saldo da conta
        </label>
      )}
    </Modal>
  )
}

function NovoFinanciamentoModal({ clienteId, contas, prefill, onClose, onCriado }: {
  clienteId: string
  contas: ContaBancaria[]
  prefill: PrefillFinanciamento | null
  onClose: () => void
  onCriado: () => void
}) {
  const [descricao, setDescricao] = useState(prefill?.descricao ?? '')
  const [tipo, setTipo] = useState<Financiamento['tipo']>('Financiamento')
  const [valorBem, setValorBem] = useState(prefill ? fmtNum(prefill.valorBem) : '')
  const [valorEntrada, setValorEntrada] = useState('')
  const [numeroParcelas, setNumeroParcelas] = useState('')
  const [valorParcela, setValorParcela] = useState('')
  const [taxaJurosMensal, setTaxaJurosMensal] = useState('')
  const [sistema, setSistema] = useState<Financiamento['sistema']>('Price')
  const [dataPrimeiraParcela, setDataPrimeiraParcela] = useState(todayISO())
  const [contaBancariaId, setContaBancariaId] = useState(prefill?.contaBancariaId ?? contas[0]?.id ?? '')
  const [bemVinculado, setBemVinculado] = useState('')
  const [erro, setErro] = useState('')
  const [salvando, setSalvando] = useState(false)

  const handleSalvar = async () => {
    setErro('')
    const bem = parseBRL(valorBem)
    const entrada = parseBRL(valorEntrada || '0')
    const parcelas = parseInt(numeroParcelas, 10)
    if (!descricao.trim()) { setErro('Informe uma descrição.'); return }
    if (bem <= entrada) { setErro('O valor do bem deve ser maior que a entrada.'); return }
    if (!parcelas || parcelas < 1) { setErro('Informe o número de parcelas.'); return }
    if (!contaBancariaId) { setErro('Selecione a conta.'); return }
    if (!valorParcela && !taxaJurosMensal && sistema !== 'SemJuros') {
      setErro('Informe a taxa de juros ou o valor da parcela (pra calcular a taxa).'); return
    }
    setSalvando(true)
    try {
      await criarFinanciamento(clienteId, {
        descricao: descricao.trim(), tipo, valorBem: bem, valorEntrada: entrada, numeroParcelas: parcelas,
        valorParcela: valorParcela ? parseBRL(valorParcela) : undefined,
        taxaJurosMensal: taxaJurosMensal ? parseFloat(taxaJurosMensal.replace(',', '.')) / 100 : undefined,
        sistema, dataPrimeiraParcela, contaBancariaId, bemVinculado: bemVinculado.trim() || undefined,
        origemMetaId: prefill?.origemMetaId,
      })
      onCriado()
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao criar.')
    } finally {
      setSalvando(false)
    }
  }

  return (
    <Modal open title="Novo financiamento" onClose={onClose} footer={
      <>
        <button className="btn-cancel" onClick={onClose}>Cancelar</button>
        <button className="btn-primary" onClick={handleSalvar} disabled={salvando}>Salvar</button>
      </>
    }>
      {erro && <div className="financiamentos-msg">{erro}</div>}
      {prefill && (
        <p className="financiamentos-dica">
          Pré-preenchido a partir do objetivo 🏆 — complete os dados do contrato (parcelas, taxa ou valor da parcela) abaixo.
        </p>
      )}
      <div className="form-grid">
        <label>Descrição
          <input value={descricao} onChange={e => setDescricao(e.target.value)} placeholder="Ex.: Carro, Reforma..." />
        </label>
        <label>Tipo
          <select value={tipo} onChange={e => setTipo(e.target.value as Financiamento['tipo'])}>
            <option value="Financiamento">Financiamento</option>
            <option value="Emprestimo">Empréstimo</option>
            <option value="Consorcio">Consórcio</option>
            <option value="Parcelamento">Parcelamento</option>
          </select>
        </label>
        <label>Valor do bem
          <input value={valorBem} onChange={e => setValorBem(e.target.value)} onBlur={() => setValorBem(fmtNum(parseBRL(valorBem)))} placeholder="0,00" />
        </label>
        <label>Entrada
          <input value={valorEntrada} onChange={e => setValorEntrada(e.target.value)} onBlur={() => setValorEntrada(fmtNum(parseBRL(valorEntrada)))} placeholder="0,00" />
        </label>
        <label>Nº de parcelas
          <input value={numeroParcelas} onChange={e => setNumeroParcelas(e.target.value.replace(/\D/g, ''))} placeholder="Ex.: 42" />
        </label>
        <label>Sistema
          <select value={sistema} onChange={e => setSistema(e.target.value as Financiamento['sistema'])}>
            <option value="Price">Price (parcela fixa)</option>
            <option value="SAC">SAC (amortização fixa)</option>
            <option value="SemJuros">Sem juros</option>
          </select>
        </label>
        {sistema !== 'SemJuros' && (
          <>
            <label>Valor da parcela (opcional)
              <input value={valorParcela} onChange={e => setValorParcela(e.target.value)} onBlur={() => setValorParcela(v => v ? fmtNum(parseBRL(v)) : v)} placeholder="0,00" />
            </label>
            <label>Taxa de juros a.m. % (opcional)
              <input value={taxaJurosMensal} onChange={e => setTaxaJurosMensal(e.target.value)} placeholder="Ex.: 1,5" />
            </label>
          </>
        )}
        <label>1ª parcela
          <input type="date" value={dataPrimeiraParcela} onChange={e => setDataPrimeiraParcela(e.target.value)} />
        </label>
        <label>Conta
          <select value={contaBancariaId} onChange={e => setContaBancariaId(e.target.value)}>
            {contas.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        </label>
        <label>Bem vinculado (opcional)
          <input value={bemVinculado} onChange={e => setBemVinculado(e.target.value)} placeholder="Ex.: Placa ABC-1234" />
        </label>
      </div>
      <p className="financiamentos-dica">
        Informe a taxa de juros OU o valor da parcela (a taxa é calculada a partir dele). Sistema Price = parcela
        fixa; SAC = amortização fixa (parcela decrescente).
      </p>
    </Modal>
  )
}

function AdiantarModal({ financiamento, onClose, onAplicado }: {
  financiamento: Financiamento
  onClose: () => void
  onAplicado: () => void
}) {
  const [valor, setValor] = useState('')
  const [data, setData] = useState(todayISO())
  const [modo, setModo] = useState<'ReduzirPrazo' | 'ReduzirParcela'>('ReduzirPrazo')
  const [resultado, setResultado] = useState<AdiantamentoResultado | null>(null)
  const [erro, setErro] = useState('')
  const [processando, setProcessando] = useState(false)

  const valorNum = parseBRL(valor)

  // Item B3: recalcula enquanto o usuário digita (debounce de 300ms) em vez de só no blur — o
  // efeito reage a valor/modo/data, então troca de modo ou de data também redispara a simulação.
  useEffect(() => {
    if (valorNum <= 0) { setResultado(null); setErro(''); return }
    const timer = setTimeout(async () => {
      setErro('')
      try {
        const r = await adiantarFinanciamento(financiamento.id, { valor: valorNum, data, modo, simular: true })
        setResultado(r)
      } catch (e) {
        setResultado(null)
        setErro(e instanceof Error ? e.message : 'Erro ao simular.')
      }
    }, 300)
    return () => clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [valorNum, modo, data])

  const handleValorBlur = () => setValor(fmtNum(valorNum))

  const handleConfirmar = async () => {
    setProcessando(true)
    setErro('')
    try {
      await adiantarFinanciamento(financiamento.id, { valor: parseBRL(valor), data, modo, simular: false })
      onAplicado()
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao adiantar.')
    } finally {
      setProcessando(false)
    }
  }

  return (
    <Modal open title={`Adiantar — ${financiamento.descricao}`} onClose={onClose} footer={
      <>
        <button className="btn-cancel" onClick={onClose}>Cancelar</button>
        <button className="btn-primary" onClick={handleConfirmar} disabled={processando || valorNum <= 0}>Confirmar adiantamento</button>
      </>
    }>
      {erro && <div className="financiamentos-msg">{erro}</div>}
      <p>Saldo devedor atual: <strong>{fmtBRL(financiamento.saldoDevedor)}</strong></p>
      <div className="form-grid">
        <label>Valor do adiantamento
          <input value={valor} onChange={e => setValor(e.target.value)} onBlur={handleValorBlur} placeholder="0,00" />
        </label>
        <label>Data
          <input type="date" value={data} onChange={e => setData(e.target.value)} />
        </label>
        <label>O que fazer com o adiantamento
          <select value={modo} onChange={e => setModo(e.target.value as 'ReduzirPrazo' | 'ReduzirParcela')}>
            <option value="ReduzirPrazo">Reduzir o número de parcelas (mantém o valor delas)</option>
            <option value="ReduzirParcela">Reduzir o valor de cada parcela (mantém a quantidade)</option>
          </select>
        </label>
      </div>
      {resultado && (
        <div className="adiantamento-simulacao">
          <div><span>Saldo devedor</span><strong>{fmtBRL(resultado.saldoAntes)} → {fmtBRL(resultado.saldoDepois)}</strong></div>
          <div><span>Parcelas restantes</span><strong>{resultado.parcelasAntes} → {resultado.parcelasDepois}</strong></div>
          <div><span>Economia de juros</span><strong className="economia">{fmtBRL(resultado.economiaJuros)}</strong></div>
        </div>
      )}
    </Modal>
  )
}
