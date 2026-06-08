# Design: Categorias no Caixa Diário

**Data:** 2026-06-02
**Sub-projeto:** 1 de 4 (Melhorias Sistema Financeiro)
**Pré-requisito para:** Dashboard Executivo Avançado (Sub-projeto 2)

---

## Contexto

O sistema atual registra saídas como `ItemFinanceiro` com apenas `descricao` e `valor`. Sem categorias, é impossível calcular indicadores gerenciais como EBITDA, Prime Cost e Composição de Despesas. Este sub-projeto introduz categorias nas saídas do Caixa Diário, com relatório de gastos por período.

---

## Escopo

- Novo model `ItemFinanceiroSaida` com campos `Categoria` (obrigatório) e `Subcategoria` (opcional)
- Lista fixa de 7 categorias com subcategorias (estruturada para customização futura)
- Botões coloridos no Caixa: azul para entrada, vermelho para saída
- Relatório de gastos por categoria com período customizável (default: mês atual)
- Fallback automático para dados históricos sem categoria: `"Administrativas"`

Fora de escopo: categorias em entradas, categorias customizáveis pelo usuário, validação de subcategoria obrigatória (futuro).

---

## Seção 1: Modelo de Dados & Backend

### Novo model

`CaixaDiario.API/Models/ItemFinanceiroSaida.cs`

```csharp
public class ItemFinanceiroSaida
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Categoria { get; set; } = "Administrativas";
    public string? Subcategoria { get; set; }
}
```

O valor default `"Administrativas"` garante o fallback automático na deserialização de registros antigos — nenhum script SQL necessário. `Subcategoria` permanece `null` para esses registros (campo opcional). A combinação `"Administrativas"` / sem subcategoria é o equivalente ao "Outros/Administrativas" solicitado.

### Mudanças em `RegistroDiario`

```csharp
// Antes
public List<ItemFinanceiro> Saidas { get; set; } = new();

// Depois
public List<ItemFinanceiroSaida> Saidas { get; set; } = new();
```

`Entradas` permanece `List<ItemFinanceiro>` sem alteração.

### Mudanças em `AppDbContext`

A conversão da coluna `saidas` (jsonb) atualiza os tipos:

```csharp
entity.Property(e => e.Saidas).HasColumnName("saidas").HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, _jsonOptions),
        v => JsonSerializer.Deserialize<List<ItemFinanceiroSaida>>(v, _jsonOptions) ?? new());
```

Nenhuma migration de schema é necessária — a coluna permanece `jsonb`.

### Novo DTO

`CaixaDiario.API/DTOs/Registros/ItemFinanceiroSaidaDto.cs`

```csharp
public class ItemFinanceiroSaidaDto
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Categoria { get; set; } = "Administrativas";
    public string? Subcategoria { get; set; }
}
```

### DTOs impactados

| Arquivo | Mudança |
| --- | --- |
| `DTOs/Registros/CriarRegistroDto.cs` | `Saidas`: `List<ItemFinanceiroDto>` → `List<ItemFinanceiroSaidaDto>` |
| `DTOs/Registros/RegistroDto.cs` | Idem |
| `Services/RegistroService.cs` | `MapToDto` e `SalvarAsync` atualizam mapeamento de saídas |

---

## Seção 2: Lista Fixa de Categorias

### `frontend/src/config/categorias.ts` (novo arquivo)

```typescript
export const CATEGORIAS: Record<string, string[]> = {
  "Custos Diretos":   ["Mercadorias", "Matéria-prima", "Insumos", "Fretes"],
  "Pessoas":          ["Salários", "Pró-labore", "Comissões", "Benefícios"],
  "Administrativas":  ["Aluguel", "Energia", "Internet", "Telefonia", "Software", "Outros"],
  "Marketing":        ["Tráfego pago", "Designer", "Agência", "Produção de conteúdo"],
  "Impostos":         ["DAS", "ISS", "ICMS", "Outros tributos"],
  "Financeiras":      ["Juros", "Tarifas bancárias", "Empréstimos"],
  "Investimentos":    ["Equipamentos", "Reformas", "Veículos"],
}

export const LISTA_CATEGORIAS = Object.keys(CATEGORIAS)
```

Este arquivo é a fonte única da verdade para categorias no frontend. Quando categorias tornarem-se customizáveis (futuro), este arquivo é substituído por uma chamada de API — o restante do código não muda.

---

## Seção 3: Frontend — Caixa Diário

### Mudanças em `types.ts`

```typescript
export interface ItemFinanceiroSaida {
  descricao: string
  valor: number
  categoria: string
  subcategoria?: string
}
```

`ItemFinanceiro` permanece sem alteração (entradas).

### Mudanças em `ClientCaixaPage.tsx`

**Estado inicial das saídas:**

```typescript
const [saidas, setSaidas] = useState<ItemFinanceiroSaida[]>([
  { descricao: '', valor: 0, categoria: 'Administrativas', subcategoria: '' }
])
```

**Layout de cada linha de saída:**

```text
[ Descrição ] [ R$ ] [ Categoria ▾ ] [ Subcategoria ▾ ] [ ✕ ]
```

- Select de categoria: lista `LISTA_CATEGORIAS` (obrigatório)
- Select de subcategoria: lista `CATEGORIAS[categoriaSelecionada]` + opção vazia no topo (opcional)
- Ao trocar categoria, subcategoria reseta para `''`

**Botões coloridos:**

- "＋ Adicionar entrada" → `btn-add-entrada` (azul, `#007aff`)
- "＋ Adicionar saída" → `btn-add-saida` (vermelho, `#ff3b30`)

### Arquivos impactados

| Arquivo | Mudança |
| --- | --- |
| `src/types.ts` | Novo tipo `ItemFinanceiroSaida` |
| `src/config/categorias.ts` | Novo arquivo |
| `src/api/registros.ts` | Tipagem de `saidas` → `ItemFinanceiroSaida[]` |
| `src/hooks/useRegistros.ts` | Idem |
| `src/pages/client/ClientCaixaPage.tsx` | Estado, UI, botões coloridos |
| `src/pages/client/ClientCaixa.css` | Estilos dos botões coloridos |
| `src/pages/client/ClientHistoricoPage.tsx` | Exibir categoria no detalhe do registro |

Nota: `AdminCaixaPage.tsx` é um wrapper puro de `ClientCaixaPage` — não requer alteração.

---

## Seção 4: Relatório por Categoria

### Componente

`src/components/shared/RelatorioCategoriasCard.tsx` (novo arquivo)

**Props:**

```typescript
interface Props {
  registros: Registro[]
}
```

O componente gerencia internamente o estado de período (de/até), com default no mês atual.

**Layout:**

```text
┌─ Gastos por Categoria ──────────────────────────┐
│ De [01/06/2026] Até [30/06/2026]                │
│                                                  │
│ Pessoas          ████████████░░░  R$ 8.500  42% │
│ Custos Diretos   ██████░░░░░░░░░  R$ 4.200  21% │
│ Administrativas  █████░░░░░░░░░░  R$ 3.800  19% │
│ Marketing        ███░░░░░░░░░░░░  R$ 2.300  11% │
│ Impostos         ██░░░░░░░░░░░░░  R$ 1.100   5% │
│ Financeiras      █░░░░░░░░░░░░░░  R$   400   2% │
│ Investimentos    ░░░░░░░░░░░░░░░  R$     0   0% │
│                                                  │
│ Total                             R$ 20.300      │
└──────────────────────────────────────────────────┘
```

- Barra de progresso CSS pura (sem biblioteca extra)
- Largura proporcional ao maior valor do período
- Categorias com valor zero exibidas em cinza
- Cálculo feito no frontend com `useMemo` sobre `registros` filtrados por período
- Sem novo endpoint de API

### Uso em `ClientCaixaPage.tsx`

```tsx
<RelatorioCategoriasCard registros={registros} />
```

Renderizado abaixo do formulário de registro do dia.

---

## Arquivos criados

| Arquivo | Tipo |
| --- | --- |
| `CaixaDiario.API/Models/ItemFinanceiroSaida.cs` | Novo |
| `CaixaDiario.API/DTOs/Registros/ItemFinanceiroSaidaDto.cs` | Novo |
| `frontend/src/config/categorias.ts` | Novo |
| `frontend/src/components/shared/RelatorioCategoriasCard.tsx` | Novo |
| `frontend/src/components/shared/RelatorioCategoriasCard.test.tsx` | Novo |

## Arquivos modificados

| Arquivo | Mudança resumida |
| --- | --- |
| `CaixaDiario.API/Models/RegistroDiario.cs` | `Saidas` muda para `List<ItemFinanceiroSaida>` |
| `CaixaDiario.API/Data/AppDbContext.cs` | Conversão jsonb de `Saidas` |
| `CaixaDiario.API/DTOs/Registros/CriarRegistroDto.cs` | `Saidas` usa novo DTO |
| `CaixaDiario.API/DTOs/Registros/RegistroDto.cs` | Idem |
| `CaixaDiario.API/Services/RegistroService.cs` | Mapeamento de saídas |
| `frontend/src/types.ts` | Novo tipo `ItemFinanceiroSaida` |
| `frontend/src/api/registros.ts` | Tipagem saídas |
| `frontend/src/hooks/useRegistros.ts` | Idem |
| `frontend/src/pages/client/ClientCaixaPage.tsx` | UI principal |
| `frontend/src/pages/client/ClientCaixa.css` | Botões coloridos |
| `frontend/src/pages/client/ClientHistoricoPage.tsx` | Exibir categoria |

---

## Testes impactados

- `CaixaDiario.Tests/Services/RegistroServiceTests.cs` — atualizar fixtures de saídas
- `CaixaDiario.Tests/Controllers/RegistrosControllerTests.cs` — idem
- `frontend/src/hooks/useRegistros.test.tsx` — atualizar mocks
- `frontend/src/pages/client/ClientCaixaPage.test.tsx` — novo comportamento de categoria
