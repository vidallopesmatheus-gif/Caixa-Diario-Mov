import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { obterInsights } from '../../../api/insights'
import type { Insight, InsightCategoria } from '../../../api/insights'
import { getContasEmRisco } from '../../../utils/alertas'
import { ehOperacional } from '../../../utils/lancamentos'
import { fmtBRL, todayISO, addDays } from '../../../utils/format'
import type { Registro, ContaBancaria } from '../../../types'

interface Props {
  clienteId: string
  registros: Registro[]
  contasBancarias: ContaBancaria[]
}

interface ItemAtencao {
  chave: string
  texto: string
  detalhe?: string
  urgente: boolean
  onClick: () => void
}

const ROTA_POR_CATEGORIA: Record<InsightCategoria, string | 'metas'> = {
  saldo: '/resultados/projecao',
  gasto: '/resultados/indicadores',
  lucro: '/resultados/indicadores',
  meta: 'metas',
  geral: '/resultados/indicadores',
}

export default function AtencaoCard({ clienteId, registros, contasBancarias }: Props) {
  const [insights, setInsights] = useState<Insight[]>([])
  const [carregado, setCarregado] = useState(false)
  const navigate = useNavigate()

  useEffect(() => {
    if (!clienteId) return
    obterInsights(clienteId)
      .then(insights => { setInsights(insights); setCarregado(true) })
      .catch(() => { setInsights([]); setCarregado(true) })
  }, [clienteId])

  function irPara(categoria: InsightCategoria) {
    const rota = ROTA_POR_CATEGORIA[categoria]
    if (rota === 'metas') {
      document.getElementById('metas-investimentos')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    } else {
      navigate(rota)
    }
  }

  if (!carregado) {
    return (
      <div className="atencao-card">
        <h3 className="atencao-titulo">🔎 Precisa de atenção</h3>
        <div className="atencao-skeleton" />
      </div>
    )
  }

  const vencimentos = getContasEmRisco(registros, 3)
  const vencidas = vencimentos.filter(v => v.vencida)
  const aVencer = vencimentos.filter(v => !v.vencida)
  const alertas = insights.filter(i => i.tipo === 'alerta')
  const totalPendentesCategorizacao = contasBancarias.reduce((s, c) => s + c.pendentesCategorizacao, 0)

  // Bloco 5 (Money Vision), item 6: "tenho dinheiro suficiente pra pagar o que vem, ou preciso
  // resgatar do investimento?" — contas a pagar nos próximos 7 dias vs. disponível em conta.
  const hoje = todayISO()
  const em7Dias = addDays(hoje, 7)
  const aPagarEm7Dias = registros
    .flatMap(r => r.contasAPagar)
    .filter(c => !c.pago && c.dataVencimento && c.dataVencimento >= hoje && c.dataVencimento <= em7Dias)
    .reduce((s, c) => s + c.valor, 0)
  const disponivelEmConta = contasBancarias
    .filter(c => c.ativa && (c.tipo === 'ContaCorrente' || c.tipo === 'Caixa'))
    .reduce((s, c) => s + c.saldoAtual, 0)
  const investidoTotal = contasBancarias.filter(c => c.ativa && c.tipo === 'Investimento').reduce((s, c) => s + c.saldoAtual, 0)
  const faltaParaPagar = aPagarEm7Dias - disponivelEmConta
  const alertaResgate: ItemAtencao | null = faltaParaPagar > 0 && investidoTotal > 0
    ? {
        chave: 'alerta-resgate-investimento',
        texto: `Você tem ${fmtBRL(aPagarEm7Dias)} pra pagar nos próximos 7 dias e ${fmtBRL(disponivelEmConta)} disponível em conta`,
        detalhe: `Há ${fmtBRL(investidoTotal)} investido: considere resgatar ${fmtBRL(Math.min(faltaParaPagar, investidoTotal))}.`,
        urgente: true,
        onClick: () => navigate('/banco'),
      }
    : null

  // Item 6, segundo alerta: dinheiro parado em conta corrente acima do gasto médio mensal —
  // média das saídas operacionais dos últimos 3 meses CHEIOS (sem contar o mês em andamento,
  // que ainda não terminou e sempre pareceria "menor" por estar incompleto).
  const inicioMesAtual = hoje.slice(0, 7) + '-01'
  const mesesAnteriores = [1, 2, 3].map(i => {
    const d = new Date(inicioMesAtual + 'T12:00:00')
    d.setMonth(d.getMonth() - i)
    return d.toISOString().slice(0, 7)
  })
  const gastosPorMes = mesesAnteriores.map(mes =>
    registros.filter(r => r.data.startsWith(mes)).reduce((s, r) => s + r.saidas.filter(ehOperacional).reduce((a, x) => a + x.valor, 0), 0),
  )
  const gastoMedioMensal = gastosPorMes.some(g => g > 0) ? gastosPorMes.reduce((a, b) => a + b, 0) / gastosPorMes.length : 0
  const alertaDinheiroParado: ItemAtencao | null = gastoMedioMensal > 0 && disponivelEmConta > gastoMedioMensal
    ? {
        chave: 'alerta-dinheiro-parado',
        texto: `${fmtBRL(disponivelEmConta)} parados na conta corrente, acima do seu gasto médio mensal`,
        detalhe: `Gasto médio dos últimos 3 meses: ${fmtBRL(gastoMedioMensal)}. Considere guardar o excedente.`,
        urgente: false,
        onClick: () => navigate('/banco'),
      }
    : null

  const itens: ItemAtencao[] = [
    ...vencidas.map((v): ItemAtencao => ({
      chave: `venc-${v.registroData}-${v.tipo}-${v.index}`,
      texto: `${v.tipo === 'pagar' ? 'Conta vencida' : 'Recebimento atrasado'}: ${v.conta.descricao}`,
      detalhe: `${fmtBRL(v.conta.valor)} · venceu em ${v.conta.dataVencimento?.split('-').reverse().join('/')}`,
      urgente: true,
      onClick: () => navigate('/contas'),
    })),
    ...(alertaResgate ? [alertaResgate] : []),
    ...alertas.map((a): ItemAtencao => ({
      chave: `insight-${a.prioridade}-${a.texto}`,
      texto: a.texto,
      detalhe: a.detalhe,
      urgente: true,
      onClick: () => irPara(a.categoria),
    })),
    ...(totalPendentesCategorizacao > 0 ? [{
      chave: 'pendentes-categorizacao',
      texto: `${totalPendentesCategorizacao} lançamento(s) importado(s) aguardando categoria`,
      detalhe: 'Já contam no saldo — só falta classificar',
      urgente: false,
      onClick: () => navigate('/banco'),
    } as ItemAtencao] : []),
    ...(alertaDinheiroParado ? [alertaDinheiroParado] : []),
    ...aVencer.map((v): ItemAtencao => ({
      chave: `avencer-${v.registroData}-${v.tipo}-${v.index}`,
      texto: `${v.tipo === 'pagar' ? 'Conta a pagar' : 'Recebimento previsto'}: ${v.conta.descricao}`,
      detalhe: `${fmtBRL(v.conta.valor)} · vence em ${v.conta.dataVencimento?.split('-').reverse().join('/')}`,
      urgente: false,
      onClick: () => navigate('/contas'),
    })),
  ].slice(0, 3)

  return (
    <div className="atencao-card">
      <h3 className="atencao-titulo">🔎 Precisa de atenção</h3>
      {itens.length === 0 ? (
        <p className="atencao-vazio">✅ Nada exigindo atenção nos próximos 30 dias.</p>
      ) : (
        <div className="atencao-lista">
          {itens.map(item => (
            <button key={item.chave} type="button" className={`atencao-item${item.urgente ? ' urgente' : ''}`} onClick={item.onClick}>
              <span className="atencao-item-texto">{item.texto}</span>
              {item.detalhe && <span className="atencao-item-detalhe">{item.detalhe}</span>}
              <span className="atencao-item-seta">→</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
