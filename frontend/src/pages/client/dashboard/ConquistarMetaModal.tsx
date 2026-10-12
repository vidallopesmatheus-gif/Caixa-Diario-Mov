import { useState, useMemo, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import Modal from '../../../components/shared/Modal'
import CategoriaCombobox from '../../../components/shared/CategoriaCombobox'
import { conquistarMeta } from '../../../api/metas'
import { listarCategorias } from '../../../api/categorias'
import { fmtBRL, todayISO } from '../../../utils/format'
import type { MetaAnual, ContaBancaria, Categorias, CategoriaAdmin } from '../../../types'

function fmtNum(n: number): string {
  if (!n) return ''
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  return parseFloat(s.replace(/\./g, '').replace(',', '.')) || 0
}

interface Props {
  meta: MetaAnual
  contasBancarias: ContaBancaria[]
  onClose: () => void
  onConquistada: (meta: MetaAnual) => void
}

// Item 5.2: sugestão de categoria pelo mesmo ícone escolhido pro sonho — não é obrigatório, só
// pré-preenche pra agilizar; o usuário pode trocar livremente pelo combobox com busca.
const CATEGORIA_POR_ICONE: Record<string, string> = {
  '💻': 'Equipamentos',
  '🚗': 'Veículos',
  '✈️': 'Viagem',
  '🏠': 'Imóvel',
}

const FORMAS: { valor: MetaAnual['formaPagamento']; label: string; disponivel: boolean }[] = [
  { valor: 'AVista', label: 'À Vista', disponivel: true },
  { valor: 'Cartao', label: 'Cartão', disponivel: true },
  { valor: 'Boleto', label: 'Boleto', disponivel: true },
  { valor: 'Financiado', label: 'Financiado', disponivel: true },
]

export default function ConquistarMetaModal({ meta, contasBancarias, onClose, onConquistada }: Props) {
  const navigate = useNavigate()
  const [valorPago, setValorPago] = useState(fmtNum(meta.valorSonho))
  const [data, setData] = useState(todayISO())
  const [formaPagamento, setFormaPagamento] = useState<NonNullable<MetaAnual['formaPagamento']>>('AVista')
  const [parcelas, setParcelas] = useState('2')
  const [dataPrimeiraParcela, setDataPrimeiraParcela] = useState(todayISO())
  const [contaPagamentoId, setContaPagamentoId] = useState('')
  const [categoria, setCategoria] = useState(meta.iconeSonho ? CATEGORIA_POR_ICONE[meta.iconeSonho] ?? '' : '')
  const [observacao, setObservacao] = useState('')
  const [resgatarDoInvestimento, setResgatarDoInvestimento] = useState(true)
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState('')

  useEffect(() => { listarCategorias(meta.clienteId).then(setCategorias).catch(() => {}) }, [meta.clienteId])

  const contasAtivas = useMemo(() => contasBancarias.filter(c => c.ativa), [contasBancarias])
  const contaCartaoSelecionada = contasAtivas.find(c => c.id === contaPagamentoId)?.tipo === 'CartaoCredito'
  const contaInvestimento = contasBancarias.find(c => c.id === meta.contaInvestimentoId)

  function handleCategoriaCriada(nova: CategoriaAdmin) {
    setCategorias(prev => ({ ...prev, saidas: [...prev.saidas, { nome: nova.nome, tipoCusto: nova.tipo, grupo: nova.grupoNome }] }))
  }

  const valorPagoNum = parseBRL(valorPago)
  const resumo = useMemo(() => {
    if (!valorPagoNum) return null
    if (formaPagamento === 'AVista') return `Será criada 1 saída de ${fmtBRL(valorPagoNum)} na conta escolhida.`
    if (formaPagamento === 'Boleto') {
      const n = Math.max(1, Number(parcelas) || 1)
      return `${n} conta(s) a pagar de ${fmtBRL(valorPagoNum / n)}, a partir de ${new Date(dataPrimeiraParcela + 'T12:00:00').toLocaleDateString('pt-BR', { month: 'short', year: 'numeric' })}.`
    }
    if (formaPagamento === 'Cartao') {
      const n = Math.max(2, Number(parcelas) || 2)
      return `${n} parcelas de ${fmtBRL(valorPagoNum / n)} no cartão escolhido.`
    }
    if (formaPagamento === 'Financiado') {
      return 'Você vai completar o contrato (parcelas, taxa, sistema) na tela de Financiamentos.'
    }
    return null
  }, [formaPagamento, valorPagoNum, parcelas, dataPrimeiraParcela])

  async function confirmar() {
    if (formaPagamento === 'Financiado') {
      if (!valorPagoNum || !contaPagamentoId) {
        setErro('Preencha o valor do bem e a conta.')
        return
      }
      onClose()
      navigate('/contas/financiamentos', {
        state: {
          novoFinanciamento: {
            descricao: meta.sonho || 'Objetivo', valorBem: valorPagoNum,
            contaBancariaId: contaPagamentoId, origemMetaId: meta.id,
          },
        },
      })
      return
    }

    if (!valorPagoNum || !contaPagamentoId || !categoria) {
      setErro('Preencha valor, categoria e a conta de pagamento.')
      return
    }
    if (formaPagamento === 'Cartao' && !contaCartaoSelecionada) {
      setErro('Escolha uma conta de cartão de crédito pra pagar no cartão.')
      return
    }
    setSalvando(true)
    setErro('')
    try {
      const atualizada = await conquistarMeta(meta.id, {
        valorPago: valorPagoNum, data, formaPagamento,
        parcelas: formaPagamento !== 'AVista' ? Math.max(1, Number(parcelas) || 1) : undefined,
        dataPrimeiraParcela: formaPagamento !== 'AVista' ? dataPrimeiraParcela : undefined,
        contaPagamentoId, categoria, observacao: observacao || undefined,
        resgatarDoInvestimento: formaPagamento === 'AVista' && !!meta.contaInvestimentoId && resgatarDoInvestimento,
      })
      onConquistada(atualizada)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao registrar a conquista.')
    } finally {
      setSalvando(false)
    }
  }

  return (
    <Modal
      open
      title={`🏆 Registrar conquista — ${meta.sonho || 'Objetivo'}`}
      onClose={() => { if (!salvando) onClose() }}
      footer={
        <>
          <button className="er-btn-lote" onClick={onClose} disabled={salvando}>Cancelar</button>
          <button className="btn-save" onClick={confirmar} disabled={salvando}>
            {salvando ? 'Registrando...' : formaPagamento === 'Financiado' ? 'Abrir cadastro de financiamento' : '🏆 Confirmar conquista'}
          </button>
        </>
      }
    >
      {erro && <div className="er-msg-erro" style={{ marginBottom: 12 }}>{erro}</div>}

      <div className="inp-group">
        <label>{formaPagamento === 'Financiado' ? 'Valor do bem' : 'Valor pago'}</label>
        <div className="val-input-wrap">
          <span className="val-prefix">R$</span>
          <input type="text" inputMode="decimal" value={valorPago}
            onChange={e => setValorPago(e.target.value)}
            onBlur={() => { const n = parseBRL(valorPago); if (n) setValorPago(fmtNum(n)) }} />
        </div>
      </div>

      {formaPagamento !== 'Financiado' && (
        <div className="inp-group" style={{ marginTop: 12 }}>
          <label>Data da compra</label>
          <input type="date" value={data} max={todayISO()} onChange={e => setData(e.target.value)} />
        </div>
      )}

      <div className="inp-group" style={{ marginTop: 12 }}>
        <label>Forma de pagamento</label>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {FORMAS.map(f => (
            <button key={f.valor} type="button" disabled={!f.disponivel}
              className={formaPagamento === f.valor ? 'btn-save' : 'er-btn-lote'}
              onClick={() => f.disponivel && setFormaPagamento(f.valor!)}>
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {formaPagamento !== 'AVista' && formaPagamento !== 'Financiado' && (
        <div className="inp-group" style={{ marginTop: 12, display: 'flex', gap: 8 }}>
          <div style={{ flex: 1 }}>
            <label>Parcelas ({formaPagamento === 'Cartao' ? '2–48' : '1–48'})</label>
            <input type="number" min={formaPagamento === 'Cartao' ? 2 : 1} max={48} value={parcelas}
              onChange={e => setParcelas(e.target.value)} />
          </div>
          <div style={{ flex: 1 }}>
            <label>Data da 1ª parcela</label>
            <input type="date" value={dataPrimeiraParcela} onChange={e => setDataPrimeiraParcela(e.target.value)} />
          </div>
        </div>
      )}

      <div className="inp-group" style={{ marginTop: 12 }}>
        <label>{formaPagamento === 'Cartao' ? 'Cartão' : formaPagamento === 'Financiado' ? 'Conta do financiamento' : 'Conta de pagamento'}</label>
        <select value={contaPagamentoId} onChange={e => setContaPagamentoId(e.target.value)}>
          <option value="">Selecione...</option>
          {contasAtivas
            .filter(c => formaPagamento !== 'Cartao' || c.tipo === 'CartaoCredito')
            .map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
        </select>
      </div>

      {/* Item C1: escondido por CSS (não desmontado) quando a forma é "Financiado" — trocar de
          forma e voltar não pode perder a categoria/observação já preenchidas. Um "{cond && <JSX>}"
          aqui desmontaria o CategoriaCombobox e, com ele, qualquer estado interno dependente do
          ciclo de montagem (ex.: o texto digitado antes de confirmar a seleção). */}
      <div style={{ display: formaPagamento === 'Financiado' ? 'none' : undefined }}>
        <div className="inp-group" style={{ marginTop: 12 }}>
          <label>Categoria</label>
          <CategoriaCombobox
            clienteId={meta.clienteId}
            categorias={categorias.saidas}
            value={categoria}
            onChange={setCategoria}
            onCategoriaCriada={handleCategoriaCriada}
            blocoPadraoNovaCategoria="DESPESAS OPERACIONAIS"
            placeholder="Buscar categoria..."
          />
        </div>

        <div className="inp-group" style={{ marginTop: 12 }}>
          <label>Observação (opcional)</label>
          <textarea value={observacao} onChange={e => setObservacao(e.target.value)} rows={2} style={{ width: '100%' }} />
        </div>
      </div>

      {formaPagamento === 'AVista' && contaInvestimento && (
        <label className="inp-group" style={{ marginTop: 12, display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
          <input type="checkbox" checked={resgatarDoInvestimento} onChange={e => setResgatarDoInvestimento(e.target.checked)} />
          <span style={{ fontSize: 13 }}>
            Resgatar {valorPagoNum ? fmtBRL(valorPagoNum) : ''} do <strong>{contaInvestimento.nome}</strong> para a conta de pagamento
          </span>
        </label>
      )}

      {resumo && (
        <div style={{ marginTop: 14, padding: 10, borderRadius: 8, background: 'var(--bg-input)', fontSize: 13, color: 'var(--tx2)' }}>
          {resumo}
        </div>
      )}
    </Modal>
  )
}
