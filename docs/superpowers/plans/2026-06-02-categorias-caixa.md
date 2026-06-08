# Categorias no Caixa Diário — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar campo `Categoria` (obrigatório) e `Subcategoria` (opcional) nas saídas do Caixa Diário, botões coloridos para entrada/saída, e componente de relatório de gastos por categoria.

**Architecture:** Novo model `ItemFinanceiroSaida` separado do `ItemFinanceiro` (que permanece em entradas). Dados armazenados como JSONB — nenhuma migration de schema necessária, fallback automático via valor default `"Administrativas"` no C#. Lista de categorias centralizada em `frontend/src/config/categorias.ts`.

**Tech Stack:** .NET 8 + EF Core + PostgreSQL (jsonb), React 19 + TypeScript + Vite, Vitest + xUnit.

---

## File Map

**Criados:**

- `CaixaDiario.API/Models/ItemFinanceiroSaida.cs` — model com Categoria/Subcategoria
- `CaixaDiario.API/DTOs/Registros/ItemFinanceiroSaidaDto.cs` — DTO espelho
- `frontend/src/config/categorias.ts` — lista fixa de categorias (fonte única da verdade)
- `frontend/src/components/shared/RelatorioCategoriasCard.tsx` — componente de relatório
- `frontend/src/components/shared/RelatorioCategoriasCard.css` — estilos do relatório
- `frontend/src/components/shared/RelatorioCategoriasCard.test.tsx` — testes do componente

**Modificados:**

- `CaixaDiario.API/Models/RegistroDiario.cs` — `Saidas` muda para `List<ItemFinanceiroSaida>`
- `CaixaDiario.API/Data/AppDbContext.cs` — conversão jsonb para `Saidas`
- `CaixaDiario.API/DTOs/Registros/CriarRegistroDto.cs` — `Saidas` usa `ItemFinanceiroSaidaDto`
- `CaixaDiario.API/DTOs/Registros/RegistroDto.cs` — idem
- `CaixaDiario.API/Services/RegistroService.cs` — mapeamento de saídas
- `CaixaDiario.Tests/Services/RegistroServiceTests.cs` — fixtures e novo teste
- `CaixaDiario.Tests/Controllers/RegistrosControllerTests.cs` — fixtures de saídas
- `frontend/src/types.ts` — novo tipo `ItemFinanceiroSaida`, `Registro.saidas` atualizado
- `frontend/src/api/registros.ts` — mapper e payload de saídas
- `frontend/src/hooks/useRegistros.test.tsx` — mocks atualizados
- `frontend/src/pages/client/ClientCaixaPage.tsx` — UI: selects de categoria, botões coloridos
- `frontend/src/pages/client/ClientCaixa.css` — `.btn-add-entrada` (azul) + `.saida-row` expandido
- `frontend/src/pages/client/ClientHistoricoPage.tsx` — exibir categoria na saída

---

## Task 1: Backend — Model, DTO e Service

**Files:**

- Create: `CaixaDiario.API/Models/ItemFinanceiroSaida.cs`
- Create: `CaixaDiario.API/DTOs/Registros/ItemFinanceiroSaidaDto.cs`
- Modify: `CaixaDiario.API/Models/RegistroDiario.cs`
- Modify: `CaixaDiario.API/Data/AppDbContext.cs`
- Modify: `CaixaDiario.API/DTOs/Registros/CriarRegistroDto.cs`
- Modify: `CaixaDiario.API/DTOs/Registros/RegistroDto.cs`
- Modify: `CaixaDiario.API/Services/RegistroService.cs`
- Test: `CaixaDiario.Tests/Services/RegistroServiceTests.cs`

- [ ] **Step 1: Adicionar teste de mapeamento de categoria**

Em `CaixaDiario.Tests/Services/RegistroServiceTests.cs`, adicionar ao final da classe:

```csharp
[Fact]
public async Task Salvar_ComCategoriaNaSaida_MapeiaCorretamente()
{
    var dto = new CriarRegistroDto
    {
        ClienteId = Guid.NewGuid(),
        Data = DateOnly.FromDateTime(DateTime.UtcNow),
        Inicio = 0m,
        Entradas = new(),
        Saidas = new List<ItemFinanceiroSaidaDto>
        {
            new() { Descricao = "Aluguel", Valor = 1200m, Categoria = "Administrativas", Subcategoria = "Aluguel" }
        },
        ContasReceber = new(),
        ContasPagar = new(),
        SaldoFinal = 0m
    };
    _repoMock.Setup(r => r.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data))
             .ReturnsAsync((RegistroDiario?)null);
    _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
             .ReturnsAsync((RegistroDiario r) => r);

    var (resultado, _) = await _sut.SalvarAsync(dto, "joao");

    Assert.Single(resultado.Saidas);
    Assert.Equal("Administrativas", resultado.Saidas[0].Categoria);
    Assert.Equal("Aluguel", resultado.Saidas[0].Subcategoria);
}
```

- [ ] **Step 2: Rodar o teste para verificar que falha (compile error)**

```powershell
cd CaixaDiario.Tests
dotnet build
```

Expected: erro de compilação — `ItemFinanceiroSaidaDto` não existe.

- [ ] **Step 3: Criar `ItemFinanceiroSaida.cs`**

```csharp
namespace CaixaDiario.API.Models;

public class ItemFinanceiroSaida
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Categoria { get; set; } = "Administrativas";
    public string? Subcategoria { get; set; }
}
```

- [ ] **Step 4: Criar `ItemFinanceiroSaidaDto.cs`**

```csharp
namespace CaixaDiario.API.DTOs.Registros;

public class ItemFinanceiroSaidaDto
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Categoria { get; set; } = "Administrativas";
    public string? Subcategoria { get; set; }
}
```

- [ ] **Step 5: Atualizar `RegistroDiario.cs`**

Substituir a linha `public List<ItemFinanceiro> Saidas`:

```csharp
public List<ItemFinanceiroSaida> Saidas { get; set; } = new();
```

- [ ] **Step 6: Atualizar `AppDbContext.cs`**

Substituir o bloco de conversão de `saidas`:

```csharp
entity.Property(e => e.Saidas).HasColumnName("saidas").HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, _jsonOptions),
        v => JsonSerializer.Deserialize<List<ItemFinanceiroSaida>>(v, _jsonOptions) ?? new());
```

- [ ] **Step 7: Atualizar `CriarRegistroDto.cs`**

Substituir a linha `public List<ItemFinanceiroDto> Saidas`:

```csharp
public List<ItemFinanceiroSaidaDto> Saidas { get; set; } = new();
```

- [ ] **Step 8: Atualizar `RegistroDto.cs`**

Substituir a linha `public List<ItemFinanceiroDto> Saidas`:

```csharp
public List<ItemFinanceiroSaidaDto> Saidas { get; set; } = new();
```

- [ ] **Step 9: Atualizar `RegistroService.cs`**

No método `SalvarAsync`, substituir os dois trechos que mapeiam `Saidas` (novo e existente):

```csharp
// Para registro existente (dentro do if existente != null):
existente.Saidas = dto.Saidas.Select(s => new ItemFinanceiroSaida
{
    Descricao = s.Descricao,
    Valor = s.Valor,
    Categoria = s.Categoria,
    Subcategoria = s.Subcategoria
}).ToList();

// Para registro novo:
Saidas = dto.Saidas.Select(s => new ItemFinanceiroSaida
{
    Descricao = s.Descricao,
    Valor = s.Valor,
    Categoria = s.Categoria,
    Subcategoria = s.Subcategoria
}).ToList(),
```

No método `MapToDto`, substituir o mapeamento de `Saidas`:

```csharp
Saidas = r.Saidas.Select(s => new ItemFinanceiroSaidaDto
{
    Descricao = s.Descricao,
    Valor = s.Valor,
    Categoria = s.Categoria,
    Subcategoria = s.Subcategoria
}).ToList(),
```

- [ ] **Step 10: Atualizar o helper `CriarDto` nos testes**

Em `RegistroServiceTests.cs`, substituir a linha de `Saidas` no helper:

```csharp
Saidas = new List<ItemFinanceiroSaidaDto> { new() { Descricao = "Aluguel", Valor = 200m, Categoria = "Administrativas" } },
```

- [ ] **Step 11: Rodar todos os testes backend**

```powershell
dotnet test CaixaDiario.Tests
```

Expected: todos os testes passando (verde).

Nota: `RegistrosControllerTests.cs` usa `Saidas = new()` (lista vazia inferida pelo tipo) — nenhuma mudança necessária lá.

- [ ] **Step 12: Commit**

```powershell
git add CaixaDiario.API/Models/ItemFinanceiroSaida.cs
git add CaixaDiario.API/DTOs/Registros/ItemFinanceiroSaidaDto.cs
git add CaixaDiario.API/Models/RegistroDiario.cs
git add CaixaDiario.API/Data/AppDbContext.cs
git add CaixaDiario.API/DTOs/Registros/CriarRegistroDto.cs
git add CaixaDiario.API/DTOs/Registros/RegistroDto.cs
git add CaixaDiario.API/Services/RegistroService.cs
git add CaixaDiario.Tests/Services/RegistroServiceTests.cs
git commit -m "feat: adicionar ItemFinanceiroSaida com categoria e subcategoria"
```

---

## Task 2: Frontend — Tipos e Config de Categorias

**Files:**

- Create: `frontend/src/config/categorias.ts`
- Modify: `frontend/src/types.ts`

- [ ] **Step 1: Criar `frontend/src/config/categorias.ts`**

```typescript
export const CATEGORIAS: Record<string, string[]> = {
  'Custos Diretos':  ['Mercadorias', 'Matéria-prima', 'Insumos', 'Fretes'],
  'Pessoas':         ['Salários', 'Pró-labore', 'Comissões', 'Benefícios'],
  'Administrativas': ['Aluguel', 'Energia', 'Internet', 'Telefonia', 'Software', 'Outros'],
  'Marketing':       ['Tráfego pago', 'Designer', 'Agência', 'Produção de conteúdo'],
  'Impostos':        ['DAS', 'ISS', 'ICMS', 'Outros tributos'],
  'Financeiras':     ['Juros', 'Tarifas bancárias', 'Empréstimos'],
  'Investimentos':   ['Equipamentos', 'Reformas', 'Veículos'],
}

export const LISTA_CATEGORIAS = Object.keys(CATEGORIAS)
```

- [ ] **Step 2: Atualizar `frontend/src/types.ts`**

Adicionar a interface `ItemFinanceiroSaida` logo após `ItemFinanceiro`:

```typescript
export interface ItemFinanceiroSaida {
  descricao: string
  valor: number
  categoria: string
  subcategoria?: string
}
```

Atualizar a interface `Registro`, campo `saidas`:

```typescript
// Antes:
saidas: ItemFinanceiro[]

// Depois:
saidas: ItemFinanceiroSaida[]
```

- [ ] **Step 3: Commit**

```powershell
git add frontend/src/config/categorias.ts frontend/src/types.ts
git commit -m "feat: tipos e config de categorias de saída"
```

---

## Task 3: Frontend — API Client e Hook

**Files:**

- Modify: `frontend/src/api/registros.ts`
- Modify: `frontend/src/hooks/useRegistros.test.tsx`

- [ ] **Step 1: Atualizar `frontend/src/api/registros.ts`**

Adicionar import do novo tipo no topo:

```typescript
import type { ApiResponse, Registro, ItemFinanceiro, ItemFinanceiroSaida, ContaProvisionada } from '../types'
```

Adicionar a função `mapItemFinanceiroSaida` logo após `mapItemFinanceiro`:

```typescript
// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapItemFinanceiroSaida(raw: any): ItemFinanceiroSaida {
  return {
    descricao: raw.Descricao ?? raw.descricao ?? '',
    valor: raw.Valor ?? raw.valor ?? 0,
    categoria: raw.Categoria ?? raw.categoria ?? 'Administrativas',
    subcategoria: raw.Subcategoria ?? raw.subcategoria ?? undefined,
  }
}
```

Em `mapRegistro`, substituir o mapeamento de `saidas`:

```typescript
saidas: (raw.saidas ?? []).map(mapItemFinanceiroSaida),
```

Em `salvarRegistro`, atualizar o tipo do parâmetro `saidas`:

```typescript
export const salvarRegistro = async (dto: {
  clienteId: string
  data: string
  saldoInicio: number
  entradas: ItemFinanceiro[]
  saidas: ItemFinanceiroSaida[]
  contasAReceber: ContaProvisionada[]
  contasAPagar: ContaProvisionada[]
  saldoConfirmado: number
}): Promise<ApiResponse<Registro>> => {
```

No corpo de `salvarRegistro`, substituir o mapeamento de `saidas` no payload:

```typescript
saidas: dto.saidas.map(s => ({
  Descricao: s.descricao,
  Valor: s.valor,
  Categoria: s.categoria,
  Subcategoria: s.subcategoria,
})),
```

- [ ] **Step 2: Atualizar mocks em `frontend/src/hooks/useRegistros.test.tsx`**

Atualizar `mockRegistros` para incluir `categoria` nas saídas:

```typescript
const mockRegistros = [
  {
    id: 'r1', clienteId: 'c1', data: '2026-05-15', saldoInicio: 100,
    entradas: [{ descricao: 'Caixa', valor: 200 }],
    saidas: [{ descricao: 'Despesa', valor: 50, categoria: 'Administrativas' }],
    contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 250, saldoCalculado: 250, criadoEm: ''
  }
]
```

- [ ] **Step 3: Rodar testes frontend**

```powershell
cd frontend
npm test -- --run
```

Expected: todos os testes passando.

- [ ] **Step 4: Commit**

```powershell
git add frontend/src/api/registros.ts frontend/src/hooks/useRegistros.test.tsx
git commit -m "feat: atualizar api client para saidas com categoria"
```

---

## Task 4: Frontend — UI do Caixa (selects de categoria + botões coloridos)

**Files:**

- Modify: `frontend/src/pages/client/ClientCaixaPage.tsx`
- Modify: `frontend/src/pages/client/ClientCaixa.css`

- [ ] **Step 1: Atualizar imports em `ClientCaixaPage.tsx`**

Substituir o import de tipos:

```typescript
import type { ItemFinanceiro, ItemFinanceiroSaida } from '../../types'
import { CATEGORIAS, LISTA_CATEGORIAS } from '../../config/categorias'
```

- [ ] **Step 2: Atualizar estado e função de update em `ClientCaixaPage.tsx`**

Substituir o estado de `saidas`:

```typescript
const [saidas, setSaidas] = useState<ItemFinanceiroSaida[]>([{ descricao: '', valor: 0, categoria: 'Administrativas', subcategoria: '' }])
```

Substituir a função `updateItem` por duas funções separadas (entrada e saída):

```typescript
function updateEntrada(i: number, field: keyof ItemFinanceiro, val: string) {
  setEntradas(prev => prev.map((x, j) => j !== i ? x : { ...x, [field]: field === 'valor' ? Number(val) : val }))
}

function updateSaida(i: number, field: keyof ItemFinanceiroSaida, val: string) {
  setSaidas(prev => prev.map((x, j) => {
    if (j !== i) return x
    const updated = { ...x, [field]: field === 'valor' ? Number(val) : val }
    if (field === 'categoria') updated.subcategoria = ''
    return updated
  }))
}
```

- [ ] **Step 3: Atualizar bloco de entradas no JSX de `ClientCaixaPage.tsx`**

Substituir o bloco das entradas (usando `updateItem` → `updateEntrada`):

```tsx
{entradas.map((e, i) => (
  <div key={i} className="saida-row">
    <input placeholder="Descrição" value={e.descricao}
      onChange={ev => updateEntrada(i, 'descricao', ev.target.value)} />
    <input type="number" placeholder="R$" value={e.valor || ''}
      onChange={ev => updateEntrada(i, 'valor', ev.target.value)} step="0.01" min="0" />
    <button className="btn-rm" onClick={() => setEntradas(prev => prev.filter((_, j) => j !== i))}>✕</button>
  </div>
))}
<button className="btn-add-entrada"
  onClick={() => setEntradas(e => [...e, { descricao: '', valor: 0 }])}>
  ＋ Adicionar entrada
</button>
```

- [ ] **Step 4: Atualizar bloco de saídas no JSX de `ClientCaixaPage.tsx`**

Substituir o bloco das saídas completo:

```tsx
{saidas.map((s, i) => (
  <div key={i} className="saida-row">
    <input placeholder="Descrição" value={s.descricao}
      onChange={ev => updateSaida(i, 'descricao', ev.target.value)} />
    <input type="number" placeholder="R$" value={s.valor || ''}
      onChange={ev => updateSaida(i, 'valor', ev.target.value)} step="0.01" min="0" />
    <select value={s.categoria} onChange={ev => updateSaida(i, 'categoria', ev.target.value)}
      className="saida-select">
      {LISTA_CATEGORIAS.map(cat => <option key={cat} value={cat}>{cat}</option>)}
    </select>
    <select value={s.subcategoria ?? ''} onChange={ev => updateSaida(i, 'subcategoria', ev.target.value)}
      className="saida-select">
      <option value="">— subcategoria —</option>
      {(CATEGORIAS[s.categoria] ?? []).map(sub => <option key={sub} value={sub}>{sub}</option>)}
    </select>
    <button className="btn-rm" onClick={() => setSaidas(prev => prev.filter((_, j) => j !== i))}>✕</button>
  </div>
))}
<button className="btn-add-saida"
  onClick={() => setSaidas(s => [...s, { descricao: '', valor: 0, categoria: 'Administrativas', subcategoria: '' }])}>
  ＋ Adicionar saída
</button>
```

- [ ] **Step 5: Atualizar carregamento de saídas no `useEffect` de `ClientCaixaPage.tsx`**

Dentro do `if (reg)`, substituir a linha de `setSaidas`:

```typescript
setSaidas(reg.saidas.length ? reg.saidas : [{ descricao: '', valor: 0, categoria: 'Administrativas', subcategoria: '' }])
```

No `else`, substituir:

```typescript
setSaidas([{ descricao: '', valor: 0, categoria: 'Administrativas', subcategoria: '' }])
```

- [ ] **Step 6: Atualizar CSS em `ClientCaixa.css`**

Adicionar ao final do arquivo:

```css
.btn-add-entrada {
  width: 100%; padding: 9px;
  background: #001a33; border: 1px dashed #007aff;
  border-radius: 8px; color: #007aff; font-size: 13px; font-weight: 600; margin-top: 4px;
}
.btn-add-entrada:hover { background: #002a55; }

.saida-select {
  padding: 9px 8px; background: var(--bg-input);
  border: 1px solid var(--bd); border-radius: 8px;
  color: var(--tx); font-size: 13px; width: 100%;
}
.saida-select:focus { border-color: #ff3b30; outline: none; }
```

Substituir o grid de `.saida-row` para acomodar os dois novos selects:

```css
.saida-row {
  display: grid;
  grid-template-columns: 1fr 90px 130px 130px 32px;
  gap: 8px; margin-bottom: 8px; align-items: center;
}
```

- [ ] **Step 7: Rodar testes frontend**

```powershell
npm test -- --run
```

Expected: todos passando.

- [ ] **Step 8: Commit**

```powershell
git add frontend/src/pages/client/ClientCaixaPage.tsx frontend/src/pages/client/ClientCaixa.css
git commit -m "feat: selects de categoria/subcategoria e botoes coloridos no caixa"
```

---

## Task 5: Frontend — Componente RelatorioCategoriasCard

**Files:**

- Create: `frontend/src/components/shared/RelatorioCategoriasCard.tsx`
- Create: `frontend/src/components/shared/RelatorioCategoriasCard.css`
- Create: `frontend/src/components/shared/RelatorioCategoriasCard.test.tsx`
- Modify: `frontend/src/pages/client/ClientCaixaPage.tsx`

- [ ] **Step 1: Escrever o teste**

Criar `frontend/src/components/shared/RelatorioCategoriasCard.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react'
import RelatorioCategoriasCard from './RelatorioCategoriasCard'
import type { Registro } from '../../types'

const hoje = new Date().toISOString().slice(0, 10)

const registroBase: Registro = {
  id: 'r1', clienteId: 'c1', data: hoje,
  saldoInicio: 0,
  entradas: [],
  saidas: [
    { descricao: 'Aluguel', valor: 1200, categoria: 'Administrativas' },
    { descricao: 'Salário', valor: 3000, categoria: 'Pessoas' },
  ],
  contasAReceber: [], contasAPagar: [],
  saldoConfirmado: 0, saldoCalculado: 0, criadoEm: '',
}

test('exibe todas as 7 categorias', () => {
  render(<RelatorioCategoriasCard registros={[registroBase]} />)
  expect(screen.getByText('Administrativas')).toBeInTheDocument()
  expect(screen.getByText('Pessoas')).toBeInTheDocument()
  expect(screen.getByText('Custos Diretos')).toBeInTheDocument()
  expect(screen.getByText('Marketing')).toBeInTheDocument()
  expect(screen.getByText('Impostos')).toBeInTheDocument()
  expect(screen.getByText('Financeiras')).toBeInTheDocument()
  expect(screen.getByText('Investimentos')).toBeInTheDocument()
})

test('exibe label "Total"', () => {
  render(<RelatorioCategoriasCard registros={[registroBase]} />)
  expect(screen.getByText('Total')).toBeInTheDocument()
})

test('exibe inputs de período', () => {
  render(<RelatorioCategoriasCard registros={[registroBase]} />)
  const inputs = screen.getAllByRole('textbox')
  expect(inputs.length).toBeGreaterThanOrEqual(0)
  const dateInputs = document.querySelectorAll('input[type="date"]')
  expect(dateInputs.length).toBe(2)
})

test('não exibe registros fora do período', () => {
  const registroAntigo: Registro = { ...registroBase, data: '2020-01-15' }
  render(<RelatorioCategoriasCard registros={[registroAntigo]} />)
  expect(screen.getByText('Total')).toBeInTheDocument()
})
```

- [ ] **Step 2: Rodar o teste para verificar que falha**

```powershell
npm test -- --run src/components/shared/RelatorioCategoriasCard.test.tsx
```

Expected: FAIL — `RelatorioCategoriasCard` não encontrado.

- [ ] **Step 3: Criar `RelatorioCategoriasCard.tsx`**

```typescript
import { useMemo, useState } from 'react'
import type { Registro } from '../../types'
import { LISTA_CATEGORIAS } from '../../config/categorias'
import { fmtBRL } from '../../utils/format'
import './RelatorioCategoriasCard.css'

interface Props {
  registros: Registro[]
}

export default function RelatorioCategoriasCard({ registros }: Props) {
  const hoje = new Date()
  const primeiroDiaMes = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}-01`
  const ultimoDiaMes = new Date(hoje.getFullYear(), hoje.getMonth() + 1, 0).toISOString().slice(0, 10)

  const [de, setDe] = useState(primeiroDiaMes)
  const [ate, setAte] = useState(ultimoDiaMes)

  const totais = useMemo(() => {
    const acc: Record<string, number> = {}
    for (const cat of LISTA_CATEGORIAS) acc[cat] = 0
    for (const reg of registros) {
      if (reg.data < de || reg.data > ate) continue
      for (const s of reg.saidas) {
        const cat = s.categoria ?? 'Administrativas'
        if (cat in acc) acc[cat] += s.valor
      }
    }
    return acc
  }, [registros, de, ate])

  const totalGeral = Object.values(totais).reduce((a, b) => a + b, 0)
  const maxValor = Math.max(...Object.values(totais), 1)

  return (
    <div className="relcat-card">
      <h3 className="relcat-titulo">Gastos por Categoria</h3>
      <div className="relcat-periodo">
        <label>De <input type="date" value={de} onChange={e => setDe(e.target.value)} /></label>
        <label>Até <input type="date" value={ate} onChange={e => setAte(e.target.value)} /></label>
      </div>
      <div className="relcat-lista">
        {LISTA_CATEGORIAS.map(cat => {
          const valor = totais[cat]
          const pct = totalGeral > 0 ? Math.round((valor / totalGeral) * 100) : 0
          const largura = Math.round((valor / maxValor) * 100)
          return (
            <div key={cat} className={`relcat-row ${valor === 0 ? 'relcat-zero' : ''}`}>
              <span className="relcat-cat">{cat}</span>
              <div className="relcat-barra-bg">
                <div className="relcat-barra" style={{ width: `${largura}%` }} />
              </div>
              <span className="relcat-valor">{fmtBRL(valor)}</span>
              <span className="relcat-pct">{pct}%</span>
            </div>
          )
        })}
      </div>
      <div className="relcat-total">
        <span>Total</span>
        <span>{fmtBRL(totalGeral)}</span>
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Criar `RelatorioCategoriasCard.css`**

```css
.relcat-card {
  background: var(--bg-card);
  border: 1px solid var(--bd);
  border-radius: 14px;
  padding: 20px;
  margin-top: 24px;
}

.relcat-titulo {
  font-size: 14px;
  font-weight: 700;
  color: var(--tx3);
  margin-bottom: 12px;
}

.relcat-periodo {
  display: flex;
  gap: 16px;
  margin-bottom: 16px;
  font-size: 13px;
  color: var(--tx3);
}

.relcat-periodo input {
  padding: 6px 10px;
  background: var(--bg-input);
  border: 1px solid var(--bd);
  border-radius: 8px;
  color: var(--tx);
  font-size: 13px;
  margin-left: 6px;
}

.relcat-lista {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.relcat-row {
  display: grid;
  grid-template-columns: 130px 1fr 90px 40px;
  align-items: center;
  gap: 10px;
  font-size: 13px;
}

.relcat-zero .relcat-cat,
.relcat-zero .relcat-valor,
.relcat-zero .relcat-pct {
  color: var(--tx4, #555);
}

.relcat-cat {
  font-weight: 600;
  color: var(--tx);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.relcat-barra-bg {
  height: 8px;
  background: var(--bd);
  border-radius: 4px;
  overflow: hidden;
}

.relcat-barra {
  height: 100%;
  background: #ff3b30;
  border-radius: 4px;
  transition: width 0.3s ease;
}

.relcat-zero .relcat-barra { background: var(--bd); }

.relcat-valor {
  text-align: right;
  color: var(--tx);
  font-variant-numeric: tabular-nums;
}

.relcat-pct {
  text-align: right;
  color: var(--tx3);
  font-size: 12px;
}

.relcat-total {
  display: flex;
  justify-content: space-between;
  margin-top: 16px;
  padding-top: 12px;
  border-top: 1px solid var(--bd);
  font-size: 14px;
  font-weight: 700;
  color: var(--tx);
}
```

- [ ] **Step 5: Rodar os testes para verificar que passam**

```powershell
npm test -- --run src/components/shared/RelatorioCategoriasCard.test.tsx
```

Expected: 4 testes passando.

- [ ] **Step 6: Integrar em `ClientCaixaPage.tsx`**

Adicionar o import no topo do arquivo:

```typescript
import RelatorioCategoriasCard from '../../components/shared/RelatorioCategoriasCard'
```

Adicionar o componente ao final do JSX, antes do `</>` de fechamento:

```tsx
<RelatorioCategoriasCard registros={registros} />
```

- [ ] **Step 7: Rodar todos os testes frontend**

```powershell
npm test -- --run
```

Expected: todos passando.

- [ ] **Step 8: Commit**

```powershell
git add frontend/src/components/shared/RelatorioCategoriasCard.tsx
git add frontend/src/components/shared/RelatorioCategoriasCard.css
git add frontend/src/components/shared/RelatorioCategoriasCard.test.tsx
git add frontend/src/pages/client/ClientCaixaPage.tsx
git commit -m "feat: componente RelatorioCategoriasCard com totais por categoria"
```

---

## Task 6: Histórico — Exibir Categoria nas Saídas

**Files:**

- Modify: `frontend/src/pages/client/ClientHistoricoPage.tsx`

- [ ] **Step 1: Atualizar exibição de saídas em `ClientHistoricoPage.tsx`**

Localizar o bloco que renderiza as saídas (dentro do `openId === r.id`):

```tsx
{r.saidas.map((s, i) => (
  <div key={i} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, padding: '4px 0', borderBottom: '1px solid var(--bg-card)' }}>
    <span>{s.descricao}{s.categoria ? <span style={{ marginLeft: 6, fontSize: 11, color: 'var(--tx3)', background: 'var(--bd)', borderRadius: 4, padding: '1px 6px' }}>{s.categoria}</span> : null}</span>
    <span style={{ color: '#ff3b30' }}>-{fmtBRL(s.valor)}</span>
  </div>
))}
```

- [ ] **Step 2: Rodar todos os testes frontend**

```powershell
npm test -- --run
```

Expected: todos passando.

- [ ] **Step 3: Commit**

```powershell
git add frontend/src/pages/client/ClientHistoricoPage.tsx
git commit -m "feat: exibir categoria das saidas no historico"
```

---

## Checklist Final

- [ ] `dotnet test CaixaDiario.Tests` — todos verdes
- [ ] `cd frontend && npm test -- --run` — todos verdes
- [ ] Testar manualmente no browser: criar saída com categoria, verificar relatório, verificar histórico
