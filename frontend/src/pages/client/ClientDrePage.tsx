import { useState, useEffect, useCallback, useMemo, useRef } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterDre } from '../../api/metricas'
import { listarContasBancarias } from '../../api/contasBancarias'
import { editarCategoriaLancamento } from '../../api/registros'
import { listarCategorias } from '../../api/categorias'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtPct, fmtDate, todayISO } from '../../utils/format'
import Modal from '../../components/shared/Modal'
import DreKpisPanel from './dre/DreKpisPanel'
import CategoriaCombobox from '../../components/shared/CategoriaCombobox'
import type { Dre, DreBloco } from '../../api/metricas'
import type { ContaBancaria, Bloco, Categorias, CategoriaAdmin } from '../../types'
import './ClientDre.css'

export type TipoPeriodo = 'mes' | 'trimestre' | 'ano'

const MESES = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']

// Cor de identidade de cada bloco — reaproveita a paleta já usada em CORES_GRUPO (dashboard).
const BLOCO_COR: Record<Bloco, string> = {
  'RECEITAS OPERACIONAIS': '#63e6be',
  'DEDUÇÕES DA RECEITA': '#e599f7',
  'CUSTOS OPERACIONAIS': '#ff6b6b',
  'DESPESAS OPERACIONAIS': '#ffa94d',
  'ATIVIDADES DE INVESTIMENTO': '#74c0fc',
  'ATIVIDADES DE FINANCIAMENTO': '#a9e34b',
  'RETIRADAS DOS SÓCIOS': '#f783ac',
}

const BLOCO_PREFIXO: Record<Bloco, string> = {
  'RECEITAS OPERACIONAIS': '',
  'DEDUÇÕES DA RECEITA': '(-) ',
  'CUSTOS OPERACIONAIS': '(-) ',
  'DESPESAS OPERACIONAIS': '(-) ',
  'ATIVIDADES DE INVESTIMENTO': '(±) ',
  'ATIVIDADES DE FINANCIAMENTO': '(±) ',
  'RETIRADAS DOS SÓCIOS': '(-) ',
}

function periodoParaDatas(tipo: TipoPeriodo, ano: number, mes: number): { de: string; ate: string } {
  if (tipo === 'mes') {
    const ultimo = new Date(ano, mes, 0).getDate()
    return {
      de: `${ano}-${String(mes).padStart(2, '0')}-01`,
      ate: `${ano}-${String(mes).padStart(2, '0')}-${ultimo}`,
    }
  }
  if (tipo === 'trimestre') {
    const q = Math.ceil(mes / 3)
    const mesInicio = (q - 1) * 3 + 1
    const mesFim = q * 3
    const ultimoDia = new Date(ano, mesFim, 0).getDate()
    return {
      de: `${ano}-${String(mesInicio).padStart(2, '0')}-01`,
      ate: `${ano}-${String(mesFim).padStart(2, '0')}-${ultimoDia}`,
    }
  }
  return { de: `${ano}-01-01`, ate: `${ano}-12-31` }
}

function labelPeriodo(tipo: TipoPeriodo, ano: number, mes: number): string {
  if (tipo === 'mes') return `${MESES[mes - 1]}/${ano}`
  if (tipo === 'trimestre') return `T${Math.ceil(mes / 3)}/${ano}`
  return `${ano}`
}

/** Desloca (ano, mes) `deslocamento` períodos (do tipo selecionado) para trás. */
function deslocarPeriodo(tipo: TipoPeriodo, ano: number, mes: number, deslocamento: number): { ano: number; mes: number } {
  if (deslocamento === 0) return { ano, mes }
  if (tipo === 'ano') return { ano: ano - deslocamento, mes }
  const passoMeses = tipo === 'trimestre' ? 3 : 1
  const indiceAbsoluto = ano * 12 + (mes - 1) - deslocamento * passoMeses
  return { ano: Math.floor(indiceAbsoluto / 12), mes: (((indiceAbsoluto % 12) + 12) % 12) + 1 }
}

function fmtPctOuTraco(v: number | null): string {
  return v === null ? '—' : fmtPct(v)
}

/** Largura da barra de proporção — participação absoluta sobre a Receita Bruta, sempre 0–100. */
function larguraBarra(percentual: number | null): number {
  if (percentual === null) return 0
  return Math.min(100, Math.abs(percentual))
}

interface Periodo {
  chave: string
  label: string
  de: string
  ate: string
}

interface Drill {
  periodoLabel: string
  de: string
  ate: string
  categoriaNome: string
}

type LinhaTipo = 'bloco' | 'subtotal' | 'vertical'
type VerticalKey = 'receitaFinanceira' | 'despesasNaoOperacionais' | 'naoClassificado'
interface LinhaDre {
  key: string
  tipo: LinhaTipo
  label: string
  bloco?: Bloco
  verticalKey?: VerticalKey
  final?: boolean
}

// Receita Financeira/Despesas Não Operacionais/Não Classificado entram aqui, na mesma ordem da
// fórmula do backend (resultadoOperacional + receitaFinanceira - despesasNaoOperacionais -
// naoClassificado + atividadesInvestimento + atividadesFinanciamento) — sem elas a soma das
// linhas visíveis não batia com o Resultado Líquido (item 1.1). "Não Classificado" nunca é
// omitido mesmo zerado, pro usuário ver de cara que não há lançamento sem categoria no período.
const VERTICAL_COR: Record<VerticalKey, string> = {
  receitaFinanceira: '#63e6be',
  despesasNaoOperacionais: '#ff6b6b',
  naoClassificado: '#ffd43b',
}
const VERTICAL_PREFIXO: Record<VerticalKey, string> = {
  receitaFinanceira: '(+) ',
  despesasNaoOperacionais: '(-) ',
  naoClassificado: '(-) ',
}

const LINHAS: LinhaDre[] = [
  { key: 'RECEITAS OPERACIONAIS', tipo: 'bloco', label: 'Receitas Operacionais', bloco: 'RECEITAS OPERACIONAIS' },
  { key: 'DEDUÇÕES DA RECEITA', tipo: 'bloco', label: 'Deduções da Receita', bloco: 'DEDUÇÕES DA RECEITA' },
  { key: 'CUSTOS OPERACIONAIS', tipo: 'bloco', label: 'Custos Operacionais', bloco: 'CUSTOS OPERACIONAIS' },
  { key: 'margemContribuicao', tipo: 'subtotal', label: 'Margem de Contribuição' },
  { key: 'DESPESAS OPERACIONAIS', tipo: 'bloco', label: 'Despesas Operacionais', bloco: 'DESPESAS OPERACIONAIS' },
  { key: 'resultadoOperacional', tipo: 'subtotal', label: 'Resultado Operacional' },
  { key: 'receitaFinanceira', tipo: 'vertical', label: 'Receita Financeira', verticalKey: 'receitaFinanceira' },
  { key: 'despesasNaoOperacionais', tipo: 'vertical', label: 'Despesas Não Operacionais', verticalKey: 'despesasNaoOperacionais' },
  { key: 'naoClassificado', tipo: 'vertical', label: 'Não Classificados', verticalKey: 'naoClassificado' },
  { key: 'ATIVIDADES DE INVESTIMENTO', tipo: 'bloco', label: 'Atividades de Investimento', bloco: 'ATIVIDADES DE INVESTIMENTO' },
  { key: 'ATIVIDADES DE FINANCIAMENTO', tipo: 'bloco', label: 'Atividades de Financiamento', bloco: 'ATIVIDADES DE FINANCIAMENTO' },
  // Pró-labore/retiradas dos sócios: última dedução antes do Resultado Líquido, depois de
  // Investimento e Financiamento (decisão explícita do usuário) — nunca despesa fixa.
  { key: 'RETIRADAS DOS SÓCIOS', tipo: 'bloco', label: 'Retiradas dos Sócios', bloco: 'RETIRADAS DOS SÓCIOS' },
  { key: 'resultadoLiquido', tipo: 'subtotal', label: 'Resultado Líquido', final: true },
]

function valorEPercentualSubtotal(dre: Dre, key: string): { total: number; percentual: number | null } {
  if (key === 'margemContribuicao') return { total: dre.margemContribuicao, percentual: dre.margemContribuicaoPercentual }
  if (key === 'resultadoOperacional') return { total: dre.resultadoOperacional, percentual: dre.resultadoOperacionalPercentual }
  return { total: dre.resultadoLiquido, percentual: dre.resultadoLiquidoPercentual }
}

// ── Parte 1 (Money Vision): "Comparar períodos" como uma tabela única ──────────────────────────
// Antes, cada bloco expandido empilhava um bloco de grupos/categorias por período (Ago, depois
// Set, depois Out), impossível de comparar lado a lado. Agora cada grupo/categoria é UMA linha
// com uma célula por período, igual Bloco já fazia no topo.
interface ValorPeriodo { total: number; percentual: number | null }
interface LinhaTabelaDre {
  key: string
  nivel: 0 | 1 | 2
  label: string
  cor?: string
  final?: boolean
  expandavel: boolean
  expandido: boolean
  vazio: boolean
  destaqueAlerta?: boolean
  onToggle?: () => void
  onClickCelula?: (periodoIdx: number) => void
  valores: ValorPeriodo[]
}

/** Nomes únicos preservando a ordem do período mais recente primeiro, com os nomes que só
 * existem em períodos mais antigos aparecendo no final — assim uma categoria que saiu de uso
 * recentemente não "pula" pro topo da lista. */
function unionNomes(listas: { nome: string }[][]): string[] {
  const vistos = new Set<string>()
  const ordem: string[] = []
  for (let i = listas.length - 1; i >= 0; i--) {
    for (const item of listas[i]) {
      if (!vistos.has(item.nome)) { vistos.add(item.nome); ordem.push(item.nome) }
    }
  }
  return ordem
}

// Item 3 da Parte 3 (Money Vision): média(valor) ÷ média(receita), não a média dos percentuais —
// a média dos percentuais distorce quando a receita varia muito entre os períodos comparados (ex.:
// um mês com receita baixa e despesa fixa alta pesa demais na média, mesmo a despesa em R$ sendo
// estável). `receitas` é a Receita Bruta de cada período, na mesma ordem/tamanho de `valores`.
function mediaValores(valores: ValorPeriodo[], receitas: number[]): ValorPeriodo {
  const total = valores.reduce((s, v) => s + v.total, 0) / valores.length
  const mediaReceita = receitas.reduce((s, r) => s + r, 0) / receitas.length
  const percentual = mediaReceita > 0 ? (total / mediaReceita) * 100 : null
  return { total, percentual }
}

function diasEntre(deIso: string, ateIso: string): number {
  const de = new Date(deIso + 'T12:00:00')
  const ate = new Date(ateIso + 'T12:00:00')
  return Math.round((ate.getTime() - de.getTime()) / 86400000) + 1
}

// Item 2 da Parte 3 (Money Vision): quando o período mais recente comparado ainda está em
// andamento (ex.: comparando até o mês atual, mas hoje é só dia 10), comparar o valor CHEIO dele
// com o valor cheio do período anterior é injusto — sempre parece uma queda forte só por ter menos
// dias corridos ainda. Em vez disso, poda o período anterior pros MESMOS N dias corridos
// (proporcional, já que o DRE não guarda granularidade diária) e marca a variação como "parcial".
function variacaoValores(
  valores: ValorPeriodo[], periodosInfo: Periodo[], hoje: string,
): { rs: number; pct: number | null; parcial: boolean } | null {
  if (valores.length < 2) return null
  const atual = valores[valores.length - 1].total
  const anteriorCheio = valores[valores.length - 2].total

  const periodoAtual = periodosInfo[periodosInfo.length - 1]
  const periodoAnterior = periodosInfo[periodosInfo.length - 2]
  const diasTotalAtual = diasEntre(periodoAtual.de, periodoAtual.ate)
  const diasElapsed = Math.min(diasTotalAtual, Math.max(1, diasEntre(periodoAtual.de, hoje)))
  const parcial = diasElapsed < diasTotalAtual

  const anterior = parcial
    ? anteriorCheio * (diasElapsed / diasEntre(periodoAnterior.de, periodoAnterior.ate))
    : anteriorCheio

  const rs = atual - anterior
  const pct = anterior !== 0 ? (rs / Math.abs(anterior)) * 100 : null
  return { rs, pct, parcial }
}

export default function ClientDrePage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''
  const { registros, recarregar: recarregarRegistros } = useRegistros(clienteId || null)

  const hoje = new Date()
  const [tipo, setTipo] = useState<TipoPeriodo>('mes')
  const [ano, setAno] = useState(hoje.getFullYear())
  const [mes, setMes] = useState(hoje.getMonth() + 1)
  const [contaFiltro, setContaFiltro] = useState<string>('')
  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [comparar, setComparar] = useState(false)
  const [qtdPeriodos, setQtdPeriodos] = useState(3)

  const [dresPorPeriodo, setDresPorPeriodo] = useState<Record<string, Dre>>({})
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  const [salvandoCategoriaId, setSalvandoCategoriaId] = useState<string | null>(null)
  const [msgCategoria, setMsgCategoria] = useState('')
  const [exportandoPdf, setExportandoPdf] = useState(false)
  const [blocosExpandidos, setBlocosExpandidos] = useState<Set<Bloco>>(new Set())
  const [gruposExpandidos, setGruposExpandidos] = useState<Set<string>>(new Set())
  const [drill, setDrill] = useState<Drill | null>(null)

  useEffect(() => {
    if (!clienteId) return
    listarContasBancarias(clienteId)
      .then(cs => setContas(cs.filter(c => c.ativa)))
      .catch(() => {})
  }, [clienteId])

  useEffect(() => { if (clienteId) listarCategorias(clienteId).then(setCategorias).catch(() => {}) }, [clienteId])

  // Item 1.11: a tela abre no mês atual por padrão — se ele ainda não tem nenhum lançamento
  // (comum logo no início do mês), mostrar um DRE zerado não ajuda ninguém. Pula direto pro
  // último mês que de fato tem movimento. Roda só uma vez, e só se o usuário ainda não tiver
  // navegado pra outro período enquanto os registros carregavam.
  const ajusteInicialRef = useRef(false)
  useEffect(() => {
    if (ajusteInicialRef.current) return
    if (registros.length === 0) return
    ajusteInicialRef.current = true

    const anoHoje = hoje.getFullYear()
    const mesHoje = hoje.getMonth() + 1
    if (ano !== anoHoje || mes !== mesHoje) return

    const temAtividadeNoMesAtual = registros.some(r => {
      const [a, m] = r.data.split('-').map(Number)
      return a === anoHoje && m === mesHoje
    })
    if (temAtividadeNoMesAtual) return

    const maisRecente = registros.reduce((max, r) => (r.data > max ? r.data : max), registros[0].data)
    const [a, m] = maisRecente.split('-').map(Number)
    setAno(a)
    setMes(m)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [registros])

  const periodos = useMemo<Periodo[]>(() => {
    const qtd = comparar ? qtdPeriodos : 1
    return Array.from({ length: qtd }, (_, i) => qtd - 1 - i).map(deslocamento => {
      const { ano: a, mes: m } = deslocarPeriodo(tipo, ano, mes, deslocamento)
      const { de, ate } = periodoParaDatas(tipo, a, m)
      return { chave: `${tipo}-${a}-${m}`, label: labelPeriodo(tipo, a, m), de, ate }
    })
  }, [comparar, qtdPeriodos, tipo, ano, mes])

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true)
    setErro('')
    try {
      const resultados = await Promise.all(
        periodos.map(p => obterDre(clienteId, p.de, p.ate, contaFiltro || undefined))
      )
      const mapa: Record<string, Dre> = {}
      periodos.forEach((p, i) => { mapa[p.chave] = resultados[i] })
      setDresPorPeriodo(mapa)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar DRE.')
    } finally {
      setLoading(false)
    }
  }, [clienteId, periodos, contaFiltro])

  useEffect(() => { carregar() }, [carregar])

  function toggleBloco(bloco: Bloco) {
    setBlocosExpandidos(prev => {
      const next = new Set(prev)
      if (next.has(bloco)) next.delete(bloco)
      else next.add(bloco)
      return next
    })
  }

  function toggleGrupo(chave: string) {
    setGruposExpandidos(prev => {
      const next = new Set(prev)
      if (next.has(chave)) next.delete(chave)
      else next.add(chave)
      return next
    })
  }

  function abrirLancamentos(periodo: Periodo, categoriaNome: string) {
    setDrill({ periodoLabel: periodo.label, de: periodo.de, ate: periodo.ate, categoriaNome })
  }

  interface LinhaDrill {
    id?: string
    contaBancariaId?: string
    tipo: 'Entrada' | 'Saida'
    data: string
    descricao: string
    valor: number
    categoria: string
  }

  const lancamentosDrill = useMemo(() => {
    if (!drill) return []
    const linhas: LinhaDrill[] = []
    for (const r of registros) {
      if (r.data < drill.de || r.data > drill.ate) continue
      if (contaFiltro && r.contaBancariaId !== contaFiltro) continue
      for (const e of r.entradas) {
        const nomeEfetivo = e.categoria && e.categoria.trim() ? e.categoria : 'Não Classificado'
        if (nomeEfetivo === drill.categoriaNome) {
          linhas.push({
            id: e.id, contaBancariaId: r.contaBancariaId, tipo: 'Entrada', data: r.data,
            descricao: e.descricao, valor: e.valor, categoria: e.categoria ?? '',
          })
        }
      }
      for (const s of r.saidas) {
        const nomeEfetivo = s.categoria && s.categoria.trim() ? s.categoria : 'Não Classificado'
        if (nomeEfetivo === drill.categoriaNome) {
          linhas.push({
            id: s.id, contaBancariaId: r.contaBancariaId, tipo: 'Saida', data: r.data,
            descricao: s.descricao, valor: -s.valor, categoria: s.categoria ?? '',
          })
        }
      }
    }
    return linhas.sort((a, b) => a.data.localeCompare(b.data))
  }, [drill, registros, contaFiltro])

  // Item E2: mesma normalização usada pelo backend (DescricaoMatcher.NormalizarExata) — conta
  // "quantos lançamentos parecidos" existem ANTES de perguntar, usando os registros já carregados
  // nesta tela (sem round-trip extra); o número real pós-ação vem da resposta da API.
  function normalizar(descricao: string) {
    return descricao.toUpperCase().trim()
  }

  // Item 1 do Bloco 5 ("Visão de Futuro"): mesmo helper usado em ClientCaixaPage/
  // ClientContaDetalhePage — categoria criada ali no próprio combobox entra na lista local sem
  // precisar recarregar a tela inteira.
  function handleCategoriaCriada(nova: CategoriaAdmin) {
    const item = { nome: nova.nome, tipoCusto: nova.tipo, grupo: nova.grupoNome }
    if (nova.ehEntrada) setCategorias(prev => ({ ...prev, entradas: [...prev.entradas, item] }))
    if (nova.ehSaida) setCategorias(prev => ({ ...prev, saidas: [...prev.saidas, item] }))
  }

  // Item 2 do Bloco 5: "sugerir categoria" — procura, entre os lançamentos JÁ carregados nesta
  // tela, outro com a mesma descrição normalizada que já tem categoria, e propõe ela num clique.
  // Não é uma regra nova (não existe tela de criar regra manualmente) — é reusar uma categoria que
  // o próprio cliente já escolheu antes pra essa mesma descrição.
  function sugerirCategoriaPara(linha: LinhaDrill): string | null {
    const descNorm = normalizar(linha.descricao)
    const candidato = registros
      .flatMap(r => linha.tipo === 'Entrada' ? r.entradas : r.saidas)
      .find(item => item.id !== linha.id && item.categoria?.trim() && normalizar(item.descricao) === descNorm)
    return candidato?.categoria?.trim() || null
  }

  async function handleAlterarCategoria(linha: LinhaDrill, categoriaNova: string) {
    if (!linha.id || !linha.contaBancariaId || categoriaNova === linha.categoria) return
    setMsgCategoria('')

    const descNorm = normalizar(linha.descricao)
    const similares = registros.flatMap(r =>
      linha.tipo === 'Entrada'
        ? r.entradas.filter(e => e.id !== linha.id && !e.categoria?.trim() && normalizar(e.descricao) === descNorm)
        : r.saidas.filter(s => s.id !== linha.id && !s.categoria?.trim() && normalizar(s.descricao) === descNorm),
    )
    const aplicarASimilares = similares.length > 0
      && confirm(`Aplicar "${categoriaNova}" também a ${similares.length} lançamento(s) parecido(s) ainda sem categoria?`)

    setSalvandoCategoriaId(linha.id)
    try {
      const resultado = await editarCategoriaLancamento(clienteId, {
        contaBancariaId: linha.contaBancariaId, lancamentoId: linha.id, tipo: linha.tipo,
        data: linha.data, categoriaNova, aplicarASimilares,
      })
      setMsgCategoria(
        `Categoria atualizada.${resultado.similaresAtualizados > 0 ? ` ${resultado.similaresAtualizados} lançamento(s) parecido(s) também foram atualizados.` : ''}`,
      )
      // "Recalcule a DRE sem recarregar a página" — busca de novo o DRE e os registros (a lista do
      // drill-down em si lê de `registros`), em vez de um reload de verdade.
      await Promise.all([carregar(), recarregarRegistros()])
    } catch (e: unknown) {
      setMsgCategoria(e instanceof Error ? e.message : 'Erro ao atualizar categoria.')
    } finally {
      setSalvandoCategoriaId(null)
    }
  }

  // Item E3: PDF sempre de UM mês (bate com o nome do arquivo, AAAA-MM) — usa o 1º período
  // visível mesmo em modo "trimestre"/"ano" ou comparativo, pra sempre existir um mês de referência.
  async function handleExportarPdf() {
    if (!clienteId) return
    setErro('')
    setExportandoPdf(true)
    try {
      const referencia = periodosComDre[0].periodo.de
      const [anoRef, mesRef] = referencia.split('-')
      const token = localStorage.getItem('token')
      const query = new URLSearchParams({ ano: anoRef, mes: String(Number(mesRef)) })
      if (contaFiltro) query.set('contaBancariaId', contaFiltro)
      const res = await fetch(`/api/export/${clienteId}/dre-pdf?${query.toString()}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      })
      if (!res.ok) throw new Error(`Erro ${res.status}`)

      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `MoneyVision_DRE_${user?.nomeUsuario ?? clienteId}_${anoRef}-${mesRef}.pdf`
      a.click()
      URL.revokeObjectURL(url)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao exportar PDF.')
    } finally {
      setExportandoPdf(false)
    }
  }

  const anosOpcoes = Array.from({ length: 5 }, (_, i) => hoje.getFullYear() - 2 + i)
  const trimestresOpcoes = [1, 2, 3, 4]

  const periodosComDre = periodos.map(p => ({ periodo: p, dre: dresPorPeriodo[p.chave] }))
  const todasCarregadas = periodosComDre.every(p => p.dre)
  const modoComparativo = periodos.length > 1

  function encontrarBloco(dre: Dre, bloco: Bloco): DreBloco {
    return dre.blocos.find(b => b.bloco === bloco) ?? { bloco, total: 0, percentual: 0, grupos: [] }
  }

  // Monta a tabela comparativa: uma linha por Bloco/Subtotal/Vertical/Grupo/Categoria, com uma
  // célula de valor por período — só processa quando "Comparar períodos" está marcado.
  const linhasTabelaComparativa = useMemo<LinhaTabelaDre[]>(() => {
    if (!modoComparativo || !todasCarregadas) return []
    const linhas: LinhaTabelaDre[] = []

    for (const l of LINHAS) {
      if (l.tipo === 'subtotal') {
        const valores = periodosComDre.map(p => valorEPercentualSubtotal(p.dre, l.key))
        linhas.push({ key: l.key, nivel: 0, label: l.label, expandavel: false, expandido: false, vazio: false, valores, final: l.final })
        continue
      }

      if (l.tipo === 'vertical') {
        const vKey = l.verticalKey!
        const linhasPorPeriodo = periodosComDre.map(p => p.dre[vKey])
        const vazio = linhasPorPeriodo.every(x => x.categorias.length === 0)
        const chaveExpand = `vertical::${vKey}`
        const expandido = gruposExpandidos.has(chaveExpand)
        linhas.push({
          key: l.key, nivel: 0, label: `${VERTICAL_PREFIXO[vKey]}${l.label.toUpperCase()}`, cor: VERTICAL_COR[vKey],
          expandavel: !vazio, expandido, vazio, onToggle: () => toggleGrupo(chaveExpand),
          valores: linhasPorPeriodo.map(x => ({ total: x.total, percentual: x.percentual })),
        })
        if (expandido && !vazio) {
          for (const nome of unionNomes(linhasPorPeriodo.map(x => x.categorias))) {
            const valores = linhasPorPeriodo.map(lp => {
              const cat = lp.categorias.find(c => c.nome === nome)
              return cat ? { total: cat.total, percentual: cat.percentual } : { total: 0, percentual: null }
            })
            linhas.push({
              key: `${l.key}::${nome}`, nivel: 2, label: nome, expandavel: false, expandido: false, vazio: false,
              destaqueAlerta: nome === 'Não Classificado', valores,
              onClickCelula: idx => abrirLancamentos(periodosComDre[idx].periodo, nome),
            })
          }
        }
        continue
      }

      const bloco = l.bloco!
      const blocosPorPeriodo = periodosComDre.map(p => encontrarBloco(p.dre, bloco))
      const vazio = blocosPorPeriodo.every(b => b.grupos.length === 0)
      const expandido = blocosExpandidos.has(bloco)
      linhas.push({
        key: l.key, nivel: 0, label: `${BLOCO_PREFIXO[bloco]}${l.label.toUpperCase()}`, cor: BLOCO_COR[bloco],
        expandavel: !vazio, expandido, vazio, onToggle: () => toggleBloco(bloco),
        valores: blocosPorPeriodo.map(b => ({ total: b.total, percentual: b.percentual })),
      })
      if (!expandido || vazio) continue

      for (const nomeGrupo of unionNomes(blocosPorPeriodo.map(b => b.grupos))) {
        const gruposPorPeriodo = blocosPorPeriodo.map(b => b.grupos.find(g => g.nome === nomeGrupo))
        const chaveGrupo = `${bloco}::${nomeGrupo}`
        const grupoExpandido = gruposExpandidos.has(chaveGrupo)
        linhas.push({
          key: chaveGrupo, nivel: 1, label: nomeGrupo, cor: BLOCO_COR[bloco],
          expandavel: true, expandido: grupoExpandido, vazio: false, onToggle: () => toggleGrupo(chaveGrupo),
          valores: gruposPorPeriodo.map(g => g ? { total: g.total, percentual: g.percentual } : { total: 0, percentual: null }),
        })
        if (!grupoExpandido) continue

        for (const nomeCat of unionNomes(gruposPorPeriodo.map(g => g?.categorias ?? []))) {
          const valores = gruposPorPeriodo.map(g => {
            const cat = g?.categorias.find(c => c.nome === nomeCat)
            return cat ? { total: cat.total, percentual: cat.percentual } : { total: 0, percentual: null }
          })
          linhas.push({
            key: `${chaveGrupo}::${nomeCat}`, nivel: 2, label: nomeCat, expandavel: false, expandido: false, vazio: false,
            destaqueAlerta: nomeCat === 'Não Classificado', valores,
            onClickCelula: idx => abrirLancamentos(periodosComDre[idx].periodo, nomeCat),
          })
        }
      }
    }
    return linhas
  }, [modoComparativo, todasCarregadas, periodosComDre, blocosExpandidos, gruposExpandidos])

  // Mobile (item 3): só 2 períodos de cada vez, com setas pra navegar — a tabela inteira (todas
  // as colunas) continua existindo, só a JANELA visível de colunas muda.
  const [janelaMobile, setJanelaMobile] = useState(0)
  const [telaEstreita, setTelaEstreita] = useState(() => typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia('(max-width: 480px)').matches)
  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return
    const mq = window.matchMedia('(max-width: 480px)')
    const handler = () => setTelaEstreita(mq.matches)
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [])
  const usaJanelaMobile = telaEstreita && modoComparativo && periodosComDre.length > 2
  const janelaInicio = usaJanelaMobile ? Math.min(janelaMobile, periodosComDre.length - 2) : 0
  const indicesVisiveis = usaJanelaMobile ? [janelaInicio, janelaInicio + 1] : periodosComDre.map((_, i) => i)

  const naoClassificadoTotal = todasCarregadas
    ? periodosComDre.reduce((acc, p) => {
        const soma = p.dre.blocos
          .flatMap(b => b.grupos)
          .filter(g => g.nome === 'Não Classificado')
          .reduce((s, g) => s + Math.abs(g.total), 0)
        return acc + soma
      }, 0)
    : 0

  return (
    <>
      <div className="dre-header">
        <div>
          <h2 className="dre-titulo">📑 Demonstrativo de Resultado (DRE)</h2>
          <div className="dre-subtitulo">
            Bloco → Grupo → Categoria · % sobre Receita Operacional (base = 100%)
          </div>
        </div>
        <button className="btn-cancel" onClick={handleExportarPdf} disabled={exportandoPdf || !todasCarregadas}>
          {exportandoPdf ? '⏳ Gerando...' : '📄 Exportar PDF'}
        </button>
      </div>

      <div className="dre-controles">
        <div className="dre-tipo-tabs">
          {(['mes', 'trimestre', 'ano'] as TipoPeriodo[]).map(t => (
            <button
              key={t}
              className={`dre-tipo-btn${tipo === t ? ' active' : ''}`}
              onClick={() => setTipo(t)}
            >
              {t === 'mes' ? 'Mês' : t === 'trimestre' ? 'Trimestre' : 'Ano'}
            </button>
          ))}
        </div>

        <div className="dre-filtros">
          <select className="dre-select" value={ano} onChange={e => setAno(+e.target.value)}>
            {anosOpcoes.map(a => <option key={a} value={a}>{a}</option>)}
          </select>

          {tipo === 'mes' && (
            <select className="dre-select" value={mes} onChange={e => setMes(+e.target.value)}>
              {MESES.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
            </select>
          )}

          {tipo === 'trimestre' && (
            <select className="dre-select" value={Math.ceil(mes / 3)} onChange={e => setMes((+e.target.value - 1) * 3 + 1)}>
              {trimestresOpcoes.map(q => <option key={q} value={q}>T{q}</option>)}
            </select>
          )}

          {contas.length > 1 && (
            <select className="dre-select" value={contaFiltro} onChange={e => setContaFiltro(e.target.value)}>
              <option value="">Todas as contas</option>
              {contas.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          )}
        </div>

        <div className="dre-comparar">
          <label className="dre-comparar-toggle">
            <input type="checkbox" checked={comparar} onChange={e => setComparar(e.target.checked)} />
            Comparar períodos
          </label>
          {comparar && (
            <select className="dre-select" value={qtdPeriodos} onChange={e => setQtdPeriodos(+e.target.value)}>
              {[2, 3, 4, 6].map(n => <option key={n} value={n}>Últimos {n}</option>)}
            </select>
          )}
        </div>
      </div>

      {loading && <p className="dre-loading">Calculando...</p>}
      {erro && <p className="dre-erro">{erro}</p>}

      {todasCarregadas && !loading && naoClassificadoTotal > 0 && (
        <div className="dre-alerta-global">
          ⚠ Há lançamentos <strong>Não Classificados</strong> somando {fmtBRL(naoClassificadoTotal)} no período — categorize-os em "Categorizar Lançamentos" para um DRE mais preciso.
        </div>
      )}

      {todasCarregadas && !loading && modoComparativo && (() => {
        const colTemplate = `minmax(190px,1.6fr) repeat(${indicesVisiveis.length}, minmax(100px,1fr))${telaEstreita ? '' : ' minmax(100px,1fr) minmax(100px,1fr)'}`
        return (
        <div className="dre-tabela-wrap">
          {usaJanelaMobile && (
            <div className="dre-tabela-nav">
              <button
                type="button" className="dre-tabela-nav-btn"
                disabled={janelaInicio === 0}
                onClick={() => setJanelaMobile(Math.max(0, janelaInicio - 1))}
              >‹</button>
              <span className="dre-tabela-nav-label">
                {indicesVisiveis.map(i => periodosComDre[i].periodo.label).join(' · ')}
              </span>
              <button
                type="button" className="dre-tabela-nav-btn"
                disabled={janelaInicio >= periodosComDre.length - 2}
                onClick={() => setJanelaMobile(Math.min(periodosComDre.length - 2, janelaInicio + 1))}
              >›</button>
            </div>
          )}
          <div className="dre-tabela-scroll">
            <div className="dre-tabela-cabecalho" style={{ gridTemplateColumns: colTemplate }}>
              <span className="dre-tabela-label-cel" />
              {indicesVisiveis.map(i => <span key={periodosComDre[i].periodo.chave} className="dre-tabela-th">{periodosComDre[i].periodo.label}</span>)}
              {!telaEstreita && <span className="dre-tabela-th dre-tabela-var-col">Variação</span>}
              {!telaEstreita && <span className="dre-tabela-th dre-tabela-media-col">Média</span>}
            </div>

            {linhasTabelaComparativa.map((linha, idx) => {
              const variacao = variacaoValores(linha.valores, periodosComDre.map(p => p.periodo), todayISO())
              const media = mediaValores(linha.valores, periodosComDre.map(p => p.dre.receitaBruta))
              const zebra = linha.nivel > 0 && idx % 2 === 1
              const corTexto = linha.nivel === 0 ? linha.cor : undefined
              return (
                <div
                  key={linha.key}
                  className={`dre-tabela-linha dre-tabela-nivel-${linha.nivel}${linha.final ? ' dre-tabela-final' : ''}${zebra ? ' dre-tabela-zebra' : ''}${linha.vazio ? ' dre-tabela-vazia' : ''}`}
                  style={{ gridTemplateColumns: colTemplate }}
                >
                  <span
                    className="dre-tabela-label-cel"
                    style={linha.nivel === 0 ? { borderLeft: `4px solid ${linha.cor ?? 'transparent'}` } : undefined}
                    onClick={linha.expandavel ? linha.onToggle : undefined}
                    role={linha.expandavel ? 'button' : undefined}
                  >
                    {linha.expandavel && <span className="dre-expand">{linha.expandido ? '▾' : '▸'}</span>}
                    <span
                      className="dre-tabela-nome"
                      style={{ color: linha.destaqueAlerta ? 'var(--warning)' : corTexto }}
                    >
                      {linha.label}
                    </span>
                  </span>
                  {indicesVisiveis.map(i => {
                    const v = linha.valores[i]
                    const clicavel = !!linha.onClickCelula
                    return (
                      <span
                        key={periodosComDre[i].periodo.chave}
                        className={`dre-tabela-cel${clicavel ? ' dre-tabela-cel-clicavel' : ''}`}
                        onClick={clicavel ? () => linha.onClickCelula!(i) : undefined}
                        role={clicavel ? 'button' : undefined}
                      >
                        <span className="dre-tabela-cel-valor" style={linha.final ? { color: v.total >= 0 ? 'var(--success)' : 'var(--danger)' } : undefined}>{fmtBRL(v.total)}</span>
                        <span className="dre-tabela-cel-pct">{fmtPctOuTraco(v.percentual)}</span>
                      </span>
                    )
                  })}
                  {!telaEstreita && (
                    <span className="dre-tabela-cel dre-tabela-var-col">
                      {variacao ? (
                        <>
                          <span className="dre-tabela-cel-valor" style={{ color: variacao.rs >= 0 ? 'var(--success)' : 'var(--danger)' }}>
                            {fmtBRL(variacao.rs)}
                            {variacao.parcial && (
                              <span className="dre-tabela-selo-parcial" title="Período mais recente ainda em andamento — comparado com os mesmos dias do período anterior.">
                                parcial
                              </span>
                            )}
                          </span>
                          <span className="dre-tabela-cel-pct" style={{ color: variacao.rs >= 0 ? 'var(--success)' : 'var(--danger)' }}>{variacao.pct === null ? '—' : fmtPct(variacao.pct)}</span>
                        </>
                      ) : <span className="dre-tabela-cel-pct">—</span>}
                    </span>
                  )}
                  {!telaEstreita && (
                    <span className="dre-tabela-cel dre-tabela-media-col">
                      <span className="dre-tabela-cel-valor">{fmtBRL(media.total)}</span>
                      <span className="dre-tabela-cel-pct">{fmtPctOuTraco(media.percentual)}</span>
                    </span>
                  )}
                </div>
              )
            })}
          </div>
        </div>
        )
      })()}

      {todasCarregadas && !loading && !modoComparativo && (
      <div className="dre-layout">
        <div className="dre-corpo">
          {LINHAS.map(linha => {
            if (linha.tipo === 'subtotal') {
              const { total, percentual } = valorEPercentualSubtotal(periodosComDre[0].dre, linha.key)
              const corPositivoNegativo = total >= 0 ? 'var(--success)' : 'var(--danger)'
              return (
                <div key={linha.key} className={`dre-linha dre-linha-subtotal${linha.final ? ' dre-linha-final' : ''}`}>
                  <span className="dre-linha-label">{linha.label}</span>
                  <span className="dre-linha-valor-wrap">
                    <span className="dre-linha-valor" style={linha.final ? { color: corPositivoNegativo } : undefined}>{fmtBRL(total)}</span>
                    <span className="dre-linha-pct">{fmtPctOuTraco(percentual)}</span>
                  </span>
                </div>
              )
            }

            if (linha.tipo === 'vertical') {
              const vKey = linha.verticalKey!
              const cor = VERTICAL_COR[vKey]
              const l = periodosComDre[0].dre[vKey]
              const chaveExpand = `vertical::${vKey}`
              const expandido = gruposExpandidos.has(chaveExpand)
              const vazio = l.categorias.length === 0

              return (
                <div key={linha.key} className="dre-bloco-wrap">
                  <div
                    className={`dre-linha dre-linha-bloco${vazio ? ' dre-linha-vazia' : ''}`}
                    style={{ borderLeft: `4px solid ${cor}` }}
                    onClick={vazio ? undefined : () => toggleGrupo(chaveExpand)}
                    role={vazio ? undefined : 'button'}
                  >
                    <span className="dre-linha-label">
                      {!vazio && <span className="dre-expand">{expandido ? '▾' : '▸'}</span>}
                      <span className="dre-bloco-nome" style={{ color: cor }}>{VERTICAL_PREFIXO[vKey]}{linha.label.toUpperCase()}</span>
                    </span>
                    <span className="dre-linha-valor">{fmtBRL(l.total)}</span>
                    <span className="dre-linha-pct">{fmtPctOuTraco(l.percentual)}</span>
                  </div>

                  {expandido && !vazio && (
                    <div className="dre-expand-bloco">
                      <div className="dre-cat-lista">
                        {l.categorias.map(cat => (
                          <button
                            key={cat.nome}
                            className={`dre-cat-btn${cat.nome === 'Não Classificado' ? ' dre-cat-btn-alerta' : ''}`}
                            onClick={() => abrirLancamentos(periodosComDre[0].periodo, cat.nome)}
                          >
                            <span className="dre-cat-nome">{cat.nome}</span>
                            <span className="dre-cat-valor">{fmtBRL(cat.total)}</span>
                            <span className="dre-cat-pct">{fmtPctOuTraco(cat.percentual)}</span>
                          </button>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )
            }

            const bloco = linha.bloco!
            const cor = BLOCO_COR[bloco]
            const b = encontrarBloco(periodosComDre[0].dre, bloco)
            const expandido = blocosExpandidos.has(bloco)
            const vazio = b.grupos.length === 0

            return (
              <div key={linha.key} className="dre-bloco-wrap">
                <div
                  className={`dre-linha dre-linha-bloco${vazio ? ' dre-linha-vazia' : ''}`}
                  style={{ borderLeft: `4px solid ${cor}` }}
                  onClick={vazio ? undefined : () => toggleBloco(bloco)}
                  role={vazio ? undefined : 'button'}
                >
                  <span className="dre-linha-label">
                    {!vazio && <span className="dre-expand">{expandido ? '▾' : '▸'}</span>}
                    <span className="dre-bloco-nome" style={{ color: cor }}>{BLOCO_PREFIXO[bloco]}{linha.label.toUpperCase()}</span>
                  </span>
                  <span className="dre-linha-valor">{fmtBRL(b.total)}</span>
                  <span className="dre-linha-pct">{fmtPctOuTraco(b.percentual)}</span>
                </div>
                <div className="dre-barra-trilho">
                  <div className="dre-barra-preenchida" style={{ width: `${larguraBarra(b.percentual)}%`, background: cor }} />
                </div>

                {expandido && !vazio && (
                  <div className="dre-expand-bloco">
                    {b.grupos.map(grupo => {
                      const chaveGrupo = `${bloco}::${grupo.nome}`
                      const grupoAberto = gruposExpandidos.has(chaveGrupo)
                      return (
                        <div key={grupo.nome} className="dre-grupo-wrap">
                          <button type="button" className="dre-grupo-linha" onClick={() => toggleGrupo(chaveGrupo)}>
                            <span className="dre-expand dre-expand-grupo">{grupoAberto ? '▾' : '▸'}</span>
                            <span className="dre-grupo-nome">{grupo.nome}</span>
                            <span className="dre-grupo-valor">{fmtBRL(grupo.total)}</span>
                            <span className="dre-grupo-pct">{fmtPctOuTraco(grupo.percentual)}</span>
                          </button>
                          <div className="dre-barra-trilho dre-barra-trilho-grupo">
                            <div className="dre-barra-preenchida" style={{ width: `${larguraBarra(grupo.percentual)}%`, background: cor, opacity: 0.6 }} />
                          </div>
                          {grupoAberto && (
                            <div className="dre-cat-lista">
                              {grupo.categorias.map(cat => (
                                <button
                                  key={cat.nome}
                                  className={`dre-cat-btn${cat.nome === 'Não Classificado' ? ' dre-cat-btn-alerta' : ''}`}
                                  onClick={() => abrirLancamentos(periodosComDre[0].periodo, cat.nome)}
                                >
                                  <span className="dre-cat-nome">{cat.nome}</span>
                                  <span className="dre-cat-valor">{fmtBRL(cat.total)}</span>
                                  <span className="dre-cat-pct">{fmtPctOuTraco(cat.percentual)}</span>
                                </button>
                              ))}
                            </div>
                          )}
                        </div>
                      )
                    })}
                  </div>
                )}
              </div>
            )
          })}
        </div>

        <DreKpisPanel
          dre={periodosComDre[0].dre}
          tipoPeriodo={tipo}
          registros={registros}
          de={periodosComDre[0].periodo.de}
          ate={periodosComDre[0].periodo.ate}
          contaFiltro={contaFiltro}
        />
      </div>
      )}

      <Modal
        open={!!drill}
        title={drill ? `${drill.categoriaNome} · ${drill.periodoLabel}` : ''}
        onClose={() => setDrill(null)}
      >
        {msgCategoria && <p className="dre-drill-msg">{msgCategoria}</p>}
        {lancamentosDrill.length === 0 ? (
          <p className="dre-vazio-inline">Nenhum lançamento encontrado.</p>
        ) : (
          <div className="dre-drill-lista">
            {lancamentosDrill.map((l, i) => {
              const opcoes = l.tipo === 'Entrada' ? categorias.entradas : categorias.saidas
              const sugestao = !l.categoria ? sugerirCategoriaPara(l) : null
              return (
                <div key={i} className="dre-drill-item-wrap">
                  <div className="dre-drill-item">
                    <span className="dre-drill-data">{fmtDate(l.data)}</span>
                    <span className="dre-drill-desc">{l.descricao}</span>
                    <span className="dre-drill-valor" style={{ color: l.valor >= 0 ? 'var(--success)' : 'var(--danger)' }}>{fmtBRL(l.valor)}</span>
                  </div>
                  {l.id && l.contaBancariaId ? (
                    <>
                      <CategoriaCombobox
                        clienteId={clienteId}
                        categorias={opcoes}
                        value={l.categoria}
                        onChange={nome => handleAlterarCategoria(l, nome)}
                        onCategoriaCriada={handleCategoriaCriada}
                        blocoPadraoNovaCategoria={l.tipo === 'Entrada' ? 'RECEITAS OPERACIONAIS' : 'DESPESAS OPERACIONAIS'}
                        placeholder="Não Classificado"
                      />
                      {sugestao && (
                        <button
                          type="button" className="dre-drill-sugerir-link"
                          disabled={salvandoCategoriaId === l.id}
                          onClick={() => handleAlterarCategoria(l, sugestao)}
                        >
                          💡 sugerir "{sugestao}"
                        </button>
                      )}
                    </>
                  ) : (
                    <span className="dre-drill-cat-readonly" title="Lançamento antigo — não pode ser reclassificado por aqui.">
                      {l.categoria || 'Não Classificado'}
                    </span>
                  )}
                </div>
              )
            })}
          </div>
        )}
      </Modal>
    </>
  )
}
