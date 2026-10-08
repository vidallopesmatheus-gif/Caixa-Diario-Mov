import { useState, useEffect, useRef } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import DayNav from '../../components/shared/DayNav'
import { fmtBRL, todayISO, addDays } from '../../utils/format'
import { listarCategorias } from '../../api/categorias'
import { listarContasBancarias } from '../../api/contasBancarias'
import { converterLancamentoEmTransferencia } from '../../api/transferencias'
import { excluirLancamento } from '../../api/importacao'
import CategoriaCombobox from '../../components/shared/CategoriaCombobox'
import type { ItemFinanceiro, ItemFinanceiroSaida, Categorias, ContaBancaria, CategoriaAdmin } from '../../types'
import './ClientCaixa.css'

interface Props { clienteIdOverride?: string }

// Fase 1.13: GET registro-por-data agora sempre devolve 200 — sem lançamento ainda vem com esse
// Id vazio em vez de 404. Precisa continuar tratado como "dia em branco" (ex.: campo "Confirmar
// saldo" começa vazio, não com o saldo calculado já preenchido).
const GUID_VAZIO = '00000000-0000-0000-0000-000000000000'

// Cada linha carrega sua própria conta de destino, explícita desde a criação — default é a conta
// selecionada na tela no momento (nunca um "last used" global, que misturaria linhas de contas
// diferentes se o usuário trocasse a conta de só uma linha). Quem só movimenta dinheiro numa conta
// só nunca vê esse campo (seletor só aparece com 2+ contas ativas), então não precisa tocar em nada.
// transferenciaContaId: preenchido quando a linha foi marcada como transferência (não uma
// categoria de verdade) — guarda a conta contrapartida escolhida, pra criar o par vinculado
// depois que a linha for salva (precisa existir de verdade antes de virar o outro lado do par).
type LinhaEntrada = ItemFinanceiro & { contaId: string; transferenciaContaId?: string }
type LinhaSaida = ItemFinanceiroSaida & { contaId: string; transferenciaContaId?: string }

const CATEGORIA_TRANSFERENCIA = 'Transferência'

// Id gerado no cliente pra todo lançamento novo nascer com identidade própria — sem isso, baixas
// de Contas a Pagar/Receber não conseguem vincular com segurança a um lançamento manual específico.
const novaEntrada = (contaId = ''): LinhaEntrada => ({ id: crypto.randomUUID(), descricao: '', valor: 0, contaId })
const novaSaida = (contaId = ''): LinhaSaida => ({ id: crypto.randomUUID(), descricao: '', valor: 0, categoria: '', subcategoria: '', contaId })

function agruparPorConta<T extends { contaId: string }>(itens: T[]): Map<string, T[]> {
  const mapa = new Map<string, T[]>()
  for (const item of itens) {
    const lista = mapa.get(item.contaId)
    if (lista) lista.push(item)
    else mapa.set(item.contaId, [item])
  }
  return mapa
}

function fmtNum(n: number) {
  if (!n) return ''
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  return parseFloat(s.replace(/\./g, '').replace(',', '.')) || 0
}

export default function ClientCaixaPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { loading, salvar, buscarPorData } = useRegistros(clienteId)

  const [data, setData] = useState(todayISO())
  const [inicio, setInicio] = useState(0)
  const [entradas, setEntradas] = useState<LinhaEntrada[]>([novaEntrada()])
  const [saidas, setSaidas] = useState<LinhaSaida[]>([novaSaida()])
  const [entradaDisplays, setEntradaDisplays] = useState<string[]>([''])
  const [saidaDisplays, setSaidaDisplays] = useState<string[]>([''])
  const [confirmado, setConfirmado] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  const [saveSuccess, setSaveSuccess] = useState(false)
  const [catOpen, setCatOpen] = useState<{ tipo: 'entrada' | 'saida'; idx: number } | null>(null)
  // Sub-passo dentro do mesmo dropdown de categoria: usuário clicou "🔁 É uma transferência" e
  // agora escolhe a conta contrapartida, em vez de uma categoria normal.
  const [escolhendoContrapartida, setEscolhendoContrapartida] = useState(false)
  const [savedEntradas, setSavedEntradas] = useState<LinhaEntrada[]>([])
  const [savedSaidas, setSavedSaidas] = useState<LinhaSaida[]>([])
  const dropdownRef = useRef<HTMLDivElement>(null)
  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [contaId, setContaId] = useState<string>('')
  const entradaDescRefs = useRef<(HTMLInputElement | null)[]>([])
  const saidaDescRefs = useRef<(HTMLInputElement | null)[]>([])

  // Item 2.3: skeleton deve cobrir TODO o carregamento inicial (lista de registros + contas + o
  // registro do dia), não só o primeiro — senão a tela real aparece de passagem com "R$ 0,00" e
  // sem o seletor de conta enquanto esses outros fetches ainda estão em voo. Só vale pra PRIMEIRA
  // carga: trocar de dia/conta depois não deve esconder a tela inteira de novo.
  const [contasLoading, setContasLoading] = useState(true)
  const [carregandoInicial, setCarregandoInicial] = useState(true)
  const primeiraCargaFeita = useRef(false)

  // Sem clienteId não há nada pra carregar (ex.: usuário/auth ainda não resolvido) — não trava no
  // skeleton pra sempre esperando um fetch que nunca vai disparar.
  useEffect(() => {
    if (!clienteId) {
      setContasLoading(false)
      setCarregandoInicial(false)
    }
  }, [clienteId])

  useEffect(() => {
    listarCategorias().then(setCategorias).catch(console.error)
  }, [])

  useEffect(() => {
    if (!clienteId) return
    listarContasBancarias(clienteId)
      .then(cs => {
        setContas(cs)
        const ativas = cs.filter(c => c.ativa)
        if (ativas.length > 0 && !contaId) {
          const caixa = ativas.find(c => c.tipo === 'Caixa') ?? ativas[0]
          setContaId(caixa.id)
        } else if (ativas.length === 0 && !primeiraCargaFeita.current) {
          // Cliente sem nenhuma conta ativa — não há registro-por-data a esperar, encerra a carga.
          primeiraCargaFeita.current = true
          setCarregandoInicial(false)
        }
      })
      .catch(e => {
        console.error(e)
        // Falhou a busca de contas — não há mais nada que destrave o carregamento inicial (o
        // efeito do dia depende de contaId, que nunca vai ser preenchido). Encerra aqui mesmo,
        // pra não deixar a tela presa no skeleton pra sempre.
        if (!primeiraCargaFeita.current) {
          primeiraCargaFeita.current = true
          setCarregandoInicial(false)
        }
      })
      .finally(() => setContasLoading(false))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clienteId])

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setCatOpen(null)
        setEscolhendoContrapartida(false)
      }
    }
    if (catOpen) document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [catOpen])

  // Rascunhos que o usuário moveu pra outra conta (via o select por item) não entram no total do
  // card — ele é só da conta exibida agora. savedEntradas/savedSaidas já são só dela (vêm de
  // buscarPorData(data, contaId)).
  const totalEntradas = [...entradas.filter(e => e.contaId === contaId), ...savedEntradas].reduce((s, x) => s + (Number(x.valor) || 0), 0)
  const totalSaidas = [...saidas.filter(s => s.contaId === contaId), ...savedSaidas].reduce((s, x) => s + (Number(x.valor) || 0), 0)
  const calculado = inicio + totalEntradas - totalSaidas
  const dif = confirmado !== '' ? calculado - Number(confirmado) : null

  useEffect(() => {
    if (!clienteId || !contaId) return
    let ignore = false
    // Item 2.4: limpa os selos JÁ, antes do fetch assíncrono — senão os selos da conta/dia
    // anterior continuam visíveis (e contando no total) durante a troca.
    setSavedEntradas([])
    setSavedSaidas([])
    const load = async () => {
      const reg = await buscarPorData(data, contaId)
      if (ignore) return
      // reg.id vazio = Fase 1.13: backend já devolve o saldo inicial calculado (último registro
      // real anterior desta conta, ou SaldoInicial da conta), só não é um registro persistido.
      if (reg && reg.id !== GUID_VAZIO) {
        setInicio(reg.saldoInicio)
        setSavedEntradas(reg.entradas.map(e => ({ ...e, contaId })))
        setSavedSaidas(reg.saidas.map(s => ({ ...s, contaId })))
        setEntradas([novaEntrada(contaId)])
        setSaidas([novaSaida(contaId)])
        setEntradaDisplays([''])
        setSaidaDisplays([''])
        setConfirmado(String(reg.saldoConfirmado))
      } else {
        setInicio(reg?.saldoInicio ?? 0)
        setSavedEntradas([])
        setSavedSaidas([])
        setEntradas([novaEntrada(contaId)])
        setSaidas([novaSaida(contaId)])
        setEntradaDisplays([''])
        setSaidaDisplays([''])
        setConfirmado('')
      }
      if (!primeiraCargaFeita.current) {
        primeiraCargaFeita.current = true
        setCarregandoInicial(false)
      }
    }
    load()
    return () => { ignore = true }
  }, [data, clienteId, contaId, buscarPorData])

  function handleSaveEntrada(idx: number) {
    const item = entradas[idx]
    if (!item.descricao && !item.valor) return
    setSavedEntradas(prev => [...prev, item])
    setEntradas(prev => {
      const next = prev.filter((_, j) => j !== idx)
      return next.length ? next : [novaEntrada(contaId)]
    })
    setEntradaDisplays(prev => {
      const next = prev.filter((_, j) => j !== idx)
      return next.length ? next : ['']
    })
  }

  function handleSaveSaida(idx: number) {
    const item = saidas[idx]
    if (!item.descricao && !item.valor) return
    if (!item.categoria) { setMsg('Selecione uma categoria para a saída.'); setSaveSuccess(false); return }
    setSavedSaidas(prev => [...prev, item])
    setSaidas(prev => {
      const next = prev.filter((_, j) => j !== idx)
      return next.length ? next : [novaSaida(contaId)]
    })
    setSaidaDisplays(prev => {
      const next = prev.filter((_, j) => j !== idx)
      return next.length ? next : ['']
    })
    setMsg('')
  }

  // Item 2.4: Enter em qualquer campo da linha confirma o lançamento e já foca a descrição da
  // próxima linha (que pode ser uma linha já existente que subiu de posição, ou a linha em branco
  // nova criada automaticamente quando essa era a última). setTimeout(0) espera o React aplicar o
  // filter/novaEntrada antes de focar — focar no mesmo tick ainda pegaria o input antigo.
  function handleEnterNaLinha(tipo: 'entrada' | 'saida', idx: number, ev: React.KeyboardEvent) {
    if (ev.key !== 'Enter') return
    ev.preventDefault()
    if (tipo === 'entrada') handleSaveEntrada(idx)
    else handleSaveSaida(idx)
    setTimeout(() => {
      const refs = tipo === 'entrada' ? entradaDescRefs.current : saidaDescRefs.current
      refs[idx]?.focus()
    }, 0)
  }

  // Item 2.4: "editar" um item já salvo (persistido no servidor) devolve ele pra lista de
  // rascunhos editáveis, no topo — exatamente a mesma linha de edição que todo lançamento novo
  // usa. Só vira persistência de verdade no próximo "Salvar e sincronizar" (mesma regra de
  // qualquer rascunho desta tela); o id original é preservado, então o reenvio substitui o item
  // antigo em vez de duplicar.
  function editarEntradaSalva(idx: number) {
    const item = savedEntradas[idx]
    // entradaDisplays é paralelo a entradas por posição — filtra os dois JUNTOS (pelo mesmo
    // índice) pra não desalinhar qual display pertence a qual rascunho.
    const rascunhos = entradas
      .map((e, j) => ({ e, display: entradaDisplays[j] ?? '' }))
      .filter(({ e }) => e.descricao || e.valor)
    setSavedEntradas(prev => prev.filter((_, j) => j !== idx))
    setEntradas([item, ...rascunhos.map(r => r.e)])
    setEntradaDisplays([fmtNum(item.valor), ...rascunhos.map(r => r.display)])
    setTimeout(() => entradaDescRefs.current[0]?.focus(), 0)
  }

  function editarSaidaSalva(idx: number) {
    const item = savedSaidas[idx]
    const rascunhos = saidas
      .map((s, j) => ({ s, display: saidaDisplays[j] ?? '' }))
      .filter(({ s }) => s.descricao || s.valor)
    setSavedSaidas(prev => prev.filter((_, j) => j !== idx))
    setSaidas([item, ...rascunhos.map(r => r.s)])
    setSaidaDisplays([fmtNum(item.valor), ...rascunhos.map(r => r.display)])
    setTimeout(() => saidaDescRefs.current[0]?.focus(), 0)
  }

  // Item 2.4: "excluir" um item já salvo chama o mesmo endpoint usado no Banco (reaproveita a
  // limpeza de vínculos/transferências que ele já faz), não um filtro só local — senão o item
  // continuaria existindo no servidor até o próximo "Salvar e sincronizar".
  async function excluirEntradaSalva(idx: number) {
    const item = savedEntradas[idx]
    if (!item.id) return
    if (!confirm(`Excluir "${item.descricao || 'Entrada'}" (${fmtBRL(item.valor)})? Essa ação não pode ser desfeita.`)) return
    try {
      await excluirLancamento(contaId, { id: item.id, data })
      setSavedEntradas(prev => prev.filter((_, j) => j !== idx))
    } catch (e: unknown) {
      setSaveSuccess(false)
      setMsg(e instanceof Error ? e.message : 'Erro ao excluir lançamento.')
    }
  }

  async function excluirSaidaSalva(idx: number) {
    const item = savedSaidas[idx]
    if (!item.id) return
    if (!confirm(`Excluir "${item.descricao || 'Saída'}" (${fmtBRL(item.valor)})? Essa ação não pode ser desfeita.`)) return
    try {
      await excluirLancamento(contaId, { id: item.id, data })
      setSavedSaidas(prev => prev.filter((_, j) => j !== idx))
    } catch (e: unknown) {
      setSaveSuccess(false)
      setMsg(e instanceof Error ? e.message : 'Erro ao excluir lançamento.')
    }
  }

  // Mesma lógica de carregamento do efeito de troca de dia/conta — chamada aqui pra garantir que,
  // depois de salvar, a tela reflita exatamente o que ficou gravado no servidor (nunca os arrays
  // locais, que já foram enviados e não devem ser reenviados numa próxima sincronização).
  async function recarregarRegistroAtual() {
    if (!clienteId || !contaId) return
    const reg = await buscarPorData(data, contaId)
    const existe = reg && reg.id !== GUID_VAZIO
    setSavedEntradas(existe ? reg!.entradas.map(e => ({ ...e, contaId })) : [])
    setSavedSaidas(existe ? reg!.saidas.map(s => ({ ...s, contaId })) : [])
    setInicio(reg?.saldoInicio ?? 0)
    setConfirmado(existe ? String(reg!.saldoConfirmado) : '')
    setEntradas([novaEntrada(contaId)])
    setSaidas([novaSaida(contaId)])
    setEntradaDisplays([''])
    setSaidaDisplays([''])
  }

  async function handleSave() {
    if (!clienteId) return
    setSaving(true)
    setMsg('')
    try {
      const todasEntradas = [...savedEntradas, ...entradas.filter(e => e.descricao || e.valor)]
      const todasSaidas = [...savedSaidas, ...saidas.filter(s => s.descricao || s.valor)]
      if (todasSaidas.some(s => !s.categoria)) {
        setSaveSuccess(false)
        setMsg('Selecione uma categoria para cada saída.')
        return
      }

      // Cada linha pode ter sua própria conta — agrupa e salva um registro por conta envolvida,
      // preservando os lançamentos e pendências que já existiam em cada uma.
      const gruposEntrada = agruparPorConta(todasEntradas)
      const gruposSaida = agruparPorConta(todasSaidas)
      const contasEnvolvidas = new Set([...gruposEntrada.keys(), ...gruposSaida.keys()])
      if (contasEnvolvidas.size === 0) contasEnvolvidas.add(contaId)

      // Item 3.3: o motor de vínculo roda depois de salvar — soma as sugestões encontradas em
      // cada conta/dia afetado pra avisar o usuário de uma vez, sem precisar ir a Contas conferir.
      let totalSugestoesVinculo = 0

      for (const grupoContaId of contasEnvolvidas) {
        const entradasGrupo = gruposEntrada.get(grupoContaId) ?? []
        const saidasGrupo = gruposSaida.get(grupoContaId) ?? []
        const regBuscado = await buscarPorData(data, grupoContaId)
        // Fase 1.13: o backend sempre devolve 200 agora — Id vazio significa que esse dia ainda
        // não tem registro persistido (não é "sem resposta"). saldoInicio já vem corretamente
        // calculado (último registro real anterior desta conta) nos dois casos.
        const regExistente = regBuscado && regBuscado.id !== GUID_VAZIO ? regBuscado : null
        const inicioGrupo = regBuscado?.saldoInicio ?? 0
        const totalEntradasGrupo = entradasGrupo.reduce((s, x) => s + (Number(x.valor) || 0), 0)
        const totalSaidasGrupo = saidasGrupo.reduce((s, x) => s + (Number(x.valor) || 0), 0)
        const calculadoGrupo = inicioGrupo + totalEntradasGrupo - totalSaidasGrupo
        const saldoConfirmadoGrupo = grupoContaId === contaId && confirmado !== ''
          ? Number(confirmado)
          : regExistente?.saldoConfirmado ?? calculadoGrupo

        const salvo = await salvar({
          clienteId, contaBancariaId: grupoContaId, data, saldoInicio: inicioGrupo,
          entradas: [...(regExistente?.entradas ?? []), ...entradasGrupo],
          saidas: [...(regExistente?.saidas ?? []), ...saidasGrupo],
          contasAReceber: regExistente?.contasAReceber ?? [],
          contasAPagar: regExistente?.contasAPagar ?? [],
          saldoConfirmado: saldoConfirmadoGrupo,
        })
        totalSugestoesVinculo += salvo?.totalSugestoesVinculo ?? 0

        // Linhas marcadas como transferência: só dá pra criar o par vinculado depois que a linha
        // existe de verdade (salva acima). Sequencial, não Promise.all — evita duas chamadas
        // lendo/escrevendo o mesmo RegistroDiario ao mesmo tempo e uma perder a alteração da outra.
        for (const item of entradasGrupo.filter(e => e.transferenciaContaId && e.id)) {
          await converterLancamentoEmTransferencia({
            contaId: grupoContaId, lancamentoId: item.id!, data, tipo: 'Entrada',
            contaContrapartidaId: item.transferenciaContaId!,
          })
        }
        for (const item of saidasGrupo.filter(s => s.transferenciaContaId && s.id)) {
          await converterLancamentoEmTransferencia({
            contaId: grupoContaId, lancamentoId: item.id!, data, tipo: 'Saida',
            contaContrapartidaId: item.transferenciaContaId!,
          })
        }
      }

      await recarregarRegistroAtual()
      setSaveSuccess(true)
      setMsg(totalSugestoesVinculo > 0
        ? `Salvo com sucesso! 💡 ${totalSugestoesVinculo} conta(s) prevista(s) encontrada(s) no extrato — reveja em Contas.`
        : 'Salvo com sucesso!')
    } catch (e: unknown) {
      setSaveSuccess(false)
      setMsg(e instanceof Error ? e.message : String(e))
    } finally {
      setSaving(false)
    }
  }

  function tipoCustoDe(nome: string) {
    const cat = [...categorias.entradas, ...categorias.saidas].find(c => c.nome === nome)
    return cat ? (cat.tipoCusto as 'Receita' | 'CustoFixo' | 'CustoVariavel') : undefined
  }

  function updateEntrada(i: number, field: keyof ItemFinanceiro, val: string) {
    setEntradas(prev => prev.map((x, j) => {
      if (j !== i) return x
      const updated: LinhaEntrada = { ...x, [field]: field === 'valor' ? Number(val) : val }
      if (field === 'categoria') {
        const tc = tipoCustoDe(val); if (tc) updated.tipoCusto = tc
        updated.transferenciaContaId = undefined // categoria normal escolhida — não é mais transferência
      }
      return updated
    }))
  }

  function updateSaida(i: number, field: keyof ItemFinanceiroSaida, val: string) {
    setSaidas(prev => prev.map((x, j) => {
      if (j !== i) return x
      const updated: LinhaSaida = { ...x, [field]: field === 'valor' ? Number(val) : val }
      if (field === 'categoria') {
        const tc = tipoCustoDe(val); if (tc) updated.tipoCusto = tc
        updated.transferenciaContaId = undefined
      }
      return updated
    }))
  }

  /** Marca a linha como transferência: categoria vira o rótulo fixo, guarda a conta contrapartida
   *  pra criar o par vinculado depois que a linha for salva de verdade. */
  function marcarTransferencia(tipo: 'entrada' | 'saida', idx: number, contaContrapartidaId: string) {
    if (tipo === 'entrada') {
      setEntradas(prev => prev.map((x, j) => j === idx
        ? { ...x, categoria: CATEGORIA_TRANSFERENCIA, transferenciaContaId: contaContrapartidaId }
        : x))
    } else {
      setSaidas(prev => prev.map((x, j) => j === idx
        ? { ...x, categoria: CATEGORIA_TRANSFERENCIA, transferenciaContaId: contaContrapartidaId }
        : x))
    }
    setCatOpen(null)
    setEscolhendoContrapartida(false)
  }

  // Troca a conta de UMA linha específica — nunca afeta as outras nem o default de linhas futuras.
  function updateEntradaConta(i: number, novaContaId: string) {
    setEntradas(prev => prev.map((x, j) => j === i ? { ...x, contaId: novaContaId } : x))
  }

  function updateSaidaConta(i: number, novaContaId: string) {
    setSaidas(prev => prev.map((x, j) => j === i ? { ...x, contaId: novaContaId } : x))
  }

  function abrirEscolhaDeContrapartida(tipo: 'entrada' | 'saida', idx: number) {
    setCatOpen({ tipo, idx })
    setEscolhendoContrapartida(true)
  }

  function cancelarEscolhaDeContrapartida() {
    setCatOpen(null)
    setEscolhendoContrapartida(false)
  }

  // Caixa é lançamento rápido do dia a dia — só receita/devolução entram aqui; Investimento e
  // Financiamento (que também contam como entrada no combobox do Banco) ficam de fora.
  function categoriasEntradaPermitidas() {
    return categorias.entradas.filter(c => c.tipoCusto === 'Receita' || c.grupo === 'Devolução e Estorno')
  }

  function handleCategoriaCriada(nova: CategoriaAdmin) {
    const item = { nome: nova.nome, tipoCusto: nova.tipo, grupo: nova.grupoNome }
    if (nova.ehEntrada) setCategorias(prev => ({ ...prev, entradas: [...prev.entradas, item] }))
    if (nova.ehSaida) setCategorias(prev => ({ ...prev, saidas: [...prev.saidas, item] }))
  }

  // Linhas em edição (ainda não salvas individualmente com ✔) que seriam perdidas ao trocar de
  // dia ou de conta — savedEntradas/savedSaidas não entram aqui porque já estão persistidas.
  function temRascunhoNaoSalvo() {
    return entradas.some(e => e.descricao || e.valor) || saidas.some(s => s.descricao || s.valor)
  }

  function podeTrocarDeTelaAgora() {
    if (!temRascunhoNaoSalvo()) return true
    return confirm('Você tem lançamentos não salvos nesta tela. Trocar de dia ou de conta agora vai descartá-los. Continuar?')
  }

  if (loading || contasLoading || carregandoInicial) {
    return (
      <>
        <div className="caixa-skeleton-stats">
          <div className="caixa-skeleton-shimmer" />
          <div className="caixa-skeleton-shimmer" />
          <div className="caixa-skeleton-shimmer" />
          <div className="caixa-skeleton-shimmer" />
        </div>
        <div className="form-card caixa-skeleton-form">
          <div className="caixa-skeleton-shimmer" />
          <div className="caixa-skeleton-shimmer" />
          <div className="caixa-skeleton-shimmer" />
          <div className="caixa-skeleton-shimmer" />
        </div>
      </>
    )
  }

  return (
    <>
      <DayNav
        date={data}
        max={todayISO()}
        onPrev={() => { if (podeTrocarDeTelaAgora()) setData(d => addDays(d, -1)) }}
        onNext={() => { if (podeTrocarDeTelaAgora()) setData(d => addDays(d, 1)) }}
        onPick={novaData => { if (podeTrocarDeTelaAgora()) setData(novaData) }}
      />
      <div className="stats-grid">
        <StatCard label="📥 Início" value={fmtBRL(inicio)} />
        <StatCard label="📤 Entradas" value={fmtBRL(totalEntradas)} className="val-green" />
        <StatCard label="💸 Saídas" value={fmtBRL(totalSaidas)} className="val-red" />
        <StatCard label="💰 Saldo" value={fmtBRL(calculado)} className="val-green" />
      </div>

      {contas.length > 1 && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
          <span style={{ fontSize: 13, color: 'var(--tx3)' }}>Conta:</span>
          {contas.filter(c => c.ativa).map(c => (
            <button key={c.id} type="button"
              onClick={() => { if (podeTrocarDeTelaAgora()) setContaId(c.id) }}
              style={{
                padding: '5px 14px', borderRadius: 8, fontSize: 13, cursor: 'pointer',
                border: '1px solid var(--bd)',
                background: contaId === c.id ? '#0a84ff' : 'var(--bg-card)',
                color: contaId === c.id ? '#fff' : 'var(--tx1)',
              }}>
              {c.nome}
            </button>
          ))}
        </div>
      )}

      <div className="form-card">
        <h3>📋 Registro do dia</h3>
        <div className="inp-group">
          <label>Saldo início (preenchido automaticamente)</label>
          <input type="text" value={fmtNum(inicio)} readOnly style={{ color: 'var(--tx4)', cursor: 'not-allowed' }} />
        </div>

        <div className="inp-group">
          <label>💵 Entradas do dia</label>
          {savedEntradas.length > 0 && (
            <div className="saved-summary">
              {savedEntradas.map((e, i) => (
                <span key={i} className="saved-badge">
                  ✔ {e.descricao || 'Entrada'} · {fmtBRL(e.valor)}
                  <button type="button" className="saved-badge-acao" title="Editar" aria-label={`Editar ${e.descricao || 'entrada'}`}
                    onClick={() => editarEntradaSalva(i)}>✏️</button>
                  <button type="button" className="saved-badge-acao" title="Excluir" aria-label={`Excluir ${e.descricao || 'entrada'}`}
                    onClick={() => excluirEntradaSalva(i)}>🗑️</button>
                </span>
              ))}
            </div>
          )}
          {entradas.map((e, i) => (
            <div key={i}>
            <div className="lancamento-row">
              <input className="lancamento-desc lanc-desc" placeholder="Descrição" value={e.descricao}
                ref={el => { entradaDescRefs.current[i] = el }}
                onKeyDown={ev => handleEnterNaLinha('entrada', i, ev)}
                onChange={ev => updateEntrada(i, 'descricao', ev.target.value)} />
              <div className="val-input-wrap lanc-valor">
                <span className="val-prefix">R$</span>
                <input
                  type="text" inputMode="decimal" placeholder="0,00"
                  value={entradaDisplays[i] ?? ''}
                  onKeyDown={ev => handleEnterNaLinha('entrada', i, ev)}
                  onChange={ev => {
                    const raw = ev.target.value.replace(/[^\d,]/g, '')
                    setEntradaDisplays(prev => prev.map((v, j) => j === i ? raw : v))
                    updateEntrada(i, 'valor', String(parseBRL(raw)))
                  }}
                  onBlur={() => {
                    const num = entradas[i]?.valor ?? 0
                    setEntradaDisplays(prev => prev.map((v, j) => j === i ? fmtNum(num) : v))
                  }}
                />
              </div>
              <div className="lanc-cat">
                {e.transferenciaContaId ? (
                  <button type="button" className="cat-btn cat-btn-entrada" style={{ width: '100%' }}
                    onClick={() => updateEntrada(i, 'categoria', '')}>
                    🔁 {contas.find(c => c.id === e.transferenciaContaId)?.nome ?? 'Transferência'} ✕
                  </button>
                ) : catOpen?.tipo === 'entrada' && catOpen.idx === i && escolhendoContrapartida ? (
                  <div className="cat-dropdown" ref={dropdownRef} style={{ position: 'absolute', top: 0 }}>
                    <div className="cat-items" style={{ padding: 6 }}>
                      <div style={{ fontSize: 11, color: 'var(--tx3)', padding: '2px 6px 6px', width: '100%' }}>De qual conta veio?</div>
                      {contas.filter(c => c.ativa && c.id !== (e.contaId || contaId)).map(c => (
                        <button key={c.id} className="cat-item" style={{ borderColor: '#007aff44' }}
                          onClick={() => marcarTransferencia('entrada', i, c.id)}>
                          {c.nome}
                        </button>
                      ))}
                      <button className="cat-item" onClick={cancelarEscolhaDeContrapartida}>Cancelar</button>
                    </div>
                  </div>
                ) : (
                  <CategoriaCombobox
                    categorias={categoriasEntradaPermitidas()}
                    value={e.categoria ?? ''}
                    onChange={nome => updateEntrada(i, 'categoria', nome)}
                    onCategoriaCriada={handleCategoriaCriada}
                    blocoPadraoNovaCategoria="RECEITAS OPERACIONAIS"
                    placeholder="Categoria"
                  />
                )}
              </div>
              <div className="lanc-acoes">
                {!e.transferenciaContaId && !(catOpen?.tipo === 'entrada' && catOpen.idx === i && escolhendoContrapartida)
                  && contas.filter(c => c.ativa).length > 1 && (
                  <button type="button" className="lanc-icon-btn btn-transferencia"
                    title="Marcar como transferência" aria-label="Marcar como transferência"
                    onClick={() => abrirEscolhaDeContrapartida('entrada', i)}>🔁</button>
                )}
                <button type="button" className="lanc-icon-btn btn-item-save-entrada"
                  title="Confirmar lançamento" aria-label="Confirmar lançamento"
                  disabled={!e.descricao && !e.valor} onClick={() => handleSaveEntrada(i)}>✔</button>
                <button type="button" className="lanc-icon-btn btn-rm" title="Remover" aria-label="Remover lançamento"
                  onClick={() => {
                    setEntradas(prev => prev.filter((_, j) => j !== i))
                    setEntradaDisplays(prev => prev.filter((_, j) => j !== i))
                  }}>✕</button>
              </div>
            </div>
            {contas.filter(c => c.ativa).length > 1 && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, margin: '-4px 0 8px 2px' }}>
                <span style={{ fontSize: 11, color: 'var(--tx3)' }}>Conta:</span>
                <select
                  value={e.contaId || contaId}
                  onChange={ev => updateEntradaConta(i, ev.target.value)}
                  style={{ fontSize: 12, padding: '2px 6px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-input)', color: 'var(--tx1)' }}
                >
                  {contas.filter(c => c.ativa).map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
                </select>
              </div>
            )}
            </div>
          ))}
          <button className="btn-add-entrada" onClick={() => {
            setEntradas(e => [...e, novaEntrada(contaId)])
            setEntradaDisplays(d => [...d, ''])
          }}>＋ Adicionar Entrada</button>
        </div>

        <div className="inp-group">
          <label>💸 Saídas do dia</label>
          {savedSaidas.length > 0 && (
            <div className="saved-summary">
              {savedSaidas.map((s, i) => (
                <span key={i} className="saved-badge"
                  style={{ background: 'rgba(255,59,48,.1)', borderColor: 'rgba(255,59,48,.3)', color: '#ff6b6b' }}>
                  ✔ {s.descricao || 'Saída'} · {fmtBRL(s.valor)}
                  <button type="button" className="saved-badge-acao" title="Editar" aria-label={`Editar ${s.descricao || 'saída'}`}
                    onClick={() => editarSaidaSalva(i)}>✏️</button>
                  <button type="button" className="saved-badge-acao" title="Excluir" aria-label={`Excluir ${s.descricao || 'saída'}`}
                    onClick={() => excluirSaidaSalva(i)}>🗑️</button>
                </span>
              ))}
            </div>
          )}
          {saidas.map((s, i) => (
            <div key={i}>
            <div className="lancamento-row">
              <input className="lancamento-desc lanc-desc" placeholder="Descrição" value={s.descricao}
                ref={el => { saidaDescRefs.current[i] = el }}
                onKeyDown={ev => handleEnterNaLinha('saida', i, ev)}
                onChange={ev => updateSaida(i, 'descricao', ev.target.value)} />
              <div className="val-input-wrap lanc-valor">
                <span className="val-prefix">R$</span>
                <input
                  type="text" inputMode="decimal" placeholder="0,00"
                  value={saidaDisplays[i] ?? ''}
                  onKeyDown={ev => handleEnterNaLinha('saida', i, ev)}
                  onChange={ev => {
                    const raw = ev.target.value.replace(/[^\d,]/g, '')
                    setSaidaDisplays(prev => prev.map((v, j) => j === i ? raw : v))
                    updateSaida(i, 'valor', String(parseBRL(raw)))
                  }}
                  onBlur={() => {
                    const num = saidas[i]?.valor ?? 0
                    setSaidaDisplays(prev => prev.map((v, j) => j === i ? fmtNum(num) : v))
                  }}
                />
              </div>
              <div className="lanc-cat">
                {s.transferenciaContaId ? (
                  <button type="button" className="cat-btn cat-btn-saida com-cat" style={{ width: '100%' }}
                    onClick={() => updateSaida(i, 'categoria', '')}>
                    🔁 {contas.find(c => c.id === s.transferenciaContaId)?.nome ?? 'Transferência'} ✕
                  </button>
                ) : catOpen?.tipo === 'saida' && catOpen.idx === i && escolhendoContrapartida ? (
                  <div className="cat-dropdown" ref={dropdownRef} style={{ position: 'absolute', top: 0 }}>
                    <div className="cat-items" style={{ padding: 6 }}>
                      <div style={{ fontSize: 11, color: 'var(--tx3)', padding: '2px 6px 6px', width: '100%' }}>Pra qual conta foi?</div>
                      {contas.filter(c => c.ativa && c.id !== (s.contaId || contaId)).map(c => (
                        <button key={c.id} className="cat-item" style={{ borderColor: '#ff6b6b44' }}
                          onClick={() => marcarTransferencia('saida', i, c.id)}>
                          {c.nome}
                        </button>
                      ))}
                      <button className="cat-item" onClick={cancelarEscolhaDeContrapartida}>Cancelar</button>
                    </div>
                  </div>
                ) : (
                  <CategoriaCombobox
                    categorias={categorias.saidas}
                    value={s.categoria}
                    onChange={nome => updateSaida(i, 'categoria', nome)}
                    onCategoriaCriada={handleCategoriaCriada}
                    blocoPadraoNovaCategoria="DESPESAS OPERACIONAIS"
                    placeholder="Categoria"
                  />
                )}
              </div>
              <div className="lanc-acoes">
                {!s.transferenciaContaId && !(catOpen?.tipo === 'saida' && catOpen.idx === i && escolhendoContrapartida)
                  && contas.filter(c => c.ativa).length > 1 && (
                  <button type="button" className="lanc-icon-btn btn-transferencia"
                    title="Marcar como transferência" aria-label="Marcar como transferência"
                    onClick={() => abrirEscolhaDeContrapartida('saida', i)}>🔁</button>
                )}
                <button type="button" className="lanc-icon-btn btn-item-save-saida"
                  title="Confirmar lançamento" aria-label="Confirmar lançamento"
                  disabled={!s.descricao && !s.valor} onClick={() => handleSaveSaida(i)}>✔</button>
                <button type="button" className="lanc-icon-btn btn-rm" title="Remover" aria-label="Remover lançamento"
                  onClick={() => {
                    setSaidas(prev => prev.filter((_, j) => j !== i))
                    setSaidaDisplays(prev => prev.filter((_, j) => j !== i))
                  }}>✕</button>
              </div>
            </div>
            {contas.filter(c => c.ativa).length > 1 && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, margin: '-4px 0 8px 2px' }}>
                <span style={{ fontSize: 11, color: 'var(--tx3)' }}>Conta:</span>
                <select
                  value={s.contaId || contaId}
                  onChange={ev => updateSaidaConta(i, ev.target.value)}
                  style={{ fontSize: 12, padding: '2px 6px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-input)', color: 'var(--tx1)' }}
                >
                  {contas.filter(c => c.ativa).map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
                </select>
              </div>
            )}
            </div>
          ))}
          <button className="btn-add-saida" onClick={() => {
            setSaidas(s => [...s, novaSaida(contaId)])
            setSaidaDisplays(d => [...d, ''])
          }}>＋ Adicionar saída</button>
        </div>
      </div>

      <div className="saldo-box">
        <div>
          <div className="saldo-calc-lbl">Saldo calculado</div>
          <div className="saldo-calc-val">{fmtBRL(calculado)}</div>
        </div>
        <div style={{ textAlign: 'right' }}>
          <div className="saldo-calc-lbl">Confirmar saldo (R$)</div>
          {/* Item 2.4: placeholder é o saldo calculado de verdade, nunca um "0,00" hardcoded que
              não reflete nada — fmtNum não serve aqui pq esconde zero como "", e um saldo
              zerado de verdade também precisa aparecer no placeholder. */}
          <input type="number" value={confirmado} onChange={e => setConfirmado(e.target.value)}
            placeholder={calculado.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} step="0.01"
            style={{ width: 140, padding: '8px 12px', background: '#111', border: '2px solid #34c759', borderRadius: 8, color: '#fff', fontSize: 17, fontWeight: 700, textAlign: 'right' }} />
        </div>
      </div>
      {dif !== null && (
        <div className={`dif-msg ${Math.abs(dif) < 0.01 ? 'val-green' : 'val-red'}`}>
          {Math.abs(dif) < 0.01 ? '✅ Saldo conferido!' : `⚠️ Diferença: ${fmtBRL(Math.abs(dif))}`}
        </div>
      )}
      {msg && <div style={{ marginTop: 8, fontWeight: 600, color: saveSuccess ? '#34c759' : '#ff6b6b' }}>{msg}</div>}
      <button className="btn-save" onClick={handleSave} disabled={saving}>
        {saving ? 'Salvando...' : '☁️ Salvar e sincronizar'}
      </button>
    </>
  )
}
