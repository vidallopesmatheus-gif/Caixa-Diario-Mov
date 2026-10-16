import { useState, useEffect, useCallback, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterProjecao, obterTrajetoria } from '../../api/projecao'
import { obterPreferenciasFuturo } from '../../api/preferenciasFuturo'
import { listarContasBancarias } from '../../api/contasBancarias'
import { listarCategorias } from '../../api/categorias'
import { listarSugestoesRecorrencia, ignorarSugestaoRecorrencia, criarContaRecorrente, type SugestaoRecorrencia } from '../../api/contasRecorrentes'
import { fmtBRL, fmtBRLCompacto, fmtLidoEm, todayISO } from '../../utils/format'
import Modal from '../../components/shared/Modal'
import { MvPage, MvHero, MvSectionLabel, MvKpi, MvKpiGrid, MvTabs, MvInsight, MvDetails, prefereMovimentoReduzido } from '../../components/mv'
import type { Projecao, ProjecaoDia, ProjecaoItem, Trajetoria, Cenario } from '../../api/projecao'
import type { ContaBancaria, Categorias } from '../../types'
import {
  AreaChart, Area, CartesianGrid, XAxis, YAxis, Tooltip,
  ResponsiveContainer, ReferenceLine, ReferenceArea,
} from 'recharts'
import './ClientProjecao.css'

// 'trajetoria' é uma visão diferente (histórico + projeção, resolução mensal), não um raio a mais
// da mesma projeção diária — por isso vira uma 5ª aba mutuamente exclusiva com 30/60/90, e não um
// segundo controle independente: não faz sentido combinar "60 dias" com "6 meses de histórico" ao
// mesmo tempo, são dois jeitos de olhar o caixa (operacional de curto prazo vs. tendência).
type Janela = 30 | 60 | 90 | 'trajetoria'

const MESES_ABREV = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']

function fmtDataCurta(iso: string): string {
  const d = new Date(iso + 'T12:00:00')
  return `${String(d.getDate()).padStart(2,'0')}/${MESES_ABREV[d.getMonth()]}`
}

function fmtDataCompleta(iso: string): string {
  const d = new Date(iso + 'T12:00:00')
  return `${String(d.getDate()).padStart(2,'0')}/${String(d.getMonth()+1).padStart(2,'0')}/${d.getFullYear()}`
}

function fmtNum(n: number) {
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  const n = parseFloat(s.replace(/\./g, '').replace(',', '.'))
  return Number.isFinite(n) ? n : 0
}

// Agrupa dias por semana ISO para a lista de detalhes (evita 90 linhas abertas)
function agruparPorSemana(dias: ProjecaoDia[]): { label: string; dias: ProjecaoDia[] }[] {
  const grupos: Record<string, ProjecaoDia[]> = {}
  dias.forEach(dia => {
    const d = new Date(dia.data + 'T12:00:00')
    // Usa a segunda-feira da semana como chave
    const dow = d.getDay() === 0 ? 6 : d.getDay() - 1
    const seg = new Date(d); seg.setDate(d.getDate() - dow)
    const key = seg.toISOString().slice(0, 10)
    if (!grupos[key]) grupos[key] = []
    grupos[key].push(dia)
  })
  return Object.entries(grupos)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([key, ds]) => ({
      label: `Semana de ${fmtDataCompleta(key)}`,
      dias: ds,
    }))
}

// Redesign visual (Money Vision): um dia entra como "movimento grande" se sua soma de
// entradas+saídas estiver no topo 20% dos dias com movimento do período (ou todos, se houver
// poucos) — usado pros marcadores no gráfico protagonista.
function limiarMovimentoGrande(dias: ProjecaoDia[]): number {
  const somas = dias
    .map(d => d.totalEntradas + d.totalSaidas)
    .filter(s => s > 0)
    .sort((a, b) => a - b)
  if (somas.length <= 3) return somas[0] ?? Infinity
  return somas[Math.floor(somas.length * 0.8)]
}

interface PontoGrafico {
  data: string
  dataCompleta: string
  saldo: number
  faixa: [number, number]
  totalEntradas: number
  totalSaidas: number
  maioresEntradas: ProjecaoItem[]
  maioresSaidas: ProjecaoItem[]
  grande: boolean
}

export default function ClientProjecaoPage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''

  const [janela, setJanela] = useState<Janela>(30)
  const [contaFiltro, setContaFiltro] = useState('')
  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [projecao, setProjecao] = useState<Projecao | null>(null)
  const [projecaoAlt, setProjecaoAlt] = useState<Projecao | null>(null)
  const [trajetoria, setTrajetoria] = useState<Trajetoria | null>(null)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')
  const [expandidos, setExpandidos] = useState<Record<string, boolean>>({})

  // Bloco 2 da rodada "Visão de Futuro": cenário e reserva de investimento valem pra qualquer
  // janela (30/60/90 ou trajetória) — por isso ficam fora do tipo Janela, como filtros à parte.
  const [cenario, setCenario] = useState<Cenario>('Provavel')
  const [incluirReserva, setIncluirReserva] = useState(false)
  // Item 5 (rodada de ajustes Money Vision): sugestão por FAVORECIDO identificado (descrição
  // normalizada), não mais por categoria — dentro de uma categoria como "Salários/Folha" há vários
  // favorecidos diferentes, cada um sua própria recorrência. Reaproveita o mesmo motor de detecção
  // já usado em Contas ("🔁 Parece uma conta recorrente?"), só filtrado a Pagar.
  const [sugestoesPagar, setSugestoesPagar] = useState<SugestaoRecorrencia[]>([])
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  const [sugestaoAlvo, setSugestaoAlvo] = useState<SugestaoRecorrencia | null>(null)
  const [formCategoria, setFormCategoria] = useState('')
  const [formValorDisplay, setFormValorDisplay] = useState('')
  const [formValor, setFormValor] = useState(0)
  const [formDiaVencimento, setFormDiaVencimento] = useState('')
  const [formContaId, setFormContaId] = useState('')
  const [formValorVariavel, setFormValorVariavel] = useState(false)
  const [salvandoSugestao, setSalvandoSugestao] = useState(false)
  const [ignorandoChave, setIgnorandoChave] = useState<string | null>(null)
  // Redesign visual (Money Vision): reserva mínima já configurada em "Meu Futuro" — reaproveitada
  // aqui só como referência visual no gráfico (linha + área tingida), nunca entra em nenhum cálculo
  // desta tela.
  const [reservaMinima, setReservaMinima] = useState<number | null>(null)

  useEffect(() => {
    if (!clienteId) return
    listarContasBancarias(clienteId)
      .then(cs => setContas(cs.filter(c => c.ativa)))
      .catch(() => {})
    listarSugestoesRecorrencia(clienteId)
      .then(s => setSugestoesPagar(s.filter(x => x.tipo === 'Pagar')))
      .catch(() => {})
    listarCategorias(clienteId).then(setCategorias).catch(() => {})
    obterPreferenciasFuturo(clienteId).then(p => setReservaMinima(p.reservaMinima ?? null)).catch(() => {})
  }, [clienteId])

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true); setErro('')
    try {
      const opcoes = { contaBancariaId: contaFiltro || undefined, cenario, incluirReserva }
      if (janela === 'trajetoria') {
        setTrajetoria(await obterTrajetoria(clienteId, 6, 6, opcoes))
      } else {
        const cenarioAlt: Cenario = cenario === 'Provavel' ? 'Conservador' : 'Provavel'
        const [data, dataAlt] = await Promise.all([
          obterProjecao(clienteId, janela, opcoes),
          obterProjecao(clienteId, janela, { ...opcoes, cenario: cenarioAlt }),
        ])
        setProjecao(data)
        setProjecaoAlt(dataAlt)
        setExpandidos({})
      }
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar projeção.')
    } finally {
      setLoading(false)
    }
  }, [clienteId, janela, contaFiltro, cenario, incluirReserva])

  useEffect(() => { carregar() }, [carregar])

  // Item 5.2: abre o formulário de recorrente já preenchido com descrição = favorecido, categoria,
  // valor médio, dia típico, conta bancária e "valor variável" marcado se houver variação — o
  // cliente só confirma, nunca cadastra do zero.
  function abrirSugestaoPagar(s: SugestaoRecorrencia) {
    const contaPadrao = contas.find(c => c.id === s.contaBancariaId) ?? contas.find(c => c.tipo === 'Caixa') ?? contas[0]
    setSugestaoAlvo(s)
    setFormCategoria(s.categoria ?? '')
    setFormValor(s.valorMedio)
    setFormValorDisplay(fmtNum(s.valorMedio))
    setFormDiaVencimento(String(s.diaTipico))
    setFormContaId(contaPadrao?.id ?? '')
    setFormValorVariavel(s.valorVariavel)
  }

  async function confirmarCriarRecorrentePagar() {
    if (!sugestaoAlvo || !clienteId || !formContaId || !formCategoria) return
    setSalvandoSugestao(true)
    try {
      await criarContaRecorrente({
        clienteId, descricao: sugestaoAlvo.descricao, valor: formValor, categoria: formCategoria,
        tipo: 'Pagar', dataInicio: todayISO(), periodicidade: 'Mensal',
        contaBancariaId: formContaId, valorVariavel: formValorVariavel,
        diaVencimento: formDiaVencimento ? Number(formDiaVencimento) : undefined,
      })
      setSugestoesPagar(prev => prev.filter(s => s.chave !== sugestaoAlvo.chave))
      setSugestaoAlvo(null)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao criar conta recorrente.')
    } finally {
      setSalvandoSugestao(false)
    }
  }

  // Item 5.4: "Não sugerir mais" vale por favorecido (a chave já é tipo+descrição normalizada).
  async function ignorarSugestaoPagar(s: SugestaoRecorrencia) {
    if (!clienteId) return
    setIgnorandoChave(s.chave)
    try {
      await ignorarSugestaoRecorrencia(clienteId, s.chave)
      setSugestoesPagar(prev => prev.filter(x => x.chave !== s.chave))
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao ignorar sugestão.')
    } finally {
      setIgnorandoChave(null)
    }
  }

  // Item 5.3: agrupamento visual por categoria (cabeçalho "Salários/Folha" com os favorecidos
  // embaixo) — a ação continua sempre por favorecido.
  const sugestoesPagarPorCategoria = useMemo(() => {
    const grupos = new Map<string, SugestaoRecorrencia[]>()
    for (const s of sugestoesPagar) {
      const cat = s.categoria ?? 'Sem categoria'
      if (!grupos.has(cat)) grupos.set(cat, [])
      grupos.get(cat)!.push(s)
    }
    return [...grupos.entries()].sort(([, a], [, b]) => b.length - a.length)
  }, [sugestoesPagar])

  // Dados para o gráfico protagonista — amostra a cada N dias (pra não sobrecarregar em 60/90),
  // mas nunca descarta um dia de "movimento grande" mesmo que caia fora do passo de amostragem.
  const dadosGrafico = useMemo<PontoGrafico[]>(() => {
    if (!projecao) return []
    const passo = janela === 30 ? 1 : janela === 60 ? 2 : 3
    const limiar = limiarMovimentoGrande(projecao.dias)
    const diasAlt = projecaoAlt?.dias ?? []
    return projecao.dias
      .map((d, i) => ({ d, i, grande: (d.totalEntradas + d.totalSaidas) >= limiar && (d.totalEntradas + d.totalSaidas) > 0 }))
      .filter(({ i, grande }) => i % passo === 0 || i === projecao.dias.length - 1 || grande)
      .map(({ d, i, grande }) => {
        const saldoAlt = diasAlt[i]?.saldoFim ?? d.saldoFim
        return {
          data: fmtDataCurta(d.data),
          dataCompleta: fmtDataCompleta(d.data),
          saldo: d.saldoFim,
          faixa: [Math.min(d.saldoFim, saldoAlt), Math.max(d.saldoFim, saldoAlt)] as [number, number],
          totalEntradas: d.totalEntradas,
          totalSaidas: d.totalSaidas,
          maioresEntradas: [...d.entradas].sort((a, b) => b.valor - a.valor).slice(0, 2),
          maioresSaidas: [...d.saidas].sort((a, b) => b.valor - a.valor).slice(0, 2),
          grande,
        }
      })
  }, [projecao, projecaoAlt, janela])

  const dominioY = useMemo(() => {
    if (dadosGrafico.length === 0) return undefined
    const valores = dadosGrafico.flatMap(p => [p.faixa[0], p.faixa[1]])
    if (reservaMinima !== null) valores.push(reservaMinima)
    const min = Math.min(...valores)
    const max = Math.max(...valores)
    const margem = Math.max((max - min) * 0.08, 1)
    return [min - margem, max + margem] as [number, number]
  }, [dadosGrafico, reservaMinima])

  const diasComMovimento = useMemo(
    () => projecao?.dias.filter(d => d.entradas.length > 0 || d.saidas.length > 0) ?? [],
    [projecao],
  )

  // Redesign visual (Money Vision): "Próximos 7 dias" como timeline vertical — os primeiros 7 dias
  // do período, com ou sem movimento, pra responder "o que vem por aí" de forma escaneável.
  const proximosSete = useMemo(() => projecao?.dias.slice(0, 7) ?? [], [projecao])

  const semanas = useMemo(
    () => typeof janela === 'number' && janela > 30 ? agruparPorSemana(diasComMovimento) : null,
    [diasComMovimento, janela],
  )

  // Um único array pro gráfico, com o ponto do mês atual presente nas duas séries (realizado E
  // projetado) — é o que faz as duas linhas se encontrarem visualmente em "hoje", sem buraco.
  const mesLabel = (mes: string) => {
    const [ano, m] = mes.split('-')
    return `${MESES_ABREV[Number(m) - 1]}/${ano.slice(2)}`
  }
  const dadosTrajetoria = useMemo(() => {
    if (!trajetoria) return []
    const pontos = [
      ...trajetoria.historico.map(p => ({ mes: mesLabel(p.mes), saldoRealizado: p.saldo, saldoProjetado: undefined as number | undefined })),
      ...trajetoria.projetado.map(p => ({ mes: mesLabel(p.mes), saldoRealizado: undefined as number | undefined, saldoProjetado: p.saldo })),
    ]
    // Ponte no "hoje": o último ponto do histórico também entra como início da série projetada.
    const ultimoHistorico = trajetoria.historico.at(-1)
    if (ultimoHistorico && trajetoria.projetado.length > 0) {
      const idx = pontos.findIndex(p => p.mes === mesLabel(ultimoHistorico.mes))
      if (idx >= 0) pontos[idx] = { ...pontos[idx], saldoProjetado: ultimoHistorico.saldo }
    }
    return pontos
  }, [trajetoria])
  const mesHojeLabel = trajetoria?.historico.at(-1) ? mesLabel(trajetoria.historico.at(-1)!.mes) : undefined
  const historicoCurto = (trajetoria?.mesesHistoricoDisponiveis ?? 6) < 3

  const temNegativo = projecao?.dias.some(d => d.saldoNegativo) ?? false
  const diasNegativos = projecao?.dias.filter(d => d.saldoNegativo).length ?? 0
  const menorSaldoDia = projecao ? projecao.dias.reduce((pior, d) => d.saldoFim < pior.saldoFim ? d : pior, projecao.dias[0]) : undefined
  const menorSaldo = menorSaldoDia?.saldoFim ?? 0
  const totalEntradasPeriodo = projecao?.dias.reduce((s, d) => s + d.totalEntradas, 0) ?? 0
  const totalSaidasPeriodo = projecao?.dias.reduce((s, d) => s + d.totalSaidas, 0) ?? 0
  const saldoFinal = projecao?.dias.at(-1)?.saldoFim ?? projecao?.saldoAtual ?? 0

  function toggleDia(key: string) {
    setExpandidos(prev => ({ ...prev, [key]: !prev[key] }))
  }

  function tooltipGrafico(props: { active?: boolean; payload?: readonly { payload?: PontoGrafico }[] }) {
    const { active, payload } = props
    if (!active || !payload || payload.length === 0) return null
    const p = payload[0]?.payload
    if (!p) return null
    return (
      <div className="mv-chart-tooltip">
        <div className="mv-chart-tooltip-label">{p.dataCompleta}</div>
        <div className="mv-chart-tooltip-row"><span>Saldo projetado</span><strong>{fmtBRL(p.saldo)}</strong></div>
        {p.totalEntradas > 0 && (
          <div className="mv-chart-tooltip-row"><span style={{ color: 'var(--success)' }}>Entradas</span><strong>+{fmtBRL(p.totalEntradas)}</strong></div>
        )}
        {p.maioresEntradas.map((e, i) => (
          <div key={`e${i}`} className="mv-chart-tooltip-row"><span>· {e.descricao}</span><span>+{fmtBRL(e.valor)}</span></div>
        ))}
        {p.totalSaidas > 0 && (
          <div className="mv-chart-tooltip-row"><span style={{ color: 'var(--danger)' }}>Saídas</span><strong>-{fmtBRL(p.totalSaidas)}</strong></div>
        )}
        {p.maioresSaidas.map((s, i) => (
          <div key={`s${i}`} className="mv-chart-tooltip-row"><span>· {s.descricao}</span><span>-{fmtBRL(s.valor)}</span></div>
        ))}
      </div>
    )
  }

  function pontoGrafico(props: { cx?: number; cy?: number; payload?: PontoGrafico }) {
    const { cx, cy, payload } = props
    if (!payload?.grande || cx === undefined || cy === undefined) return <circle cx={cx ?? 0} cy={cy ?? 0} r={0} />
    const cor = payload.saldo < 0 ? 'var(--danger)' : 'var(--mv-blue)'
    return <circle cx={cx} cy={cy} r={4.5} fill={cor} stroke="var(--bg)" strokeWidth={2} />
  }

  const semAnimacao = prefereMovimentoReduzido()

  return (
    <MvPage>
      <MvHero
        eyebrow={typeof janela === 'number' ? `Saldo projetado em ${janela} dias` : 'Saldo projetado'}
        value={saldoFinal}
        tone={saldoFinal >= 0 ? 'good' : 'bad'}
        contraste={projecao ? { valor: projecao.saldoAtual } : undefined}
        caption={
          projecao && (
            temNegativo
              ? <>Seu menor saldo é <strong>{fmtBRL(menorSaldo)}</strong>, em <strong>{menorSaldoDia && fmtDataCompleta(menorSaldoDia.data)}</strong>.</>
              : <>Seu saldo não fica negativo em nenhum dia desse período.</>
          )
        }
        footnote={fmtLidoEm(cenario === 'Provavel' ? 'cenário provável' : 'cenário conservador')}
      />

      <div className="mv-controles-row">
        <MvTabs
          opcoes={[
            { valor: '30', label: '30 dias' },
            { valor: '60', label: '60 dias' },
            { valor: '90', label: '90 dias' },
            { valor: 'trajetoria', label: '6m + 6m' },
          ]}
          ativa={String(janela)}
          onChange={v => setJanela(v === 'trajetoria' ? 'trajetoria' : (Number(v) as Janela))}
        />
        {contas.length > 1 && (
          <select className="mv-select" value={contaFiltro} onChange={e => setContaFiltro(e.target.value)}>
            <option value="">Todas as contas</option>
            {contas.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        )}
      </div>

      <div className="mv-controles-row">
        <MvTabs
          opcoes={[{ valor: 'Provavel', label: 'Provável' }, { valor: 'Conservador', label: 'Conservador' }]}
          ativa={cenario}
          onChange={setCenario}
        />
        <label
          className="mv-checkbox-label"
          title={contaFiltro ? 'Só se aplica no modo "Todas as contas"' : 'Por padrão, contas de Investimento ficam de fora do saldo (reserva, não dinheiro disponível pro dia a dia).'}
        >
          <input
            type="checkbox"
            checked={incluirReserva}
            disabled={!!contaFiltro}
            onChange={e => setIncluirReserva(e.target.checked)}
          />
          Incluir reserva de investimento
        </label>
      </div>

      {loading && <p className="pj-loading">Calculando projeção...</p>}
      {erro    && <p className="pj-erro">{erro}</p>}

      {sugestoesPagar.length > 0 && (
        <div className="pj-despesas-estimadas">
          <h3 className="pj-lista-titulo">Pagamentos recorrentes identificados (não cadastrados)</h3>
          <p className="pj-despesas-estimadas-dica">
            Mesmo favorecido, valor parecido, em pelo menos 2 dos últimos 3 meses — ainda sem conta recorrente cadastrada.
          </p>
          {sugestoesPagarPorCategoria.map(([categoria, itens]) => (
            <div key={categoria} className="pj-despesa-estimada-grupo">
              <div className="pj-despesa-estimada-grupo-titulo">{categoria}</div>
              {itens.map(s => (
                <div key={s.chave} className="pj-despesa-estimada-item">
                  <span className="pj-despesa-estimada-cat">{s.descricao}</span>
                  <span className="pj-despesa-estimada-valor">
                    {s.valorVariavel ? '~' : ''}{fmtBRL(s.valorMedio)}/mês · dia {s.diaTipico}
                  </span>
                  <button className="pj-despesa-estimada-btn" onClick={() => abrirSugestaoPagar(s)}>
                    Transformar em conta a pagar
                  </button>
                  <button
                    className="pj-despesa-estimada-ignorar" disabled={ignorandoChave === s.chave}
                    onClick={() => ignorarSugestaoPagar(s)}
                  >
                    {ignorandoChave === s.chave ? 'Ignorando...' : 'Não sugerir mais'}
                  </button>
                  <span className="pj-despesa-estimada-sub">
                    Apareceu em {s.quantidadeOcorrencias} dos últimos 3 meses · última vez em {fmtDataCompleta(s.ultimaOcorrencia)}
                  </span>
                </div>
              ))}
            </div>
          ))}
        </div>
      )}

      {janela === 'trajetoria' && trajetoria && !loading && (
        <>
          {historicoCurto && (
            <MvInsight tone="warn" icon="⚠️" text={`Só há ${trajetoria.mesesHistoricoDisponiveis} mês(es) de histórico disponível — mostrando o período que existe, em vez de 6 meses completos.`} />
          )}

          <MvKpiGrid>
            <MvKpi label="Saldo atual" value={trajetoria.saldoAtual} tone={trajetoria.saldoAtual >= 0 ? 'good' : 'bad'} />
            <MvKpi label={`Variação · últimos ${trajetoria.mesesHistoricoDisponiveis}m`} value={trajetoria.variacaoRealizada} tone={trajetoria.variacaoRealizada >= 0 ? 'good' : 'bad'} />
            <MvKpi label="Variação · próximos 6m" value={trajetoria.variacaoProjetada} tone={trajetoria.variacaoProjetada >= 0 ? 'good' : 'bad'} />
            <MvKpi
              label="Projeção vs. realizado"
              value={null}
              tone={trajetoria.variacaoProjetada >= trajetoria.variacaoRealizada ? 'good' : 'bad'}
              delta={trajetoria.variacaoProjetada >= trajetoria.variacaoRealizada ? '↑ Melhor' : '↓ Pior'}
            />
          </MvKpiGrid>

          <div className="mv-chart-card">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={dadosTrajetoria} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
                <defs>
                  <linearGradient id="gradRealizado" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="var(--mv-blue)" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="var(--mv-blue)" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid stroke="var(--tx2)" strokeOpacity={0.12} vertical={false} />
                <XAxis dataKey="mes" tick={{ fontSize: 11, fill: 'var(--tx2)' }} interval="preserveStartEnd" axisLine={{ stroke: 'var(--tx2)', strokeOpacity: 0.3 }} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: 'var(--tx3)' }} tickFormatter={v => fmtBRLCompacto(v)} width={64} axisLine={false} tickLine={false} />
                <Tooltip
                  formatter={(v, name) => typeof v === 'number' ? [fmtBRL(v), name === 'saldoRealizado' ? 'Realizado' : 'Projetado'] : [String(v)]}
                  contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', fontSize: 12, borderRadius: 10 }}
                  labelStyle={{ color: 'var(--tx2)' }}
                />
                {mesHojeLabel && (
                  <ReferenceLine x={mesHojeLabel} stroke="var(--tx3)" strokeDasharray="2 2" label={{ value: 'Hoje', fill: 'var(--tx3)', fontSize: 11, position: 'insideTopRight' }} />
                )}
                <Area
                  type="monotone" dataKey="saldoRealizado" name="saldoRealizado"
                  stroke="var(--mv-blue)" strokeWidth={2} fill="url(#gradRealizado)"
                  dot={false} activeDot={{ r: 4 }} connectNulls={false} isAnimationActive={!semAnimacao}
                />
                <Area
                  type="monotone" dataKey="saldoProjetado" name="saldoProjetado"
                  stroke="var(--mv-blue)" strokeWidth={2} strokeDasharray="6 4" strokeOpacity={0.7}
                  fill="url(#gradRealizado)" fillOpacity={0.4}
                  dot={false} activeDot={{ r: 4 }} connectNulls={false} isAnimationActive={!semAnimacao}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </>
      )}

      {janela !== 'trajetoria' && projecao && !loading && (
        <>
          <MvKpiGrid>
            <MvKpi label="Entradas previstas" value={totalEntradasPeriodo} tone="good" />
            <MvKpi label="Saídas previstas" value={totalSaidasPeriodo} tone="bad" />
            <MvKpi
              label="Menor saldo"
              value={menorSaldo}
              tone={menorSaldo >= 0 ? 'neutral' : 'bad'}
              delta={menorSaldoDia ? fmtDataCompleta(menorSaldoDia.data) : undefined}
            />
            <MvKpi
              label="Dias com saldo negativo"
              value={diasNegativos}
              formatter={n => `${n} dia${n === 1 ? '' : 's'}`}
              tone={diasNegativos > 0 ? 'bad' : 'good'}
            />
          </MvKpiGrid>

          {temNegativo ? (
            <MvInsight tone="bad" icon="⚠️" text={<>Risco de caixa: seu saldo fica negativo em <strong>{diasNegativos}</strong> dia(s) desse período.</>} />
          ) : (
            <MvInsight tone="good" icon="✅" text="Seu caixa se mantém positivo durante todo o período, nesse cenário." />
          )}
          {reservaMinima !== null && menorSaldo >= 0 && menorSaldo < reservaMinima && (
            <MvInsight tone="warn" icon="🛟" text={<>Seu saldo mínimo projetado fica abaixo da reserva de <strong>{fmtBRL(reservaMinima)}</strong> que você definiu em "Meu Futuro".</>} />
          )}

          <MvSectionLabel>Evolução do saldo</MvSectionLabel>
          <div className="mv-chart-card">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={dadosGrafico} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
                <defs>
                  <linearGradient id="gradSaldoPj" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%"  stopColor="var(--mv-blue)" stopOpacity={0.28} />
                    <stop offset="95%" stopColor="var(--mv-blue)" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid stroke="var(--tx2)" strokeOpacity={0.12} vertical={false} />
                <XAxis dataKey="data" tick={{ fontSize: 11, fill: 'var(--tx2)' }} interval="preserveStartEnd" axisLine={{ stroke: 'var(--tx2)', strokeOpacity: 0.3 }} tickLine={false} />
                <YAxis
                  tick={{ fontSize: 11, fill: 'var(--tx3)' }}
                  tickFormatter={v => fmtBRLCompacto(v)} width={64} axisLine={false} tickLine={false}
                  domain={dominioY}
                />
                <Tooltip content={tooltipGrafico} cursor={{ stroke: 'var(--bd)' }} />

                {reservaMinima !== null && dominioY && (
                  <>
                    <ReferenceArea y1={dominioY[0]} y2={reservaMinima} fill="var(--danger)" fillOpacity={0.08} ifOverflow="extendDomain" />
                    <ReferenceLine y={reservaMinima} stroke="var(--danger)" strokeDasharray="4 2" strokeOpacity={0.6}
                      label={{ value: 'Reserva mínima', fill: 'var(--danger)', fontSize: 10.5, position: 'insideTopLeft' }} />
                  </>
                )}
                {temNegativo && <ReferenceLine y={0} stroke="var(--warning)" strokeDasharray="4 2" label={{ value: 'R$ 0', fill: 'var(--warning)', fontSize: 11 }} />}

                {/* Faixa de incerteza entre os cenários Provável e Conservador. */}
                <Area type="monotone" dataKey="faixa" stroke="none" fill="var(--mv-blue)" fillOpacity={0.12} isAnimationActive={!semAnimacao} activeDot={false} />

                {/* Linha protagonista: 100% desse período é projeção futura, por isso sempre tracejada. */}
                <Area
                  type="monotone" dataKey="saldo" name="Saldo"
                  stroke="var(--mv-blue)" strokeWidth={2} strokeDasharray="6 4"
                  fill="url(#gradSaldoPj)" dot={pontoGrafico} activeDot={{ r: 5 }} isAnimationActive={!semAnimacao}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>

          {/* Redesign visual: "Próximos 7 dias" como timeline, substituindo a antiga lista plana. */}
          <MvSectionLabel>Próximos 7 dias</MvSectionLabel>
          <div className="mv-timeline">
            {proximosSete.map(dia => {
              const temMovimento = dia.entradas.length > 0 || dia.saidas.length > 0
              const categorias = [...new Set([...dia.entradas, ...dia.saidas].map(it => it.categoria).filter((c): c is string => !!c))]
              return (
                <div key={dia.data} className={`mv-timeline-item${dia.saldoNegativo ? ' mv-tone-bad' : ''}`}>
                  <span className="mv-timeline-dot" />
                  <div className="mv-timeline-body">
                    <div className="mv-timeline-date">{fmtDataCompleta(dia.data)}</div>
                    {temMovimento ? (
                      <>
                        <div className="mv-timeline-valores">
                          {dia.entradas.length > 0 && <span style={{ color: 'var(--success)' }}>↑ {fmtBRL(dia.totalEntradas)}</span>}
                          {dia.saidas.length > 0 && <span style={{ color: 'var(--danger)' }}>↓ {fmtBRL(dia.totalSaidas)}</span>}
                          <span className="mv-timeline-saldo">saldo <strong className={dia.saldoNegativo ? 'val-red' : undefined}>{fmtBRL(dia.saldoFim)}</strong></span>
                        </div>
                        {categorias.length > 0 && (
                          <div className="mv-chips">
                            {categorias.slice(0, 4).map(c => <span key={c} className="mv-chip">{c}</span>)}
                          </div>
                        )}
                      </>
                    ) : (
                      <div className="mv-timeline-saldo">Sem movimento previsto — saldo <strong>{fmtBRL(dia.saldoFim)}</strong></div>
                    )}
                  </div>
                </div>
              )
            })}
          </div>

          <MvDetails label="Ver todas as movimentações">
            {diasComMovimento.length === 0 && (
              <p className="pj-vazio">Nenhuma entrada ou saída prevista no período.</p>
            )}

            {/* 30 dias: lista diária | 60/90 dias: agrupada por semana */}
            {janela === 30 || !semanas ? (
              <div className="pj-lista">
                {diasComMovimento.map(dia => (
                  <DiaItem
                    key={dia.data}
                    dia={dia}
                    expanded={!!expandidos[dia.data]}
                    onToggle={() => toggleDia(dia.data)}
                  />
                ))}
              </div>
            ) : (
              <div className="pj-lista">
                {semanas.map(sem => (
                  <div key={sem.label} className="pj-semana">
                    <button className="pj-semana-header" onClick={() => toggleDia(sem.label)}>
                      <span>{expandidos[sem.label] ? '▾' : '▸'} {sem.label}</span>
                      <span className="pj-semana-resumo">
                        {sem.dias.reduce((s, d) => s + d.entradas.length + d.saidas.length, 0)} lançamento(s)
                      </span>
                    </button>
                    {expandidos[sem.label] && sem.dias.map(dia => (
                      <DiaItem
                        key={dia.data}
                        dia={dia}
                        expanded={!!expandidos[dia.data + '_inner']}
                        onToggle={() => toggleDia(dia.data + '_inner')}
                      />
                    ))}
                  </div>
                ))}
              </div>
            )}
          </MvDetails>
        </>
      )}

      <Modal
        open={!!sugestaoAlvo}
        title="Transformar em conta a pagar recorrente"
        onClose={() => setSugestaoAlvo(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setSugestaoAlvo(null)} disabled={salvandoSugestao}>Cancelar</button>
            <button
              className="btn-confirm" onClick={confirmarCriarRecorrentePagar}
              disabled={salvandoSugestao || !formContaId || !formCategoria || !formValor}
            >
              {salvandoSugestao ? 'Salvando...' : 'Confirmar'}
            </button>
          </>
        }
      >
        {sugestaoAlvo && (
          <>
            <div className="inp-group">
              <label>Descrição (favorecido)</label>
              <input value={sugestaoAlvo.descricao} disabled />
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Categoria</label>
              <select value={formCategoria} onChange={e => setFormCategoria(e.target.value)}>
                <option value="">Selecione...</option>
                {categorias.saidas.map(c => <option key={c.nome} value={c.nome}>{c.nome}</option>)}
              </select>
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Valor médio</label>
              <div className="val-input-wrap">
                <span className="val-prefix">R$</span>
                <input
                  type="text" inputMode="decimal"
                  value={formValorDisplay}
                  onChange={e => {
                    const raw = e.target.value.replace(/[^\d,]/g, '')
                    setFormValorDisplay(raw)
                    setFormValor(parseBRL(raw))
                  }}
                  onBlur={() => setFormValorDisplay(formValor ? fmtNum(formValor) : '')}
                />
              </div>
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Dia de vencimento (dia típico identificado)</label>
              <input type="number" min="1" max="31" value={formDiaVencimento} onChange={e => setFormDiaVencimento(e.target.value)} />
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Conta bancária</label>
              <select value={formContaId} onChange={e => setFormContaId(e.target.value)}>
                {contas.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
              </select>
            </div>
            <div className="inp-group" style={{ marginTop: 12, display: 'flex', alignItems: 'center', gap: 6 }}>
              <input
                id="pj-sugestao-valor-variavel" type="checkbox" checked={formValorVariavel}
                onChange={e => setFormValorVariavel(e.target.checked)}
              />
              <label htmlFor="pj-sugestao-valor-variavel" style={{ margin: 0, fontSize: 13 }}>
                Valor variável {sugestaoAlvo.valorVariavel && '(detectamos variação nas últimas ocorrências)'}
              </label>
            </div>
            <p style={{ color: 'var(--tx3)', fontSize: 12, marginTop: 12 }}>
              Apareceu em {sugestaoAlvo.quantidadeOcorrencias} dos últimos 3 meses, última vez em {fmtDataCompleta(sugestaoAlvo.ultimaOcorrencia)}.
            </p>
          </>
        )}
      </Modal>
    </MvPage>
  )
}

function origemIcone(origem: string): string {
  if (origem === 'Recorrente') return '↺'
  if (origem === 'Atrasado') return '⚠️'
  if (origem === 'Estimativa') return '≈'
  if (origem === 'Fatura') return '💳'
  return '📋'
}

interface DiaItemProps {
  dia: ProjecaoDia
  expanded: boolean
  onToggle: () => void
}

function DiaItem({ dia, expanded, onToggle }: DiaItemProps) {
  const temMovimento = dia.entradas.length > 0 || dia.saidas.length > 0
  if (!temMovimento) return null

  return (
    <div className={`pj-dia${dia.saldoNegativo ? ' pj-dia-negativo' : ''}`}>
      <button className="pj-dia-header" onClick={onToggle}>
        <span className="pj-dia-data">{fmtDataCompleta(dia.data)}</span>
        <span className="pj-dia-resumo">
          {dia.entradas.length > 0 && (
            <span className="val-green">+{fmtBRL(dia.totalEntradas)}</span>
          )}
          {dia.saidas.length > 0 && (
            <span className="val-red"> -{fmtBRL(dia.totalSaidas)}</span>
          )}
        </span>
        <span className={`pj-dia-saldo ${dia.saldoNegativo ? 'val-red' : 'val-green'}`}>
          = {fmtBRL(dia.saldoFim)}
        </span>
        <span className="pj-expand">{expanded ? '▾' : '▸'}</span>
      </button>

      {expanded && (
        <div className="pj-dia-detalhe">
          {dia.entradas.map((e, i) => (
            <div key={i} className="pj-item pj-item-entrada">
              <span className="pj-item-origem pj-origem-rec">{origemIcone(e.origem)}</span>
              <span className="pj-item-desc">{e.descricao}</span>
              {e.categoria && <span className="pj-item-cat">{e.categoria}</span>}
              <span className="pj-item-val val-green">+{fmtBRL(e.valor)}</span>
            </div>
          ))}
          {dia.saidas.map((s, i) => (
            <div key={i} className="pj-item pj-item-saida">
              <span className="pj-item-origem pj-origem-rec">{origemIcone(s.origem)}</span>
              <span className="pj-item-desc">{s.descricao}</span>
              {s.categoria && <span className="pj-item-cat">{s.categoria}</span>}
              <span className="pj-item-val val-red">-{fmtBRL(s.valor)}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
