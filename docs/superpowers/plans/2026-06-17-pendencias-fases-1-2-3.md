# Pendências Fases 1-2-3 — Plano de Conclusão (Spec-Driven)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans para implementar task-by-task. Os passos usam checkbox (`- [ ]`) para tracking.
> **Origem:** Auditoria técnica da branch `feat/implementacao-fases-1-2-3` (2026-06-17). Este plano cobre **apenas** os itens classificados como ❌ NÃO IMPLEMENTADO e 🟡 PARCIAL. Os itens ✅ já comprovados em código **não são tocados**.

**Goal:** Fechar os 22 itens pendentes/parciais das Fases 1-2-3 sem regredir o que já funciona, reaproveitando os padrões existentes (services testados com xUnit/Moq, controllers `[Authorize]` com `ApiResponse<T>`, frontend React 19 + StatCard + recharts).

**Architecture:** Mudanças aditivas e backward-compatible sempre que possível. Indicadores novos entram como campos *nullable* no `MetricasPeriodoDto` (mesmo padrão de EBITDA/Runway). Mudanças de modelo (`ContaRecorrente`, `ContaProvisionada`) usam migrations EF Core seguindo a convenção `AppDbContext`. Frontend deriva o que dá para derivar de `registros`/`metricas` sem novo backend; só vai ao backend quando o cálculo exige dados históricos ou persistência.

**Tech Stack:** .NET 10, EF Core, xUnit, Moq, ClosedXML, QuestPDF, React 19, TypeScript, recharts.

## Global Constraints

- Toda rota nova de API: `[Authorize]` + checagem `if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId) throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.")`, idêntica aos controllers existentes.
- Respostas de API sempre embrulhadas em `ApiResponse<T> { Dados = ... }`.
- Valores monetários: `decimal` no backend, `number` no frontend, formatados com `fmtBRL` (pt-BR/BRL).
- Datas: `DateOnly` no backend, ISO `yyyy-MM-dd` (string) no frontend; formatação via `fmtDate`.
- Semáforos: strings `"verde" | "amarelo" | "vermelho" | "cinza"`, mapeadas a classes CSS `val-green | val-yellow | val-red | val-blue` no `StatCard`.
- Nenhum cálculo pode dividir por zero — seguir o padrão `receita > 0 ? ... : 0` já usado em `MetricasService`.
- Testes primeiro (TDD) para toda lógica de cálculo em `MetricasService`. Frontend: validar com `npx tsc --noEmit` e `npm test -- --run`.
- Comandos de teste: `dotnet test CaixaDiario.Tests -v minimal` (backend); `cd frontend && npx tsc --noEmit && npm test -- --run` (frontend).

---

## ⚠️ DECISÕES DE PRODUTO NECESSÁRIAS (resolver ANTES de codar a task indicada)

Estes pontos **não estão definidos no documento original** e não devem ser inventados. Cada um bloqueia a task correspondente. A coluna "Default proposto" é uma sugestão a confirmar com o cliente — **não** uma decisão tomada.

| # | Decisão | Bloqueia | Default proposto (confirmar) |
|---|---------|----------|------------------------------|
| D1 | A meta de lucro é definida **anualmente** (modelo atual `MetaAnual`) e o mensal é derivado, ou o cliente quer **definir um valor por mês**? | A1 | Manter meta anual e **derivar** a meta do mês (anual ÷ distribuição já existente em `planejamento`). |
| D2 | O que conta como "Quantidade de Recebimentos" no Ticket Médio? Cada item de entrada? Só entradas com `tipoCusto == "Receita"`? Cada registro/dia com receita? | A4 | Nº de **itens de entrada com `tipoCusto == "Receita"`** no período. |
| D3 | Índice de Liquidez: numerador = Caixa + **quais** contas a receber (todas pendentes? só próximos 30 dias?). Denominador continua "contas a pagar próximos 30 dias"? | A6 | Numerador = `saldoAtual + contasReceber pendentes próximos 30 dias`; denominador inalterado. |
| D4 | Múltiplo de Valuation: opções fixas 3x/4x/5x/6x escolhidas **na tela** (sem persistir) ou **persistidas por cliente**? | A7 | Selecionado na tela; enviado como query param `multiplo` (default 3). Sem persistência. |
| D5 | **Taxonomia de categorias** (grupos Custos Diretos, Pessoas, Despesas Administrativas, Marketing, Impostos, Financeiras, Investimentos + subcategorias): definir a lista exata de subcategorias E como mapear os dados já existentes (categorias atuais: Aluguel, Salários/Folha, Insumos/Mercadoria, etc.). **Alto impacto:** EBITDA e Prime Cost hoje dependem dos nomes `"Salários/Folha"`, `"Insumos/Mercadoria"`, `"Manutenção"`. | B2, e A8 (pizza depende do agrupamento) | Ver "Proposta de taxonomia" na Task B2 — **requer sign-off**. |
| D6 | Recorrência: semântica de cada periodicidade e como `QuantidadeParcelas` interage com `DataFim` (parcelas têm prioridade? geram `DataFim`?). "Não recorrente" deve sequer existir como conta recorrente? | C1 | Periodicidade gera ocorrências; `QuantidadeParcelas` (opcional) limita o nº de ocorrências; se ambos definidos, para na primeira condição atingida. "Não recorrente" = lançamento único (não vira `ContaRecorrente`). |
| D7 | Baixa que "movimenta o caixa automaticamente": criar um `ItemFinanceiro` (entrada/saída) no dia da baixa **além** de marcar `Pago`? Como evitar dupla contagem no `saldoConfirmado` (que hoje é informado manualmente pelo usuário)? | C2 | Registrar `DataBaixa` e criar item financeiro correspondente no registro do dia da baixa; **não** alterar `saldoConfirmado` automaticamente (ele continua sendo a conferência manual). Confirmar. |

> Tasks marcadas com 🔒 dependem de uma decisão acima e trazem o código sob o default proposto, claramente sinalizado.

---

## Mapa de Arquivos

| Ação | Arquivo | Frente |
|------|---------|--------|
| Modify | `frontend/src/pages/client/ClientDashboardPage.tsx` | A1, A2, A3, A7, A8 |
| Modify | `CaixaDiario.API/Services/MetricasService.cs` | A4, A5, A6, A7 |
| Modify | `CaixaDiario.API/DTOs/Metricas/MetricasPeriodoDto.cs` | A4, A5 |
| Modify | `CaixaDiario.API/Services/IMetricasService.cs` | A7 |
| Modify | `CaixaDiario.API/Controllers/MetricasController.cs` | A7 |
| Modify | `CaixaDiario.Tests/Services/MetricasServiceTests.cs` | A4, A5, A6, A7 |
| Modify | `frontend/src/api/metricas.ts` | A4, A5, A7 |
| Modify | `frontend/src/hooks/useMetricas.ts` | A7 |
| Modify | `frontend/src/pages/client/ClientCaixaPage.tsx` | B1, 2.1.1 |
| Modify | `frontend/src/pages/client/ClientCaixa.css` | 2.1.1 |
| Modify | `CaixaDiario.API/Services/RegistroService.cs` | B1 |
| Modify | `CaixaDiario.API/DTOs/Registros/CriarRegistroDto.cs` (validação) | B1 |
| Modify | `CaixaDiario.API/Controllers/CategoriasController.cs` | B2 🔒 |
| Modify | `frontend/src/types.ts` | B2, C1 |
| Create | `frontend/src/pages/client/ClientRelatorioCategoriaPage.tsx` (ou seção) | B3 |
| Modify | `CaixaDiario.API/Models/ContaRecorrente.cs` | C1 🔒 |
| Modify | `CaixaDiario.API/DTOs/ContasRecorrentes/*.cs` | C1 🔒 |
| Modify | `CaixaDiario.API/Services/ContaRecorrenteService.cs` | C1 🔒 |
| Modify | `CaixaDiario.API/Services/RecorrenciaService.cs` | C1 🔒 |
| Modify | `CaixaDiario.API/Services/MetricasService.cs` (fluxo projetado) | C1 🔒 |
| Create | EF migration (recorrência) | C1 🔒 |
| Modify | `CaixaDiario.API/Models/ContaProvisionada.cs` | C2 🔒 |
| Modify | `CaixaDiario.API/Services/RegistroService.cs` (baixa) | C2 🔒 |
| Create | EF migration (DataBaixa) | C2 🔒 |
| Modify | `frontend/src/pages/client/ClientContasPage.tsx` | C1, C2 |
| Modify | `frontend/src/utils/alertas.ts` | C3 |
| Modify | `frontend/src/pages/client/ClientDashboardPage.tsx` | C3 |

---

# FRENTE A — Dashboard Executivo

## Task A1 — Card "Meta de Lucro do Mês" (barra de progresso, % atingido, valor faltante) 🔒 D1

Cobre **1.1.1, 1.1.3, 1.1.4, 1.1.5** e a parte derivável de **1.1.2**. Sob o default D1, a meta do mês é derivada da meta anual via a lógica `planejamento` já existente em [ClientDashboardPage.tsx:69-100](../../../frontend/src/pages/client/ClientDashboardPage.tsx#L69-L100) (`targetLucro` do mês atual) e o lucro real do mês já é `lucroReal`.

**Files:**
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1: Derivar meta/realizado/faltante do mês atual**

Logo após o `useMemo` de `planejamento` (linha ~100), adicionar:

```tsx
const metaMesAtual = useMemo(() => {
  const linha = planejamento.find(p => p.isAtual)
  if (!linha || linha.lucroReal === null) return null
  const alvo = linha.targetLucro
  const real = linha.lucroReal
  const pct = alvo > 0 ? Math.min(100, Math.max(0, (real / alvo) * 100)) : 0
  const faltante = Math.max(0, alvo - real)
  return { alvo, real, pct, faltante, atingida: real >= alvo }
}, [planejamento])
```

- [ ] **Step 2: Renderizar o card dedicado com barra de progresso**

Inserir antes do card `🎯 Metas Anuais` (linha ~222), só quando há meta carregada:

```tsx
{metaMesAtual && (
  <div className="meta-card">
    <h3>🎯 Meta de Lucro do Mês</h3>
    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, color: 'var(--tx3)', marginBottom: 6 }}>
      <span>Realizado: {fmtBRL(metaMesAtual.real)}</span>
      <span>Meta: {fmtBRL(metaMesAtual.alvo)}</span>
    </div>
    <div style={{ height: 14, background: 'var(--bd)', borderRadius: 8, overflow: 'hidden' }}>
      <div style={{
        width: `${metaMesAtual.pct}%`, height: '100%',
        background: metaMesAtual.atingida ? '#34c759' : '#0a84ff', transition: 'width .3s',
      }} />
    </div>
    <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 8, fontSize: 14, fontWeight: 600 }}>
      <span style={{ color: metaMesAtual.atingida ? '#34c759' : 'var(--tx1)' }}>
        {metaMesAtual.pct.toFixed(1)}% atingido
      </span>
      <span style={{ color: metaMesAtual.atingida ? '#34c759' : '#ff9500' }}>
        {metaMesAtual.atingida ? '✅ Meta batida!' : `Faltam ${fmtBRL(metaMesAtual.faltante)}`}
      </span>
    </div>
  </div>
)}
```

- [ ] **Step 3: TypeScript check**

Run: `cd frontend && npx tsc --noEmit`
Expected: No errors.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/pages/client/ClientDashboardPage.tsx
git commit -m "feat(fase1): card Meta de Lucro do Mês com barra/% atingido/faltante"
```

> **Se D1 = "definir valor por mês":** adicionar campo mensal exige backend novo (modelo `MetaMensal` ou coluna por mês em `MetaAnual`) — abrir sub-spec. Não implementar sob o default.

---

## Task A2 — Card "Lucro Líquido" com mês atual, mês anterior e variação % (1.2.1)

O dashboard hoje só mostra "Lucro Operacional" do período ([ClientDashboardPage.tsx:129](../../../frontend/src/pages/client/ClientDashboardPage.tsx#L129)). Falta o comparativo mês-a-mês. Derivável de `registros` no frontend.

**Files:**
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1: Computar lucro do mês atual e anterior**

Adicionar após `metaMesAtual`:

```tsx
const lucroComparativo = useMemo(() => {
  const lucroDoMes = (ano: number, mes: number) => {
    const prefixo = `${ano}-${String(mes).padStart(2, '0')}`
    const doMes = registros.filter(r => r.data.startsWith(prefixo))
    return doMes.reduce((s, r) =>
      s + r.entradas.reduce((a, e) => a + e.valor, 0) - r.saidas.reduce((a, e) => a + e.valor, 0), 0)
  }
  const atual = lucroDoMes(anoAtual, mesAtual)
  const mesAnt = mesAtual === 1 ? 12 : mesAtual - 1
  const anoAnt = mesAtual === 1 ? anoAtual - 1 : anoAtual
  const anterior = lucroDoMes(anoAnt, mesAnt)
  const variacao = anterior !== 0 ? ((atual - anterior) / Math.abs(anterior)) * 100 : null
  return { atual, anterior, variacao }
}, [registros, anoAtual, mesAtual])
```

- [ ] **Step 2: Renderizar o card**

Inserir no `stats-grid` principal (após "Lucro Operacional", linha ~129):

```tsx
<StatCard
  label="🧮 Lucro Líquido (mês)"
  value={fmtBRL(lucroComparativo.atual)}
  className={lucroComparativo.atual >= 0 ? 'val-green' : 'val-red'}
  sub={lucroComparativo.variacao === null
    ? `Mês anterior: ${fmtBRL(lucroComparativo.anterior)}`
    : `${lucroComparativo.variacao >= 0 ? '▲' : '▼'} ${Math.abs(lucroComparativo.variacao).toFixed(1)}% vs mês anterior`}
/>
```

- [ ] **Step 3: Garantir que `StatCard` aceita `sub`**

Abrir `frontend/src/components/shared/StatCard.tsx`. Se não houver prop `sub`, adicioná-la:

```tsx
interface Props { label: string; value: string; className?: string; sub?: string }

export default function StatCard({ label, value, className, sub }: Props) {
  return (
    <div className="stat-card">
      <div className="stat-label">{label}</div>
      <div className={`stat-value ${className ?? ''}`}>{value}</div>
      {sub && <div className="stat-sub" style={{ fontSize: 11, color: 'var(--tx3)', marginTop: 4 }}>{sub}</div>}
    </div>
  )
}
```

> **Nota:** ler o `StatCard.tsx` atual antes de editar e preservar o markup existente; só adicionar a linha do `sub`.

- [ ] **Step 4: TypeScript check + testes frontend**

Run: `cd frontend && npx tsc --noEmit && npm test -- --run`
Expected: No errors, testes passam.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/client/ClientDashboardPage.tsx frontend/src/components/shared/StatCard.tsx
git commit -m "feat(fase1): card Lucro Líquido com comparativo mês anterior e variação %"
```

---

## Task A3 — Saldo Final: recortes "Hoje" e "Últimos 30 dias" (1.2.2)

O card "Saldo Final" hoje segue o seletor De/Até genérico. Adicionar botões de recorte rápido.

**Files:**
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1: Adicionar atalhos de período**

No bloco `.dash-period` (linha ~121), adicionar botões que setam `de`/`ate`:

```tsx
<div className="dash-period">
  <label>De <input type="date" value={de} onChange={e => setDe(e.target.value)} /></label>
  <label>Até <input type="date" value={ate} onChange={e => setAte(e.target.value)} /></label>
  <button type="button" onClick={() => { const h = todayISO(); setDe(h); setAte(h) }}
    style={{ fontSize: 12, padding: '4px 10px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)', cursor: 'pointer' }}>
    Hoje
  </button>
  <button type="button" onClick={() => { setDe(addDays(todayISO(), -29)); setAte(todayISO()) }}
    style={{ fontSize: 12, padding: '4px 10px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)', cursor: 'pointer' }}>
    Últimos 30 dias
  </button>
</div>
```

Garantir o import: `import { fmtBRL, todayISO, addDays } from '../../utils/format'` (hoje só importa `fmtBRL`).

- [ ] **Step 2: TypeScript check**

Run: `cd frontend && npx tsc --noEmit`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/client/ClientDashboardPage.tsx
git commit -m "feat(fase1): atalhos Hoje / Últimos 30 dias no Saldo Final"
```

---

## Task A4 — Ticket Médio no backend (1.3.2) 🔒 D2

Sob o default D2: Ticket Médio = receita ÷ (nº de itens de entrada com `tipoCusto == "Receita"`).

**Files:**
- Modify: `CaixaDiario.API/DTOs/Metricas/MetricasPeriodoDto.cs`
- Modify: `CaixaDiario.API/Services/MetricasService.cs`
- Modify: `CaixaDiario.Tests/Services/MetricasServiceTests.cs`
- Modify: `frontend/src/api/metricas.ts`
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1: Adicionar DTO**

Em `MetricasPeriodoDto.cs`, adicionar a propriedade e a classe:

```csharp
public TicketMedioDto? TicketMedio { get; set; }
```
```csharp
public class TicketMedioDto
{
    public decimal Valor { get; set; }
    public int QuantidadeRecebimentos { get; set; }
}
```

- [ ] **Step 2: Escrever o teste primeiro**

Em `MetricasServiceTests.cs`:

```csharp
[Fact]
public void CalcularPeriodo_ComRecebimentos_CalculaTicketMedio()
{
    var reg = CriarRegistro(new DateOnly(2026, 6, 1),
        new() { Item("Venda 1", 600m, "Vendas", "Receita"), Item("Venda 2", 400m, "Vendas", "Receita") },
        new());
    var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
    Assert.NotNull(resultado.TicketMedio);
    Assert.Equal(2, resultado.TicketMedio!.QuantidadeRecebimentos);
    Assert.Equal(500m, resultado.TicketMedio.Valor);
}

[Fact]
public void CalcularPeriodo_SemRecebimentos_TicketMedioNull()
{
    var reg = CriarRegistro(new DateOnly(2026, 6, 1), new(), new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") });
    var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
    Assert.Null(resultado.TicketMedio);
}
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test CaixaDiario.Tests --filter "MetricasServiceTests" -v minimal`
Expected: 2 novos testes FAIL.

- [ ] **Step 4: Implementar em `CalcularPeriodo`**

Dentro do bloco `if (temCategoria && receita > 0)` (que já calcula `entradas`/`receita`), após o cálculo de `dto.Ebitda`, adicionar:

```csharp
var qtdRecebimentos = entradas.Count(e => e.TipoCusto == "Receita");
if (qtdRecebimentos > 0)
{
    dto.TicketMedio = new TicketMedioDto
    {
        Valor = Math.Round(receita / qtdRecebimentos, 2),
        QuantidadeRecebimentos = qtdRecebimentos,
    };
}
```

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test CaixaDiario.Tests --filter "MetricasServiceTests" -v minimal`
Expected: All passed.

- [ ] **Step 6: Expor no frontend**

Em `frontend/src/api/metricas.ts`, adicionar a interface e o campo em `MetricasPeriodo`:

```typescript
export interface TicketMedioMetrica {
  valor: number
  quantidadeRecebimentos: number
}
```
```typescript
  ticketMedio?: TicketMedioMetrica
```

Em `ClientDashboardPage.tsx`, no `stats-grid` de métricas (após o card de Receita do Mês / EBITDA):

```tsx
{metricas.ticketMedio && (
  <StatCard
    label="🎟️ Ticket Médio"
    value={fmtBRL(metricas.ticketMedio.valor)}
    className="val-blue"
    sub={`${metricas.ticketMedio.quantidadeRecebimentos} recebimentos`}
  />
)}
```

- [ ] **Step 7: Build + checks + commit**

```bash
dotnet test CaixaDiario.Tests -v minimal
cd frontend && npx tsc --noEmit && cd ..
git add CaixaDiario.API/ CaixaDiario.Tests/ frontend/src/
git commit -m "feat(fase1): Ticket Médio (receita / nº recebimentos)"
```

---

## Task A5 — Expor Burn Rate como métrica (1.3.4)

`burnMedioMensal` já é calculado em [MetricasService.cs:89-93](../../../CaixaDiario.API/Services/MetricasService.cs#L89-L93) mas só alimenta o Runway. Expor como métrica própria.

**Files:**
- Modify: `CaixaDiario.API/DTOs/Metricas/MetricasPeriodoDto.cs`
- Modify: `CaixaDiario.API/Services/MetricasService.cs`
- Modify: `CaixaDiario.Tests/Services/MetricasServiceTests.cs`
- Modify: `frontend/src/api/metricas.ts`
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1: DTO**

Em `MetricasPeriodoDto.cs`:

```csharp
public decimal? BurnRate { get; set; }
```

- [ ] **Step 2: Teste primeiro**

```csharp
[Fact]
public void CalcularPeriodo_ComSaidas3Meses_CalculaBurnRate()
{
    var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
    var registros = new List<RegistroDiario>
    {
        CriarRegistro(hoje, new() { Item("Venda", 100m, "Vendas", "Receita") },
            new() { Item("Custo", 900m, "Aluguel", "CustoFixo") }, saldoFinal: 1000m),
    };
    var resultado = _sut.CalcularPeriodo(registros, registros);
    Assert.NotNull(resultado.BurnRate);
    Assert.Equal(900m, resultado.BurnRate);
}
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test CaixaDiario.Tests --filter "CalcularPeriodo_ComSaidas3Meses_CalculaBurnRate" -v minimal`
Expected: FAIL.

- [ ] **Step 4: Implementar**

No bloco Runway de `CalcularPeriodo`, **logo após** calcular `burnMedioMensal` e antes de montar `dto.Runway`, adicionar:

```csharp
dto.BurnRate = burnMedioMensal > 0 ? Math.Round(burnMedioMensal, 2) : (decimal?)null;
```

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test CaixaDiario.Tests --filter "MetricasServiceTests" -v minimal`
Expected: All passed.

- [ ] **Step 6: Frontend**

Em `metricas.ts`, adicionar a `MetricasPeriodo`: `burnRate?: number`.

Em `ClientDashboardPage.tsx`, no grid de métricas:

```tsx
{metricas.burnRate != null && (
  <StatCard label="🔥 Burn Rate (mês)" value={fmtBRL(metricas.burnRate)} className="val-red" />
)}
```

- [ ] **Step 7: Checks + commit**

```bash
dotnet test CaixaDiario.Tests -v minimal
cd frontend && npx tsc --noEmit && cd ..
git add CaixaDiario.API/ CaixaDiario.Tests/ frontend/src/
git commit -m "feat(fase1): expor Burn Rate como métrica no dashboard"
```

---

## Task A6 — Corrigir fórmula do Índice de Liquidez (1.3.6) 🔒 D3

Hoje [MetricasService.cs:120](../../../CaixaDiario.API/Services/MetricasService.cs#L120) usa `indice = saldoAtual / contasPagarProximas` — falta somar Contas a Receber ao numerador. Sob D3: numerador = `saldoAtual + contasReceber pendentes próximos 30 dias`.

**Files:**
- Modify: `CaixaDiario.API/Services/MetricasService.cs`
- Modify: `CaixaDiario.Tests/Services/MetricasServiceTests.cs`

- [ ] **Step 1: Teste primeiro (novo cenário com Contas a Receber)**

```csharp
[Fact]
public void CalcularPeriodo_ComContasReceberProximos30Dias_SomaNoNumeradorLiquidez()
{
    var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
    var amanha = hoje.AddDays(1);
    var registro = new RegistroDiario
    {
        Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
        Entradas = new(), Saidas = new(), SaldoFinal = 1000m,
        ContasReceber = new() { new() { Descricao = "Cliente X", Valor = 2000m, DataVencimento = amanha, Pago = false } },
        ContasPagar = new() { new() { Descricao = "Aluguel", Valor = 1000m, DataVencimento = amanha, Pago = false } },
    };
    var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });
    // (1000 + 2000) / 1000 = 3.0
    Assert.NotNull(resultado.Liquidez);
    Assert.Equal(3.0m, resultado.Liquidez!.Indice);
}
```

> O teste existente `CalcularPeriodo_ComContasPagarProximos30Dias_CalculaLiquidez` (sem contas a receber) continua válido: `(3000 + 0)/1000 = 3.0`. Não precisa mudar.

- [ ] **Step 2: Rodar e ver o novo falhar**

Run: `dotnet test CaixaDiario.Tests --filter "SomaNoNumeradorLiquidez" -v minimal`
Expected: FAIL (índice atual = 1.0).

- [ ] **Step 3: Implementar a correção**

No bloco Liquidez de `CalcularPeriodo`, antes do `if (contasPagarProximas == 0)`, calcular as contas a receber próximas e o numerador:

```csharp
var contasReceberProximas = todosRegistros
    .SelectMany(r => r.ContasReceber)
    .Where(c => !c.Pago && c.DataVencimento.HasValue &&
                c.DataVencimento.Value >= hoje30 && c.DataVencimento.Value <= em30dias)
    .Sum(c => c.Valor);
var numeradorLiquidez = saldoAtual + contasReceberProximas;
```

E trocar o cálculo do índice de `saldoAtual / contasPagarProximas` para:

```csharp
var indice = Math.Round(numeradorLiquidez / contasPagarProximas, 2);
```

(O ramo `contasPagarProximas == 0 → AltaLiquidez` permanece igual.)

- [ ] **Step 4: Rodar todos os testes de métricas**

Run: `dotnet test CaixaDiario.Tests --filter "MetricasServiceTests" -v minimal`
Expected: All passed.

- [ ] **Step 5: Commit**

```bash
git add CaixaDiario.API/Services/MetricasService.cs CaixaDiario.Tests/Services/MetricasServiceTests.cs
git commit -m "fix(fase1): Liquidez = (Caixa + Contas a Receber 30d) / Contas a Pagar 30d"
```

---

## Task A7 — Múltiplo de Valuation configurável 3x/4x/5x/6x (1.2.6) 🔒 D4

Hoje o múltiplo é fixo `* 3` em [MetricasService.cs:74](../../../CaixaDiario.API/Services/MetricasService.cs#L74). Sob D4: parâmetro de tela, sem persistência.

**Files:**
- Modify: `CaixaDiario.API/Services/IMetricasService.cs`
- Modify: `CaixaDiario.API/Services/MetricasService.cs`
- Modify: `CaixaDiario.API/Controllers/MetricasController.cs`
- Modify: `CaixaDiario.Tests/Services/MetricasServiceTests.cs`
- Modify: `frontend/src/api/metricas.ts`
- Modify: `frontend/src/hooks/useMetricas.ts`
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1: Parâmetro opcional na interface (não quebra chamadas existentes)**

Em `IMetricasService.cs`, alterar a assinatura para incluir `multiplo` com default:

```csharp
MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo, decimal multiplo = 3m);
```

- [ ] **Step 2: Teste primeiro**

```csharp
[Fact]
public void CalcularPeriodo_MultiploCustomizado_AplicaNoValuation()
{
    var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
    var registros = new List<RegistroDiario>();
    for (int i = 2; i >= 0; i--)
        registros.Add(CriarRegistro(hoje.AddMonths(-i).AddDays(-5),
            new() { Item("Venda", 2000m, "Vendas", "Receita") },
            new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") }));

    var v3 = _sut.CalcularPeriodo(registros, registros, 3m).Valuation!.Valor;
    var v6 = _sut.CalcularPeriodo(registros, registros, 6m).Valuation!.Valor;
    Assert.Equal(v3 * 2, v6);
}
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test CaixaDiario.Tests --filter "MultiploCustomizado" -v minimal`
Expected: FAIL (compilação/valor).

- [ ] **Step 4: Implementar**

Na assinatura do método em `MetricasService.cs`:

```csharp
public MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo, decimal multiplo = 3m)
```

E trocar `var valuationValor = lucroMedioMensal * 12 * 3;` por:

```csharp
var valuationValor = lucroMedioMensal * 12 * multiplo;
```

- [ ] **Step 5: Controller aceita query param**

Em `MetricasController.ObterMetricas`, adicionar `[FromQuery] decimal multiplo = 3` e repassar:

```csharp
public async Task<IActionResult> ObterMetricas(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate, [FromQuery] decimal multiplo = 3)
{
    VerificarAcesso(clienteId);
    var todos = await _registroRepo.ListarPorClienteAsync(clienteId);
    var doPeriodo = todos.Where(r => r.Data >= de && r.Data <= ate).ToList();
    var resultado = _metricasService.CalcularPeriodo(todos, doPeriodo, multiplo);
    return Ok(new ApiResponse<MetricasPeriodoDto> { Dados = resultado });
}
```

- [ ] **Step 6: Rodar testes backend**

Run: `dotnet test CaixaDiario.Tests -v minimal`
Expected: All passed.

- [ ] **Step 7: Frontend — propagar `multiplo`**

Em `metricas.ts`, alterar `obterMetricas`:

```typescript
export async function obterMetricas(clienteId: string, de: string, ate: string, multiplo = 3): Promise<MetricasPeriodo> {
  const res = await apiFetch<ApiResponse<MetricasPeriodo>>(`/api/metricas/${clienteId}?de=${de}&ate=${ate}&multiplo=${multiplo}`)
  return res.dados
}
```

Em `useMetricas.ts`, aceitar `multiplo` e repassar (ler o hook atual e seguir o mesmo padrão de parâmetros/efeito; adicionar `multiplo` à lista de dependências do efeito que chama `obterMetricas`).

Em `ClientDashboardPage.tsx`, adicionar estado e seletor 3x/4x/5x/6x acima do card de Valuation:

```tsx
const [multiploValuation, setMultiploValuation] = useState(3)
```
```tsx
{metricas?.valuation && (
  <div style={{ display: 'flex', gap: 6, alignItems: 'center', margin: '8px 0' }}>
    <span style={{ fontSize: 12, color: 'var(--tx3)' }}>Múltiplo Valuation:</span>
    {[3, 4, 5, 6].map(m => (
      <button key={m} type="button" onClick={() => setMultiploValuation(m)}
        style={{ padding: '4px 10px', borderRadius: 6, border: '1px solid var(--bd)', cursor: 'pointer',
          background: multiploValuation === m ? '#0a84ff' : 'var(--bg-card)', color: multiploValuation === m ? '#fff' : 'var(--tx1)' }}>
        {m}x
      </button>
    ))}
  </div>
)}
```

Passar `multiploValuation` ao hook: `const { metricas, fluxo } = useMetricas(clienteId, de, ate, multiploValuation)`.

- [ ] **Step 8: Checks + commit**

```bash
cd frontend && npx tsc --noEmit && npm test -- --run && cd ..
dotnet test CaixaDiario.Tests -v minimal
git add CaixaDiario.API/ CaixaDiario.Tests/ frontend/src/
git commit -m "feat(fase1): múltiplo de Valuation configurável (3x/4x/5x/6x)"
```

---

## Task A8 — Gráfico pizza: Composição das Despesas (1.4.3) 🔒 D5

**Depende da taxonomia (Task B2).** O gráfico exige agrupar saídas em Pessoas, Custos Diretos, Administrativas, Impostos, Marketing, Financeiras. Esse agrupamento só é confiável depois de definida a taxonomia (D5). Enquanto D5 não fechar, **não implementar** com grupos inventados.

**Files:**
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1 (após B2): Função de agrupamento**

Usar o mapeamento categoria→grupo definido na Task B2 (exportado de um único lugar, ex.: `frontend/src/utils/categorias.ts`). Computar:

```tsx
import { PieChart, Pie, Cell, Tooltip, ResponsiveContainer, Legend } from 'recharts'
import { grupoDaCategoria, CORES_GRUPO } from '../../utils/categorias'

const composicaoDespesas = useMemo(() => {
  const acc: Record<string, number> = {}
  for (const r of doPeriodo)
    for (const s of r.saidas) {
      const grupo = grupoDaCategoria(s.categoria)
      acc[grupo] = (acc[grupo] ?? 0) + s.valor
    }
  return Object.entries(acc).map(([name, value]) => ({ name, value })).filter(d => d.value > 0)
}, [doPeriodo])
```

- [ ] **Step 2: Renderizar a pizza**

```tsx
{composicaoDespesas.length > 0 && (
  <div className="meta-card">
    <h3>🥧 Composição das Despesas</h3>
    <div style={{ height: 260 }}>
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie data={composicaoDespesas} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={90} label>
            {composicaoDespesas.map((d) => <Cell key={d.name} fill={CORES_GRUPO[d.name] ?? '#888'} />)}
          </Pie>
          <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} />
          <Legend />
        </PieChart>
      </ResponsiveContainer>
    </div>
  </div>
)}
```

- [ ] **Step 3: Checks + commit**

```bash
cd frontend && npx tsc --noEmit && cd ..
git add frontend/src/pages/client/ClientDashboardPage.tsx
git commit -m "feat(fase1): gráfico pizza de Composição das Despesas por grupo"
```

---

# FRENTE B — Caixa Diário

## Task B1 — Categoria obrigatória ao registrar saída (2.2.1)

Hoje não há validação ([ClientCaixaPage.tsx:60-75](../../../frontend/src/pages/client/ClientCaixaPage.tsx#L60-L75) salva sem checar categoria). Validar no **frontend** (UX) e no **backend** (garantia).

**Files:**
- Modify: `frontend/src/pages/client/ClientCaixaPage.tsx`
- Modify: `CaixaDiario.API/Services/RegistroService.cs`
- Modify: `CaixaDiario.Tests/Services/RegistroServiceTests.cs`

- [ ] **Step 1: Backend — teste primeiro (saída sem categoria deve falhar)**

Em `RegistroServiceTests.cs`, seguindo o padrão de testes existentes do arquivo, adicionar um teste que monta `CriarRegistroDto` com uma saída sem `Categoria` e espera `ApiException` 400:

```csharp
[Fact]
public async Task SalvarAsync_SaidaSemCategoria_LancaExcecao()
{
    var dto = new CriarRegistroDto
    {
        ClienteId = Guid.NewGuid(),
        Data = DateOnly.FromDateTime(DateTime.UtcNow),
        Entradas = new(),
        Saidas = new() { new ItemFinanceiroDto { Descricao = "Compra", Valor = 50m, Categoria = null } },
        ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
    };
    var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "tester"));
    Assert.Equal(400, ex.StatusCode);
}
```

> Ajustar a construção do `_sut` aos mocks já usados no arquivo `RegistroServiceTests.cs` (ler antes de escrever; reaproveitar o setup existente de `IRegistroRepository`, `IAuditService`, `IRecorrenciaService`).

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test CaixaDiario.Tests --filter "SaidaSemCategoria" -v minimal`
Expected: FAIL.

- [ ] **Step 3: Implementar a validação em `SalvarAsync`**

No início de `RegistroService.SalvarAsync`, após a checagem de data futura:

```csharp
if (dto.Saidas.Any(s => string.IsNullOrWhiteSpace(s.Categoria)))
    throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Toda saída deve ter uma categoria.", "categoria");
```

> Confirmar que `CodigoRetorno.DADOS_INVALIDOS` existe (já usado em `ExportController`/`ContaRecorrenteService`). Existe.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test CaixaDiario.Tests -v minimal`
Expected: All passed.

- [ ] **Step 5: Frontend — bloquear salvar sem categoria nas saídas**

Em `ClientCaixaPage.handleSave`, antes do `await salvar(...)`:

```tsx
const saidasValidas = saidas.filter(s => s.descricao || s.valor)
if (saidasValidas.some(s => !s.categoria)) {
  setSaveSuccess(false)
  setMsg('Selecione uma categoria para cada saída.')
  return
}
```

E marcar o `<select>` de saída como obrigatório visualmente (borda de alerta quando vazio) — opcional, mas trocar o salvar para usar `saidasValidas`.

- [ ] **Step 6: Atualizar o teste do frontend**

Ajustar `ClientCaixaPage.test.tsx` (já modificado no working tree) para cobrir: salvar sem categoria em saída exibe a mensagem e não chama a API. Seguir o padrão de mocks já presente no teste.

- [ ] **Step 7: Checks + commit**

```bash
cd frontend && npx tsc --noEmit && npm test -- --run && cd ..
dotnet test CaixaDiario.Tests -v minimal
git add CaixaDiario.API/ CaixaDiario.Tests/ frontend/src/
git commit -m "feat(fase1): categoria obrigatória em saídas (frontend + backend)"
```

---

## Task B2 — Taxonomia de categorias com grupos e subcategorias (2.2.2) 🔒 D5

**ALTO IMPACTO — requer sign-off (D5).** Hoje `CategoriasController` retorna uma lista **plana** com `tipoCusto` ([CategoriasController.cs:11-29](../../../CaixaDiario.API/Controllers/CategoriasController.cs#L11-L29)). O documento original pede grupos: **Custos Diretos, Pessoas, Despesas Administrativas, Marketing, Impostos, Financeiras, Investimentos** com subcategorias. EBITDA/Prime Cost dependem hoje dos nomes `"Salários/Folha"`, `"Insumos/Mercadoria"`, `"Manutenção"` ([MetricasService.cs:14,32-33,44](../../../CaixaDiario.API/Services/MetricasService.cs#L14)).

### Proposta de taxonomia (a CONFIRMAR — não é decisão tomada)

| Grupo | tipoCusto | Subcategorias propostas |
|-------|-----------|--------------------------|
| Custos Diretos | CustoVariavel | Insumos/Mercadoria, Embalagens, Comissões |
| Pessoas | CustoFixo | Salários/Folha, Encargos, Benefícios, Pró-labore |
| Despesas Administrativas | CustoFixo | Aluguel, Energia/Água/Internet, Seguros, Manutenção, Material de Escritório |
| Marketing | CustoVariavel | Publicidade, Mídia paga, Material gráfico |
| Impostos | CustoFixo | Simples/DAS, ISS, Outros tributos |
| Financeiras | CustoFixo | Tarifas bancárias, Juros, IOF |
| Investimentos | CustoFixo | Equipamentos, Reformas, Software |

> **Pré-requisito de implementação:** definir (a) lista final de subcategorias; (b) mapeamento das categorias **já gravadas** nos registros para os novos grupos (migração de dados ou camada de compatibilidade); (c) revisar se EBITDA (que exclui `"Manutenção"`) e Prime Cost (`"Salários/Folha"` + `"Insumos/Mercadoria"`) continuam corretos com os novos nomes. **Sem isso, não alterar `CategoriasController`** — quebraria os cálculos testados.

**Files (após D5 fechado):**
- Modify: `CaixaDiario.API/Controllers/CategoriasController.cs`
- Modify: `frontend/src/types.ts` (estrutura grupo→subcategorias)
- Create: `frontend/src/utils/categorias.ts` (mapa categoria→grupo + cores, reutilizado pela pizza A8)
- Modify: `frontend/src/pages/client/ClientCaixaPage.tsx` (select agrupado `<optgroup>`)
- Modify: `CaixaDiario.API/Services/MetricasService.cs` (se os nomes de categoria mudarem, ajustar as chaves de EBITDA/PrimeCost — com testes atualizados)

- [ ] **Step 1:** Registrar a taxonomia final aprovada (estrutura abaixo) em `CategoriasController`:

```csharp
// EXEMPLO de forma — preencher subcategorias APÓS aprovação (D5)
saidas = new[]
{
    new { grupo = "Custos Diretos", nome = "Insumos/Mercadoria", tipoCusto = "CustoVariavel" },
    new { grupo = "Pessoas", nome = "Salários/Folha", tipoCusto = "CustoFixo" },
    // ... demais conforme tabela aprovada
}
```

- [ ] **Step 2:** Atualizar `frontend/src/types.ts` `CategoriaItem` para incluir `grupo`, e o `<select>` do caixa para agrupar por `<optgroup label={grupo}>`.

- [ ] **Step 3:** Criar `frontend/src/utils/categorias.ts` com `grupoDaCategoria(cat?: string): string` e `CORES_GRUPO: Record<string,string>` (consumidos pela pizza A8).

- [ ] **Step 4:** Se algum nome de categoria-chave mudar, **primeiro** atualizar os testes de `MetricasServiceTests` (EBITDA/PrimeCost) e o `MetricasService`, rodando `dotnet test` até verde.

- [ ] **Step 5: Checks + commit**

```bash
dotnet test CaixaDiario.Tests -v minimal
cd frontend && npx tsc --noEmit && npm test -- --run && cd ..
git add CaixaDiario.API/ CaixaDiario.Tests/ frontend/src/
git commit -m "feat(fase1): taxonomia de categorias com grupos e subcategorias"
```

---

## Task B3 — Relatório de total gasto por categoria (2.3.1)

A exportação XLSX lista itens por categoria, mas **não agrega** ([ExportController.cs:68-91](../../../CaixaDiario.API/Controllers/ExportController.cs#L68-L91)). Falta um relatório que **some** o gasto por categoria. Derivável de `registros` no frontend.

**Files:**
- Modify: `frontend/src/pages/client/ClientGraficoPage.tsx` (adicionar seção) **ou** Create: `frontend/src/pages/client/ClientRelatorioCategoriaPage.tsx`

> **Decisão menor:** colocar como nova seção na página de Gráficos (recomendado, menos navegação) ou página própria. Default: seção em `ClientGraficoPage`.

- [ ] **Step 1: Agregar saídas por categoria (na página de Gráficos)**

`ClientGraficoPage` hoje usa `obterEvolucao`. Para ter os itens por categoria precisa dos `registros` — usar `useRegistros(clienteId)` (mesmo hook das outras páginas) e filtrar pelo período já existente. Adicionar:

```tsx
const totaisPorCategoria = useMemo(() => {
  const acc: Record<string, number> = {}
  for (const r of registros)
    for (const s of r.saidas) {
      const cat = s.categoria ?? 'Sem categoria'
      acc[cat] = (acc[cat] ?? 0) + s.valor
    }
  return Object.entries(acc).map(([categoria, total]) => ({ categoria, total })).sort((a, b) => b.total - a.total)
}, [registros])
const totalGeral = totaisPorCategoria.reduce((s, c) => s + c.total, 0)
```

- [ ] **Step 2: Renderizar a tabela**

```tsx
<h3 style={{ fontSize: 15, fontWeight: 700, margin: '24px 0 14px', color: '#888' }}>💸 Total Gasto por Categoria</h3>
<div style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 14, padding: 20 }}>
  {totaisPorCategoria.length === 0 && <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Sem saídas no período.</p>}
  {totaisPorCategoria.map(c => (
    <div key={c.categoria} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid var(--bd)' }}>
      <span>{c.categoria}</span>
      <span>{fmtBRL(c.total)} · {totalGeral > 0 ? ((c.total / totalGeral) * 100).toFixed(1) : '0'}%</span>
    </div>
  ))}
</div>
```

- [ ] **Step 3: Checks + commit**

```bash
cd frontend && npx tsc --noEmit && npm test -- --run && cd ..
git add frontend/src/pages/client/
git commit -m "feat(fase1): relatório de total gasto por categoria"
```

---

# FRENTE C — Contas a Pagar e Receber

## Task C1 — Recorrência multifrequência + quantidade de parcelas (3.1.1, 3.1.2) 🔒 D6

Hoje `ContaRecorrente` **não tem** periodicidade nem parcelas; a materialização é **mensal implícita** ([RecorrenciaService.cs:17-82](../../../CaixaDiario.API/Services/RecorrenciaService.cs#L17-L82)) e o fluxo projeta por `DataInicio.Day == dia.Day` ([MetricasService.cs:180](../../../CaixaDiario.API/Services/MetricasService.cs#L180)).

**Files:**
- Modify: `CaixaDiario.API/Models/ContaRecorrente.cs`
- Modify: `CaixaDiario.API/DTOs/ContasRecorrentes/CriarContaRecorrenteDto.cs`, `ContaRecorrenteDto.cs`, `AtualizarContaRecorrenteDto.cs`
- Modify: `CaixaDiario.API/Services/ContaRecorrenteService.cs`
- Modify: `CaixaDiario.API/Services/RecorrenciaService.cs`
- Modify: `CaixaDiario.API/Services/MetricasService.cs` (CalcularFluxoProjetado)
- Create: EF migration
- Modify: `frontend/src/types.ts`, `frontend/src/api/contasRecorrentes.ts`, `frontend/src/pages/client/ClientContasPage.tsx`
- Modify: `CaixaDiario.Tests/Services/RecorrenciaServiceTests.cs`

- [ ] **Step 1: Adicionar campos ao modelo**

Em `ContaRecorrente.cs`:

```csharp
public string Periodicidade { get; set; } = "Mensal"; // "Semanal" | "Quinzenal" | "Mensal" | "Trimestral" | "Semestral" | "Anual"
public int? QuantidadeParcelas { get; set; }
```

> "Não recorrente" (D6 default) = lançamento único via fluxo de Conta provisionada normal (`ClientContasPage` já o suporta), **não** entra em `ContaRecorrente`. O `<select>` do frontend oferecerá "Não recorrente" apenas como atalho que usa o formulário de conta única já existente.

- [ ] **Step 2: Migration**

```
cd CaixaDiario.API && dotnet ef migrations add AdicionarPeriodicidadeEParcelas
```
Expected: migration criada seguindo a convenção dos arquivos em `Migrations/`. Revisar o `Up`/`Down` gerado (default `"Mensal"` para linhas existentes, preservando o comportamento atual).

- [ ] **Step 3: DTOs**

Adicionar `Periodicidade` (string, default "Mensal") e `QuantidadeParcelas` (int?) em `CriarContaRecorrenteDto`, `ContaRecorrenteDto` e (opcional) `AtualizarContaRecorrenteDto`. Mapear em `ContaRecorrenteService.CriarAsync`/`MapToDto`.

- [ ] **Step 4: Validar periodicidade em `CriarAsync`**

Após a validação de `Tipo`:

```csharp
var periodicidadesValidas = new[] { "Semanal", "Quinzenal", "Mensal", "Trimestral", "Semestral", "Anual" };
if (!periodicidadesValidas.Contains(dto.Periodicidade))
    throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Periodicidade inválida.", "periodicidade");
```

- [ ] **Step 5: Função de ocorrências (teste primeiro)**

A lógica central é "dada uma conta recorrente e uma data, ela ocorre nesse dia?". Criar um helper estático testável em `RecorrenciaService` (ou utilitário) e testá-lo em `RecorrenciaServiceTests`:

```csharp
public static bool OcorreEm(ContaRecorrente c, DateOnly dia)
{
    if (dia < c.DataInicio) return false;
    if (c.DataFim.HasValue && dia > c.DataFim.Value) return false;

    var ocorrencias = 0;
    bool bate = c.Periodicidade switch
    {
        "Semanal"    => (dia.DayNumber - c.DataInicio.DayNumber) % 7 == 0,
        "Quinzenal"  => (dia.DayNumber - c.DataInicio.DayNumber) % 14 == 0,
        "Mensal"     => dia.Day == c.DataInicio.Day,
        "Trimestral" => dia.Day == c.DataInicio.Day && (DiffMeses(c.DataInicio, dia) % 3 == 0),
        "Semestral"  => dia.Day == c.DataInicio.Day && (DiffMeses(c.DataInicio, dia) % 6 == 0),
        "Anual"      => dia.Day == c.DataInicio.Day && dia.Month == c.DataInicio.Month,
        _ => false,
    };
    if (!bate) return false;

    if (c.QuantidadeParcelas.HasValue)
    {
        ocorrencias = ContarOcorrenciasAte(c, dia); // 1-based índice desta ocorrência
        if (ocorrencias > c.QuantidadeParcelas.Value) return false;
    }
    return true;
}

private static int DiffMeses(DateOnly a, DateOnly b) => (b.Year - a.Year) * 12 + (b.Month - a.Month);
```

> `ContarOcorrenciasAte` e o tratamento de fim-de-mês (ex.: dia 31 em meses com 30 dias) fazem parte de D6 — **definir a regra de borda antes de implementar** e cobrir com testes (`OcorreEm_Semanal_...`, `OcorreEm_Mensal_...`, `OcorreEm_RespeitaQuantidadeParcelas`). Escrever os testes primeiro, ver falhar, implementar, ver passar.

- [ ] **Step 6: Usar `OcorreEm` no fluxo projetado**

Em `MetricasService.CalcularFluxoProjetado`, trocar as duas condições de recorrência (`r.DataInicio.Day == dia.Day`) por `RecorrenciaService.OcorreEm(r, dia)`:

```csharp
var entradasRec = recorrentes.Where(r => r.Tipo == "Receber" && r.Ativo && RecorrenciaService.OcorreEm(r, dia)).Sum(r => r.Valor);
var saidasRec   = recorrentes.Where(r => r.Tipo == "Pagar"   && r.Ativo && RecorrenciaService.OcorreEm(r, dia)).Sum(r => r.Valor);
```

Atualizar o teste `CalcularFluxoProjetado_*` se necessário (o cenário sem contas continua válido).

- [ ] **Step 7: Materialização do mês honra periodicidade**

Em `RecorrenciaService.MaterializarMesAtualAsync`, materializar para **cada dia do mês em que `OcorreEm` é verdadeiro** (hoje materializa uma vez no dia de hoje). Definir, com D6, se a materialização cria provisões para todas as ocorrências do mês ou apenas as do dia. Ajustar com testes.

- [ ] **Step 8: Frontend**

Em `types.ts`, adicionar a `ContaRecorrente`: `periodicidade: string` e `quantidadeParcelas?: number`. Em `api/contasRecorrentes.ts`, propagar no `criarContaRecorrente`. Em `ClientContasPage.tsx`, adicionar ao formulário recorrente um `<select>` de periodicidade e um input opcional de parcelas:

```tsx
<select value={novaRecPeriodicidade} onChange={e => setNovaRecPeriodicidade(e.target.value)}>
  <option value="Mensal">Mensal</option>
  <option value="Semanal">Semanal</option>
  <option value="Quinzenal">Quinzenal</option>
  <option value="Trimestral">Trimestral</option>
  <option value="Semestral">Semestral</option>
  <option value="Anual">Anual</option>
</select>
<input type="number" min="1" placeholder="Parcelas (opcional)" value={novaRecParcelas} onChange={e => setNovaRecParcelas(e.target.value)} />
```

Exibir periodicidade/parcelas na listagem de recorrentes ([ClientContasPage.tsx:207-215](../../../frontend/src/pages/client/ClientContasPage.tsx#L207-L215)).

- [ ] **Step 9: Checks + commit**

```bash
dotnet test CaixaDiario.Tests -v minimal
cd frontend && npx tsc --noEmit && npm test -- --run && cd ..
git add CaixaDiario.API/ CaixaDiario.Tests/ frontend/src/
git commit -m "feat(fase2): recorrência multifrequência + quantidade de parcelas"
```

---

## Task C2 — Baixa: botão "Pagar"/"Receber", data da baixa e movimentação do caixa (3.2.1, 3.2.2) 🔒 D7

Hoje a baixa é um checkbox que só inverte `pago` ([ClientContasPage.tsx:129](../../../frontend/src/pages/client/ClientContasPage.tsx#L129)); `ContaProvisionada` não tem data de baixa e não há movimentação de caixa. Existe `AplicarBaixaAutomatica` ([RegistroService.cs:123-131](../../../CaixaDiario.API/Services/RegistroService.cs#L123-L131)) que marca `Pago` quando vencimento == data do registro — reaproveitar a ideia.

**Files:**
- Modify: `CaixaDiario.API/Models/ContaProvisionada.cs`
- Modify: `CaixaDiario.API/DTOs/Registros/ContaProvisionadaDto.cs`
- Modify: `CaixaDiario.API/Services/RegistroService.cs`
- Create: EF migration
- Modify: `frontend/src/types.ts`, `frontend/src/pages/client/ClientContasPage.tsx`
- Modify: `CaixaDiario.Tests/Services/RegistroServiceTests.cs`

- [ ] **Step 1: Adicionar `DataBaixa` ao modelo e DTO**

Em `ContaProvisionada.cs`:

```csharp
public DateOnly? DataBaixa { get; set; }
```
Em `ContaProvisionadaDto.cs`: mesma propriedade. Mapear em `RegistroService.MapContaDto` e `MapToDto` (linhas 136-148).

- [ ] **Step 2: Migration**

```
cd CaixaDiario.API && dotnet ef migrations add AdicionarDataBaixaContaProvisionada
```
Expected: migration criada; revisar `Up`/`Down`.

- [ ] **Step 3: Definir a semântica de baixa (D7) e testar**

Sob o default D7: ao marcar `Pago = true`, gravar `DataBaixa = <hoje>` e **criar um `ItemFinanceiro`** correspondente no registro do dia da baixa (entrada se a conta é a receber; saída se a pagar), **sem** mexer em `saldoConfirmado`. Escrever o teste primeiro em `RegistroServiceTests` cobrindo: conta a pagar baixada gera uma `Saida` com a descrição/valor da conta e `DataBaixa` preenchida.

> **Atenção (D7):** definir como evitar dupla contagem com o `saldoConfirmado` manual e o que acontece ao **desfazer** a baixa (remover o item criado?). Sem essa definição, **não** ativar a movimentação automática — implementar só `DataBaixa` + status.

- [ ] **Step 4: Implementar conforme decisão**

Estender `AplicarBaixaAutomatica` (ou criar `AplicarBaixa`) para setar `DataBaixa` quando `Pago` passa a `true` e gerar o item financeiro conforme D7. Manter idempotência (não duplicar item se já baixado).

- [ ] **Step 5: Frontend — botão "Pagar"/"Receber"**

Em `ClientContasPage.renderConta`, substituir o checkbox por um botão rotulado conforme o tipo e estado:

```tsx
<button
  onClick={() => togglePago(view)}
  style={{ padding: '4px 12px', borderRadius: 6, border: 'none', cursor: 'pointer', fontSize: 12, fontWeight: 600,
    background: view.conta.pago ? 'var(--bd)' : view.tipo === 'receber' ? '#34c759' : '#ff3b30',
    color: view.conta.pago ? 'var(--tx3)' : '#fff' }}>
  {view.conta.pago
    ? `✓ ${view.tipo === 'receber' ? 'Recebido' : 'Pago'}${view.conta.dataBaixa ? ` em ${fmtDate(view.conta.dataBaixa)}` : ''}`
    : view.tipo === 'receber' ? 'Receber' : 'Pagar'}
</button>
```

Adicionar `dataBaixa?: string` a `ContaProvisionada` em `types.ts`.

- [ ] **Step 6: Checks + commit**

```bash
dotnet test CaixaDiario.Tests -v minimal
cd frontend && npx tsc --noEmit && npm test -- --run && cd ..
git add CaixaDiario.API/ CaixaDiario.Tests/ frontend/src/
git commit -m "feat(fase2): baixa financeira com data e botão Pagar/Receber"
```

---

## Task C3 — Painel "Vencem Hoje" e lista "Próximos 7 dias" (3.3.1, 3.3.2)

Hoje há um painel único "Alertas de Vencimento" com janela fixa de 3 dias ([alertas.ts:11](../../../frontend/src/utils/alertas.ts#L11), [ClientDashboardPage.tsx:24,133-148](../../../frontend/src/pages/client/ClientDashboardPage.tsx#L133-L148)). Falta separar "Vencem Hoje" e estender para 7 dias.

**Files:**
- Modify: `frontend/src/utils/alertas.ts`
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1: Adicionar bucketização em `alertas.ts`**

Adicionar função que classifica em "hoje" / "próximos 7 dias" (sem remover `getContasEmRisco`, que outros pontos podem usar):

```typescript
export interface ContasAgrupadas {
  vencemHoje: ContaEmRisco[]
  proximos7Dias: ContaEmRisco[]
}

export function agruparVencimentos(registros: Registro[]): ContasAgrupadas {
  const hoje = new Date(); hoje.setHours(0, 0, 0, 0)
  const isoHoje = hoje.toISOString().slice(0, 10)
  const em7 = new Date(hoje); em7.setDate(em7.getDate() + 7)

  const vencemHoje: ContaEmRisco[] = []
  const proximos7Dias: ContaEmRisco[] = []

  for (const reg of registros) {
    const verificar = (contas: ContaProvisionada[], tipo: 'receber' | 'pagar') => {
      contas.forEach((c, i) => {
        if (c.pago || !c.dataVencimento) return
        const venc = new Date(c.dataVencimento + 'T00:00:00')
        const item: ContaEmRisco = { registroData: reg.data, tipo, index: i, conta: c, vencida: venc < hoje }
        if (c.dataVencimento === isoHoje) vencemHoje.push(item)
        else if (venc > hoje && venc <= em7) proximos7Dias.push(item)
      })
    }
    verificar(reg.contasAReceber, 'receber')
    verificar(reg.contasAPagar, 'pagar')
  }
  return { vencemHoje, proximos7Dias }
}
```

- [ ] **Step 2: Renderizar os dois painéis no dashboard**

Substituir/complementar o bloco de alertas atual usando `agruparVencimentos(registros)`:

```tsx
const { vencemHoje, proximos7Dias } = useMemo(() => agruparVencimentos(registros), [registros])
```
```tsx
{vencemHoje.length > 0 && (
  <div className="meta-card" style={{ borderColor: '#ff3b30' }}>
    <h3>🔴 Vencem Hoje ({vencemHoje.length})</h3>
    {vencemHoje.map((c, i) => (
      <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid var(--bd)', fontSize: 13 }}>
        <span>{c.tipo === 'receber' ? '📥' : '📤'} {c.conta.descricao}</span>
        <span>{fmtBRL(c.conta.valor)}</span>
      </div>
    ))}
  </div>
)}
{proximos7Dias.length > 0 && (
  <div className="meta-card" style={{ borderColor: '#ff9500' }}>
    <h3>🗓️ Próximos 7 dias ({proximos7Dias.length})</h3>
    {proximos7Dias.map((c, i) => (
      <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid var(--bd)', fontSize: 13 }}>
        <span>{c.tipo === 'receber' ? '📥' : '📤'} {c.conta.descricao}</span>
        <span>{fmtBRL(c.conta.valor)} · {c.conta.dataVencimento}</span>
      </div>
    ))}
  </div>
)}
```

Atualizar o import: `import { getContasEmRisco, agruparVencimentos } from '../../utils/alertas'`.

- [ ] **Step 3: Checks + commit**

```bash
cd frontend && npx tsc --noEmit && npm test -- --run && cd ..
git add frontend/src/utils/alertas.ts frontend/src/pages/client/ClientDashboardPage.tsx
git commit -m "feat(fase2): painéis 'Vencem Hoje' e 'Próximos 7 dias'"
```

---

# Cobertura (Self-Review) — itens da auditoria → tasks

| ID auditoria | Status original | Task | Observação |
|--------------|-----------------|------|------------|
| 1.1.1 | 🟡 | A1 | Card dedicado com barra |
| 1.1.2 | ❌ | A1 (parcial) / D1 | Mensal derivado; "definir por mês" depende de D1 |
| 1.1.3 | ❌ | A1 | Barra de progresso |
| 1.1.4 | ❌ | A1 | % atingido |
| 1.1.5 | ❌ | A1 | Valor faltante |
| 1.2.1 | 🟡 | A2 | Mês atual/anterior/variação % |
| 1.2.2 | 🟡 | A3 | Atalhos Hoje / 30 dias |
| 1.2.6 | 🟡 | A7 / D4 | Múltiplo configurável |
| 1.3.2 | ❌ | A4 / D2 | Ticket Médio |
| 1.3.4 | 🟡 | A5 | Burn Rate exposto |
| 1.3.6 | 🟡 | A6 / D3 | Liquidez com Contas a Receber |
| 1.4.3 | ❌ | A8 / D5 | Pizza (depende de B2) |
| 2.1.1 | 🟡 | B1-adjacente | Cor do botão de entrada — ajustar classe `btn-add-receber` (azul) no caixa; ver nota abaixo |
| 2.2.1 | ❌ | B1 | Categoria obrigatória |
| 2.2.2 | ❌ | B2 / D5 | Taxonomia com grupos |
| 2.3.1 | ❌ | B3 | Relatório por categoria |
| 3.1.1 | ❌ | C1 / D6 | Periodicidades |
| 3.1.2 | 🟡 | C1 / D6 | Parcelas |
| 3.2.1 | 🟡 | C2 | Botão Pagar/Receber |
| 3.2.2 | 🟡 | C2 / D7 | Data baixa + movimenta caixa |
| 3.3.1 | 🟡 | C3 | Vencem Hoje |
| 3.3.2 | ❌ | C3 | Próximos 7 dias |

> **Item 2.1.1 (botão de entrada azul):** mudança trivial não coberta por task própria — no `ClientCaixaPage.tsx`, trocar a `className` do botão de entrada (linha ~135) de `btn-add-saida` para `btn-add-receber` (classe azul `#5ac8fa` **já existente** em `ClientCaixa.css:31-36`) e padronizar o rótulo para "Adicionar Entrada". Incluir no commit da Task B1.

## Ordem de execução sugerida

1. **Desbloquear decisões** D1–D7 com o cliente (especialmente **D5**, que destrava B2 e A8, e **D6/D7**, de maior impacto técnico).
2. **Frente A** (A2, A3, A5, A6, A4, A7, A1) — quase tudo aditivo/frontend, baixo risco, valor rápido.
3. **Frente B** (B1, B3) imediatas; **B2** só após D5; **A8** após B2.
4. **Frente C** (C3 imediata; C1 e C2 após D6/D7 — exigem migrations e mais testes).

## Notas de segurança / não-regressão

- `CalcularPeriodo` ganha parâmetro **opcional** (`multiplo = 3m`) — chamadas e testes existentes seguem compilando.
- Liquidez (A6): o teste existente sem contas a receber permanece com o mesmo resultado.
- Categorias (B2): **não** mexer nos nomes `"Salários/Folha"`, `"Insumos/Mercadoria"`, `"Manutenção"` sem atualizar `MetricasService` + testes na mesma task.
- Migrations (C1, C2): rodar `dotnet ef database update` em ambiente local e revisar o snapshot antes de commitar; default das colunas preserva o comportamento atual.
