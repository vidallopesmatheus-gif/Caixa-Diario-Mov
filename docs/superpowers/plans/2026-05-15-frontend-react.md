# Frontend React Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrar o frontend de HTML puro para React + Vite, mantendo todas as funcionalidades existentes e servindo o build pelo mesmo backend ASP.NET Core.

**Architecture:** O projeto React vive em `frontend/` na raiz do repositório. O build gera arquivos em `frontend/dist/` que são copiados para `CaixaDiario.API/wwwroot/`, onde o backend os serve como arquivos estáticos. Todo o desenvolvimento ocorre na branch `feature/frontend-react` — nenhum commit vai para `main` até o PR ser aprovado.

**Tech Stack:** React 18, TypeScript, Vite, React Router v6, Chart.js (recharts), Vitest + React Testing Library

---

## Regra de ouro da branch

**NUNCA commitar na `main` diretamente.** Todo trabalho é na branch `feature/frontend-react`. Ao final, abre-se um PR para revisão antes do merge.

---

## Estrutura de arquivos

```
frontend/
  package.json
  vite.config.ts
  tsconfig.json
  index.html
  src/
    main.tsx
    App.tsx
    api/
      client.ts          # fetch wrapper com JWT automático
      auth.ts            # POST /api/auth/login
      usuarios.ts        # CRUD /api/usuarios
      registros.ts       # CRUD /api/registros
    contexts/
      AuthContext.tsx    # token JWT, dados do usuário, login/logout
    hooks/
      useUsuarios.ts
      useRegistros.ts
    pages/
      LoginPage.tsx
      admin/
        AdminOverviewPage.tsx
        AdminClientsPage.tsx
        AdminCaixaPage.tsx
      client/
        ClientCaixaPage.tsx
        ClientHistoricoPage.tsx
        ClientGraficoPage.tsx
    components/
      Layout/
        TopBar.tsx
        TabsBar.tsx
      shared/
        StatCard.tsx
        Modal.tsx
        DayNav.tsx
        SaidaRow.tsx
        ProvRow.tsx
    styles/
      theme.css
      global.css
```

---

### Task 1: Criar branch e scaffold do projeto React

**Files:**
- Create: `frontend/package.json`
- Create: `frontend/vite.config.ts`
- Create: `frontend/tsconfig.json`
- Create: `frontend/index.html`
- Create: `frontend/src/main.tsx`
- Create: `frontend/src/App.tsx`

- [ ] **Step 1: Criar branch feature/frontend-react**

```bash
git checkout -b feature/frontend-react
```

Expected: `Switched to a new branch 'feature/frontend-react'`

- [ ] **Step 2: Criar projeto Vite + React + TypeScript**

```bash
cd frontend
npm create vite@latest . -- --template react-ts
npm install
```

Expected: Instala dependências sem erros.

- [ ] **Step 3: Instalar dependências adicionais**

```bash
npm install react-router-dom recharts
npm install -D vitest @vitest/ui @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom
```

- [ ] **Step 4: Configurar vite.config.ts**

```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/setupTests.ts',
  },
  build: {
    outDir: '../CaixaDiario.API/wwwroot',
    emptyOutDir: true,
  },
})
```

- [ ] **Step 5: Criar src/setupTests.ts**

```ts
import '@testing-library/jest-dom'
```

- [ ] **Step 6: Adicionar script de build ao package.json**

No `frontend/package.json`, garantir que os scripts sejam:
```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "test": "vitest",
    "preview": "vite preview"
  }
}
```

- [ ] **Step 7: Verificar que o dev server sobe**

```bash
npm run dev
```

Expected: `Local: http://localhost:5173/` sem erros.

- [ ] **Step 8: Commitar scaffold**

```bash
cd ..
git add frontend/
git commit -m "feat: scaffold React + Vite project in frontend/"
```

---

### Task 2: Tema CSS e estilos globais

**Files:**
- Create: `frontend/src/styles/theme.css`
- Create: `frontend/src/styles/global.css`
- Modify: `frontend/src/main.tsx`

- [ ] **Step 1: Criar frontend/src/styles/theme.css**

```css
:root {
  --bg: #0d0d0d;
  --bg-card: #1a1a1a;
  --bg-deep: #111;
  --bg-hover: #222;
  --bg-input: #111;
  --bg-btn: #2a2a2a;
  --bd: #2a2a2a;
  --bd-sub: #222;
  --bd-str: #333;
  --tx: #fff;
  --tx2: #888;
  --tx3: #666;
  --tx4: #555;
  --err-bg: #2e1010;
  --btn-cancel-bg: #2a2a2a;
  --btn-cancel-c: #aaa;
  --btn-lo-bg: #2a1a1a;
  --btn-lo-c: #ff6b6b;
  --btn-lo-bd: #5a2a2a;
  --badge-adm-bg: #2a1a3a;
  --badge-cli-bg: #0a2a1a;
  --day-btn: #2a2a2a;
  --rm-bg: #2a1010;
  --rm-bd: #5a2020;
  --rmp-bg: #1a2a3a;
  --rmp-bd: #2a4a5a;
  --tag-in: #0a2a1a;
  --tag-out: #2a0a0a;
  --tag-prec: #0a1a2a;
  --tag-ppag: #1a0a0a;
  --ov-stat: #111;
  --ov-hv: #1a1f2a;
  --vc-bg: #1a2a3a;
  --vc-bd: #2a4a5a;
}

body.light {
  --bg: #f5f5f7;
  --bg-card: #ffffff;
  --bg-deep: #ffffff;
  --bg-hover: #f0f0f2;
  --bg-input: #f0f0f2;
  --bg-btn: #e8e8eb;
  --bd: #e0e0e0;
  --bd-sub: #e8e8e8;
  --bd-str: #d0d0d0;
  --tx: #1a1a1a;
  --tx2: #555;
  --tx3: #777;
  --tx4: #999;
  --err-bg: #ffebee;
  --btn-cancel-bg: #ececec;
  --btn-cancel-c: #555;
  --btn-lo-bg: #fff0f0;
  --btn-lo-c: #e53935;
  --btn-lo-bd: #ffcdd2;
  --badge-adm-bg: #f3e8ff;
  --badge-cli-bg: #e8f5e9;
  --day-btn: #ececec;
  --rm-bg: #fff0f0;
  --rm-bd: #ffcdd2;
  --rmp-bg: #e3f2fd;
  --rmp-bd: #bbdefb;
  --tag-in: #e8f5e9;
  --tag-out: #ffebee;
  --tag-prec: #e3f2fd;
  --tag-ppag: #fff3e0;
  --ov-stat: #f5f5f7;
  --ov-hv: #e8f4fd;
  --vc-bg: #e3f2fd;
  --vc-bd: #bbdefb;
}
```

- [ ] **Step 2: Criar frontend/src/styles/global.css**

```css
* { margin: 0; padding: 0; box-sizing: border-box; }
body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
  background: var(--bg);
  color: var(--tx);
  min-height: 100vh;
}
input { font-family: inherit; }
button { font-family: inherit; cursor: pointer; }

.val-green { color: #34c759; }
.val-red   { color: #ff3b30; }
.val-blue  { color: #5ac8fa; }
.val-yellow{ color: #ffd60a; }

.tag { font-size: 11px; padding: 2px 8px; border-radius: 20px; font-weight: 600; }
.tag-in    { background: var(--tag-in);   color: #34c759; }
.tag-out   { background: var(--tag-out);  color: #ff3b30; }
.tag-prec  { background: var(--tag-prec); color: #5ac8fa; }
.tag-ppag  { background: var(--tag-ppag); color: #ff6b6b; }

.btn-primary {
  width: 100%; padding: 13px;
  background: linear-gradient(135deg, #5ac8fa, #34c759);
  color: #000; border: none; border-radius: 10px;
  font-size: 15px; font-weight: 700;
}
.btn-primary:hover { opacity: 0.9; }

.btn-sm { padding: 7px 14px; border-radius: 8px; font-size: 13px; font-weight: 600; border: none; }
.btn-cancel { padding: 10px 20px; background: var(--btn-cancel-bg); border: none; border-radius: 8px; color: var(--btn-cancel-c); font-size: 14px; }
.btn-confirm { padding: 10px 20px; background: #34c759; border: none; border-radius: 8px; color: #000; font-size: 14px; font-weight: 700; }
.btn-danger  { padding: 10px 20px; background: #ff3b30; border: none; border-radius: 8px; color: #fff; font-size: 14px; font-weight: 700; }

.modal-overlay {
  position: fixed; inset: 0; background: rgba(0,0,0,.7);
  display: none; align-items: center; justify-content: center;
  z-index: 100; overflow-y: auto;
}
.modal-overlay.open { display: flex; }
.modal {
  background: var(--bg-card); border: 1px solid var(--bd-str);
  border-radius: 16px; padding: 28px; width: 100%; max-width: 500px;
}
.modal-title { font-size: 17px; font-weight: 700; margin-bottom: 20px; }
.modal-footer { display: flex; gap: 10px; margin-top: 20px; justify-content: flex-end; }

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 12px; margin-bottom: 24px;
}
.stat-card { background: var(--bg-card); border: 1px solid var(--bd); border-radius: 14px; padding: 16px 20px; }
.stat-card .lbl { font-size: 12px; color: var(--tx3); margin-bottom: 6px; }
.stat-card .val { font-size: 22px; font-weight: 700; }

.panel { background: var(--bg-card); border: 1px solid var(--bd); border-radius: 14px; padding: 20px; }
.panel-title { font-size: 15px; font-weight: 700; margin-bottom: 16px; padding-bottom: 12px; border-bottom: 1px solid var(--bd); display: flex; align-items: center; justify-content: space-between; }

.inp-group { margin-bottom: 14px; }
.inp-group label { display: block; font-size: 13px; color: var(--tx2); margin-bottom: 5px; font-weight: 500; }
.inp-group input { width: 100%; padding: 10px 13px; background: var(--bg-input); border: 1px solid var(--bd); border-radius: 8px; color: var(--tx); font-size: 15px; }
.inp-group input:focus { outline: none; border-color: #5ac8fa; }

.form-card { background: var(--bg-card); border: 1px solid var(--bd); border-radius: 14px; padding: 20px; margin-bottom: 16px; }
.form-card h3 { font-size: 14px; font-weight: 600; color: var(--tx2); text-transform: uppercase; letter-spacing: .5px; margin-bottom: 16px; }

@media (max-width: 600px) {
  .stats-grid { grid-template-columns: 1fr 1fr; }
}
```

- [ ] **Step 3: Importar estilos em main.tsx**

```tsx
import React from 'react'
import ReactDOM from 'react-dom/client'
import './styles/theme.css'
import './styles/global.css'
import App from './App'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
)
```

- [ ] **Step 4: Commitar estilos**

```bash
git add frontend/src/styles/ frontend/src/main.tsx
git commit -m "feat: add CSS theme variables and global styles"
```

---

### Task 3: API client e tipos TypeScript

**Files:**
- Create: `frontend/src/api/client.ts`
- Create: `frontend/src/api/auth.ts`
- Create: `frontend/src/api/usuarios.ts`
- Create: `frontend/src/api/registros.ts`
- Create: `frontend/src/types.ts`

- [ ] **Step 1: Criar frontend/src/types.ts**

```ts
export interface Usuario {
  id: string
  nomeUsuario: string
  nomeCompleto: string
  nomeEstabelecimento: string
  perfil: 'admin' | 'cliente'
  ativo: boolean
  criadoEm: string
  criadoPor: string
}

export interface Saida {
  descricao: string
  valor: number
}

export interface ContaProvisionada {
  descricao: string
  valor: number
}

export interface Registro {
  id: string
  clienteId: string
  data: string
  saldoInicio: number
  entrada: number
  saidas: Saida[]
  contasAReceber: ContaProvisionada[]
  contasAPagar: ContaProvisionada[]
  saldoConfirmado: number
  saldoCalculado: number
  criadoEm: string
}

export interface LoginResponse {
  token: string
  perfil: 'admin' | 'cliente'
  nomeUsuario: string
  nomeCompleto: string
  nomeEstabelecimento: string
  usuarioId: string
}

export interface ApiResponse<T> {
  dados: T
  codigoRetorno: string
  mensagem: string
}
```

- [ ] **Step 2: Criar frontend/src/api/client.ts**

```ts
const BASE = import.meta.env.VITE_API_URL ?? ''

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const token = localStorage.getItem('token')
  const res = await fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  })
  if (!res.ok) {
    const err = await res.json().catch(() => ({}))
    throw new Error(err?.mensagem ?? `Erro ${res.status}`)
  }
  return res.json()
}
```

- [ ] **Step 3: Criar frontend/src/api/auth.ts**

```ts
import { apiFetch } from './client'
import type { ApiResponse, LoginResponse } from '../types'

export function login(nomeUsuario: string, senha: string) {
  return apiFetch<ApiResponse<LoginResponse>>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ nomeUsuario, senha }),
  })
}
```

- [ ] **Step 4: Criar frontend/src/api/usuarios.ts**

```ts
import { apiFetch } from './client'
import type { ApiResponse, Usuario } from '../types'

export const listarUsuarios = () =>
  apiFetch<ApiResponse<Usuario[]>>('/api/usuarios')

export const criarUsuario = (dto: {
  nomeUsuario: string
  senha: string
  nomeCompleto: string
  nomeEstabelecimento: string
  perfil: string
}) =>
  apiFetch<ApiResponse<Usuario>>('/api/usuarios', {
    method: 'POST',
    body: JSON.stringify(dto),
  })

export const atualizarUsuario = (
  id: string,
  dto: { nomeCompleto: string; nomeEstabelecimento: string; senha?: string }
) =>
  apiFetch<ApiResponse<Usuario>>(`/api/usuarios/${id}`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })

export const desativarUsuario = (id: string) =>
  apiFetch<ApiResponse<null>>(`/api/usuarios/${id}`, { method: 'DELETE' })
```

- [ ] **Step 5: Criar frontend/src/api/registros.ts**

```ts
import { apiFetch } from './client'
import type { ApiResponse, Registro, Saida, ContaProvisionada } from '../types'

export const listarRegistros = (clienteId: string) =>
  apiFetch<ApiResponse<Registro[]>>(`/api/registros/${clienteId}`)

export const obterRegistroPorData = (clienteId: string, data: string) =>
  apiFetch<ApiResponse<Registro>>(`/api/registros/${clienteId}/${data}`)

export const salvarRegistro = (dto: {
  clienteId: string
  data: string
  saldoInicio: number
  entrada: number
  saidas: Saida[]
  contasAReceber: ContaProvisionada[]
  contasAPagar: ContaProvisionada[]
  saldoConfirmado: number
}) =>
  apiFetch<ApiResponse<Registro>>('/api/registros', {
    method: 'POST',
    body: JSON.stringify(dto),
  })

export const excluirRegistro = (
  clienteId: string,
  data: string,
  motivoExclusao: string
) =>
  apiFetch<ApiResponse<null>>(`/api/registros/${clienteId}/${data}`, {
    method: 'DELETE',
    body: JSON.stringify({ motivoExclusao }),
  })
```

- [ ] **Step 6: Criar frontend/.env.development**

```
VITE_API_URL=http://localhost:5131
```

- [ ] **Step 7: Criar frontend/.env.production** (vazio — mesma origem)

```
VITE_API_URL=
```

- [ ] **Step 8: Commitar API layer**

```bash
git add frontend/src/api/ frontend/src/types.ts frontend/.env.development frontend/.env.production
git commit -m "feat: add typed API client and TypeScript types"
```

---

### Task 4: AuthContext e roteamento

**Files:**
- Create: `frontend/src/contexts/AuthContext.tsx`
- Create: `frontend/src/App.tsx`

- [ ] **Step 1: Escrever teste para AuthContext**

Criar `frontend/src/contexts/AuthContext.test.tsx`:
```tsx
import { render, screen, act } from '@testing-library/react'
import { AuthProvider, useAuth } from './AuthContext'

function Probe() {
  const { user, logout } = useAuth()
  return (
    <div>
      <span data-testid="user">{user?.nomeUsuario ?? 'none'}</span>
      <button onClick={logout}>sair</button>
    </div>
  )
}

test('começa sem usuário logado', () => {
  localStorage.clear()
  render(<AuthProvider><Probe /></AuthProvider>)
  expect(screen.getByTestId('user').textContent).toBe('none')
})

test('logout limpa o usuário', async () => {
  localStorage.setItem('token', 'tok')
  localStorage.setItem('user', JSON.stringify({
    nomeUsuario: 'CTI', perfil: 'admin',
    nomeCompleto: 'CTI', nomeEstabelecimento: '',
    usuarioId: '1', token: 'tok'
  }))
  render(<AuthProvider><Probe /></AuthProvider>)
  expect(screen.getByTestId('user').textContent).toBe('CTI')
  await act(async () => { screen.getByText('sair').click() })
  expect(screen.getByTestId('user').textContent).toBe('none')
})
```

- [ ] **Step 2: Rodar teste e confirmar falha**

```bash
cd frontend && npx vitest run src/contexts/AuthContext.test.tsx
```

Expected: FAIL — `AuthContext` não existe ainda.

- [ ] **Step 3: Implementar frontend/src/contexts/AuthContext.tsx**

```tsx
import { createContext, useContext, useState, useCallback, type ReactNode } from 'react'
import type { LoginResponse } from '../types'

interface AuthUser extends LoginResponse {}

interface AuthContextValue {
  user: AuthUser | null
  login: (data: LoginResponse) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    try {
      const stored = localStorage.getItem('user')
      return stored ? JSON.parse(stored) : null
    } catch {
      return null
    }
  })

  const login = useCallback((data: LoginResponse) => {
    localStorage.setItem('token', data.token)
    localStorage.setItem('user', JSON.stringify(data))
    setUser(data)
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    setUser(null)
  }, [])

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be inside AuthProvider')
  return ctx
}
```

- [ ] **Step 4: Rodar teste e confirmar que passa**

```bash
npx vitest run src/contexts/AuthContext.test.tsx
```

Expected: 2 passed.

- [ ] **Step 5: Criar frontend/src/App.tsx com rotas**

```tsx
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import LoginPage from './pages/LoginPage'
import AdminOverviewPage from './pages/admin/AdminOverviewPage'
import AdminClientsPage from './pages/admin/AdminClientsPage'
import AdminCaixaPage from './pages/admin/AdminCaixaPage'
import ClientCaixaPage from './pages/client/ClientCaixaPage'
import ClientHistoricoPage from './pages/client/ClientHistoricoPage'
import ClientGraficoPage from './pages/client/ClientGraficoPage'
import Layout from './components/Layout/Layout'

function ProtectedRoutes() {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />

  if (user.perfil === 'admin') {
    return (
      <Layout>
        <Routes>
          <Route path="/admin/overview" element={<AdminOverviewPage />} />
          <Route path="/admin/clientes" element={<AdminClientsPage />} />
          <Route path="/admin/caixa" element={<AdminCaixaPage />} />
          <Route path="*" element={<Navigate to="/admin/overview" replace />} />
        </Routes>
      </Layout>
    )
  }

  return (
    <Layout>
      <Routes>
        <Route path="/caixa" element={<ClientCaixaPage />} />
        <Route path="/historico" element={<ClientHistoricoPage />} />
        <Route path="/grafico" element={<ClientGraficoPage />} />
        <Route path="*" element={<Navigate to="/caixa" replace />} />
      </Routes>
    </Layout>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/*" element={<ProtectedRoutes />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
```

- [ ] **Step 6: Commitar contexto e roteamento**

```bash
git add frontend/src/contexts/ frontend/src/App.tsx
git commit -m "feat: add AuthContext and React Router setup"
```

---

### Task 5: Componentes de Layout (TopBar + TabsBar)

**Files:**
- Create: `frontend/src/components/Layout/Layout.tsx`
- Create: `frontend/src/components/Layout/TopBar.tsx`
- Create: `frontend/src/components/Layout/TabsBar.tsx`
- Create: `frontend/src/components/Layout/Layout.css`

- [ ] **Step 1: Criar frontend/src/components/Layout/Layout.css**

```css
.topbar {
  background: var(--bg-deep);
  border-bottom: 1px solid var(--bd-sub);
  padding: 12px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.topbar-left  { display: flex; align-items: center; gap: 16px; }
.topbar-logo  { font-size: 18px; font-weight: 700; }
.topbar-right { display: flex; align-items: center; gap: 10px; }

.badge-role   { font-size: 11px; padding: 3px 10px; border-radius: 20px; font-weight: 600; }
.badge-admin  { background: var(--badge-adm-bg); color: #bf5fff; border: 1px solid #6a2f9a; }
.badge-client { background: var(--badge-cli-bg); color: #34c759; border: 1px solid #1a6a3a; }

.avatar {
  width: 34px; height: 34px; border-radius: 50%;
  background: linear-gradient(135deg, #5ac8fa, #34c759);
  display: flex; align-items: center; justify-content: center;
  font-weight: 700; color: #000; font-size: 14px;
}
.btn-logout { background: var(--btn-lo-bg); color: var(--btn-lo-c); border: 1px solid var(--btn-lo-bd); }
.btn-logout:hover { background: #3a1a1a; }
.btn-theme {
  background: var(--bg-btn); color: var(--tx); border: 1px solid var(--bd);
  font-size: 16px; width: 34px; height: 34px;
  display: flex; align-items: center; justify-content: center; border-radius: 8px;
}
.btn-theme:hover { background: var(--bg-hover); }

.tabs-bar {
  background: var(--bg-deep);
  border-bottom: 1px solid var(--bd-sub);
  padding: 0 24px;
  display: flex; gap: 4px; overflow-x: auto;
}
.tab-btn {
  padding: 12px 20px; background: none; border: none;
  color: var(--tx3); font-size: 14px; font-weight: 600;
  cursor: pointer; border-bottom: 2px solid transparent; white-space: nowrap;
}
.tab-btn.active { color: var(--tx); border-bottom-color: #5ac8fa; }
.tab-btn:hover  { color: var(--btn-cancel-c); }

.tab-content { padding: 24px; }

@media (max-width: 600px) {
  .tabs-bar   { padding: 0 12px; }
  .tab-content { padding: 14px; }
  .topbar     { padding: 10px 14px; }
}
```

- [ ] **Step 2: Criar frontend/src/components/Layout/TopBar.tsx**

```tsx
import { useAuth } from '../../contexts/AuthContext'
import { useEffect, useState } from 'react'

export default function TopBar() {
  const { user, logout } = useAuth()
  const [light, setLight] = useState(() => localStorage.getItem('theme') === 'light')

  useEffect(() => {
    document.body.classList.toggle('light', light)
    localStorage.setItem('theme', light ? 'light' : 'dark')
  }, [light])

  if (!user) return null
  const initials = user.nomeCompleto.split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase()

  return (
    <div className="topbar">
      <div className="topbar-left">
        <span className="topbar-logo">💰 Caixa Diário</span>
        <span className={`badge-role ${user.perfil === 'admin' ? 'badge-admin' : 'badge-client'}`}>
          {user.perfil === 'admin' ? 'Admin' : 'Cliente'}
        </span>
      </div>
      <div className="topbar-right">
        <button className="btn-sm btn-theme" onClick={() => setLight(l => !l)}>
          {light ? '🌙' : '☀️'}
        </button>
        <div className="avatar">{initials}</div>
        <span style={{ fontSize: 14, fontWeight: 600 }}>{user.nomeCompleto}</span>
        <button className="btn-sm btn-logout" onClick={logout}>Sair</button>
      </div>
    </div>
  )
}
```

- [ ] **Step 3: Criar frontend/src/components/Layout/TabsBar.tsx**

```tsx
import { NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

export default function TabsBar() {
  const { user } = useAuth()

  if (!user) return null

  const adminTabs = [
    { to: '/admin/overview', label: '🏠 Visão Geral' },
    { to: '/admin/clientes', label: '👥 Clientes' },
    { to: '/admin/caixa',    label: '📋 Ver Caixa' },
  ]
  const clientTabs = [
    { to: '/caixa',     label: '📋 Caixa Diário' },
    { to: '/historico', label: '📈 Histórico' },
    { to: '/grafico',   label: '📊 Evolução' },
  ]
  const tabs = user.perfil === 'admin' ? adminTabs : clientTabs

  return (
    <div className="tabs-bar">
      {tabs.map(t => (
        <NavLink key={t.to} to={t.to}
          className={({ isActive }) => `tab-btn${isActive ? ' active' : ''}`}>
          {t.label}
        </NavLink>
      ))}
    </div>
  )
}
```

- [ ] **Step 4: Criar frontend/src/components/Layout/Layout.tsx**

```tsx
import { type ReactNode } from 'react'
import TopBar from './TopBar'
import TabsBar from './TabsBar'
import './Layout.css'

export default function Layout({ children }: { children: ReactNode }) {
  return (
    <>
      <TopBar />
      <TabsBar />
      <div className="tab-content">{children}</div>
    </>
  )
}
```

- [ ] **Step 5: Commitar Layout**

```bash
git add frontend/src/components/Layout/
git commit -m "feat: add Layout with TopBar, TabsBar and theme toggle"
```

---

### Task 6: Componentes compartilhados (StatCard, Modal, DayNav)

**Files:**
- Create: `frontend/src/components/shared/StatCard.tsx`
- Create: `frontend/src/components/shared/Modal.tsx`
- Create: `frontend/src/components/shared/DayNav.tsx`
- Create: `frontend/src/utils/format.ts`

- [ ] **Step 1: Criar frontend/src/utils/format.ts**

```ts
export function fmtBRL(value: number): string {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

export function fmtDate(iso: string): string {
  const [y, m, d] = iso.split('-')
  return `${d}/${m}/${y}`
}

export function todayISO(): string {
  return new Date().toISOString().slice(0, 10)
}

export function addDays(iso: string, days: number): string {
  const d = new Date(iso + 'T12:00:00')
  d.setDate(d.getDate() + days)
  return d.toISOString().slice(0, 10)
}

export function monthLabel(iso: string): string {
  const [y, m] = iso.split('-')
  const months = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']
  return `${months[parseInt(m) - 1]}/${y}`
}
```

- [ ] **Step 2: Criar frontend/src/components/shared/StatCard.tsx**

```tsx
interface StatCardProps {
  label: string
  value: string
  className?: string
}
export default function StatCard({ label, value, className = '' }: StatCardProps) {
  return (
    <div className="stat-card">
      <div className="lbl">{label}</div>
      <div className={`val ${className}`}>{value}</div>
    </div>
  )
}
```

- [ ] **Step 3: Criar frontend/src/components/shared/Modal.tsx**

```tsx
import { type ReactNode } from 'react'

interface ModalProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
}
export default function Modal({ open, title, onClose, children, footer }: ModalProps) {
  if (!open) return null
  return (
    <div className={`modal-overlay ${open ? 'open' : ''}`} onClick={e => { if (e.target === e.currentTarget) onClose() }}>
      <div className="modal">
        <div className="modal-title">{title}</div>
        {children}
        {footer && <div className="modal-footer">{footer}</div>}
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Criar frontend/src/components/shared/DayNav.tsx**

```tsx
import { fmtDate } from '../../utils/format'

interface DayNavProps {
  date: string
  onPrev: () => void
  onNext: () => void
}
export default function DayNav({ date, onPrev, onNext }: DayNavProps) {
  const [y, m, d] = date.split('-')
  const dayNames = ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb']
  const dayName = dayNames[new Date(`${date}T12:00:00`).getDay()]

  return (
    <div className="day-nav">
      <button onClick={onPrev}>←</button>
      <div>
        <div className="day-label">{fmtDate(date)}</div>
        <div className="day-sub">{dayName}</div>
      </div>
      <button onClick={onNext}>→</button>
    </div>
  )
}
```

Adicionar CSS para `.day-nav` em `global.css`:
```css
.day-nav {
  display: flex; align-items: center; justify-content: space-between;
  background: var(--bg-card); border: 1px solid var(--bd);
  border-radius: 14px; padding: 14px 20px; margin-bottom: 20px;
}
.day-nav button {
  background: var(--day-btn); border: none; color: var(--tx);
  width: 36px; height: 36px; border-radius: 8px; font-size: 16px;
}
.day-nav button:hover { background: var(--bd-str); }
.day-label { font-size: 17px; font-weight: 700; text-align: center; }
.day-sub   { font-size: 12px; color: var(--tx3); text-align: center; margin-top: 2px; }
```

- [ ] **Step 5: Testar format.ts**

Criar `frontend/src/utils/format.test.ts`:
```ts
import { fmtBRL, fmtDate, todayISO, addDays, monthLabel } from './format'

test('fmtBRL formata valor em reais', () => {
  expect(fmtBRL(1234.5)).toBe('R$ 1.234,50')
})

test('fmtDate converte ISO para dd/mm/yyyy', () => {
  expect(fmtDate('2026-05-15')).toBe('15/05/2026')
})

test('addDays avança dias corretamente', () => {
  expect(addDays('2026-05-15', 1)).toBe('2026-05-16')
  expect(addDays('2026-05-15', -1)).toBe('2026-05-14')
})

test('addDays atravessa virada de mês', () => {
  expect(addDays('2026-01-31', 1)).toBe('2026-02-01')
})

test('monthLabel retorna mês abreviado', () => {
  expect(monthLabel('2026-05-15')).toBe('Mai/2026')
})

test('todayISO retorna formato YYYY-MM-DD', () => {
  expect(todayISO()).toMatch(/^\d{4}-\d{2}-\d{2}$/)
})
```

Run: `cd frontend && npx vitest run src/utils/format.test.ts`
Expected: 6 passed.

- [ ] **Step 6: Testar componentes compartilhados**

Criar `frontend/src/components/shared/StatCard.test.tsx`:
```tsx
import { render, screen } from '@testing-library/react'
import StatCard from './StatCard'

test('renderiza label e valor', () => {
  render(<StatCard label="💰 Saldo" value="R$ 1.234,50" />)
  expect(screen.getByText('💰 Saldo')).toBeInTheDocument()
  expect(screen.getByText('R$ 1.234,50')).toBeInTheDocument()
})

test('aplica className extra no val', () => {
  render(<StatCard label="x" value="y" className="val-green" />)
  expect(screen.getByText('y').className).toContain('val-green')
})
```

Criar `frontend/src/components/shared/Modal.test.tsx`:
```tsx
import { render, screen, fireEvent } from '@testing-library/react'
import Modal from './Modal'

test('não renderiza quando open=false', () => {
  render(<Modal open={false} title="Teste" onClose={() => {}}>conteúdo</Modal>)
  expect(screen.queryByText('Teste')).not.toBeInTheDocument()
})

test('renderiza título e conteúdo quando open=true', () => {
  render(<Modal open={true} title="Meu Modal" onClose={() => {}}>corpo aqui</Modal>)
  expect(screen.getByText('Meu Modal')).toBeInTheDocument()
  expect(screen.getByText('corpo aqui')).toBeInTheDocument()
})

test('chama onClose ao clicar no overlay', () => {
  const onClose = vi.fn()
  const { container } = render(<Modal open={true} title="X" onClose={onClose}>y</Modal>)
  fireEvent.click(container.firstChild!)
  expect(onClose).toHaveBeenCalledTimes(1)
})
```

Criar `frontend/src/components/shared/DayNav.test.tsx`:
```tsx
import { render, screen, fireEvent } from '@testing-library/react'
import DayNav from './DayNav'

test('exibe data formatada', () => {
  render(<DayNav date="2026-05-15" onPrev={() => {}} onNext={() => {}} />)
  expect(screen.getByText('15/05/2026')).toBeInTheDocument()
})

test('chama onPrev ao clicar em ←', () => {
  const onPrev = vi.fn()
  render(<DayNav date="2026-05-15" onPrev={onPrev} onNext={() => {}} />)
  fireEvent.click(screen.getByText('←'))
  expect(onPrev).toHaveBeenCalledTimes(1)
})

test('chama onNext ao clicar em →', () => {
  const onNext = vi.fn()
  render(<DayNav date="2026-05-15" onPrev={() => {}} onNext={onNext} />)
  fireEvent.click(screen.getByText('→'))
  expect(onNext).toHaveBeenCalledTimes(1)
})
```

Run: `npx vitest run src/components/shared/`
Expected: 7 passed.

- [ ] **Step 7: Commitar componentes compartilhados com testes**

```bash
git add frontend/src/components/shared/ frontend/src/utils/
git commit -m "feat: add shared components, format utils and unit tests"
```

---

### Task 7: LoginPage

**Files:**
- Create: `frontend/src/pages/LoginPage.tsx`
- Create: `frontend/src/pages/LoginPage.css`

- [ ] **Step 1: Criar frontend/src/pages/LoginPage.css**

```css
.login-wrap {
  display: flex; align-items: center; justify-content: center;
  min-height: 100vh; padding: 20px;
}
.login-box {
  background: var(--bg-card); border: 1px solid var(--bd-str);
  border-radius: 20px; padding: 40px; width: 100%; max-width: 400px;
  box-shadow: 0 20px 60px rgba(0,0,0,.5);
}
.login-logo { text-align: center; margin-bottom: 32px; }
.login-logo h1 {
  font-size: 30px;
  background: linear-gradient(135deg, #5ac8fa, #34c759);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}
.login-logo p { color: var(--tx3); font-size: 13px; margin-top: 4px; }
.field { margin-bottom: 16px; }
.field label { display: block; font-size: 13px; color: var(--tx2); font-weight: 600; margin-bottom: 6px; }
.field input {
  width: 100%; padding: 11px 14px;
  background: var(--bg-btn); border: 1px solid var(--bd-str);
  border-radius: 10px; color: var(--tx); font-size: 15px;
}
.field input:focus { outline: none; border-color: #5ac8fa; }
.field input::placeholder { color: var(--tx4); }
.err-box {
  background: var(--err-bg); border: 1px solid #ff3b30;
  color: #ff6b6b; padding: 10px 14px; border-radius: 8px;
  font-size: 13px; margin-bottom: 16px;
}
```

- [ ] **Step 2: Criar frontend/src/pages/LoginPage.tsx**

```tsx
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { login as apiLogin } from '../api/auth'
import './LoginPage.css'

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [usuario, setUsuario] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleLogin(e: React.FormEvent) {
    e.preventDefault()
    setErro('')
    setLoading(true)
    try {
      const res = await apiLogin(usuario, senha)
      login(res.dados)
      navigate(res.dados.perfil === 'admin' ? '/admin/overview' : '/caixa')
    } catch (e: any) {
      setErro(e.message ?? 'Erro ao fazer login')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-wrap">
      <div className="login-box">
        <div className="login-logo">
          <h1>💰 Caixa Diário</h1>
          <p>Sistema de controle financeiro</p>
        </div>
        {erro && <div className="err-box">{erro}</div>}
        <form onSubmit={handleLogin}>
          <div className="field">
            <label>Usuário</label>
            <input value={usuario} onChange={e => setUsuario(e.target.value)}
              placeholder="seu usuário" autoComplete="username" required />
          </div>
          <div className="field">
            <label>Senha</label>
            <input type="password" value={senha} onChange={e => setSenha(e.target.value)}
              placeholder="sua senha" autoComplete="current-password" required />
          </div>
          <button className="btn-primary" type="submit" disabled={loading}>
            {loading ? 'Entrando...' : 'Entrar'}
          </button>
        </form>
      </div>
    </div>
  )
}
```

- [ ] **Step 3: Testar login no browser**

```bash
cd frontend && npm run dev
```

Acesse `http://localhost:5173/login`. Faça login com usuário `CTI`. Deve redirecionar para `/admin/overview` (página em branco por ora — normal).

- [ ] **Step 4: Testar LoginPage**

Criar `frontend/src/pages/LoginPage.test.tsx`:
```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { AuthProvider } from '../contexts/AuthContext'
import LoginPage from './LoginPage'
import * as authApi from '../api/auth'

vi.mock('../api/auth')

function renderLogin() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <LoginPage />
      </AuthProvider>
    </MemoryRouter>
  )
}

test('renderiza campos de usuário e senha', () => {
  renderLogin()
  expect(screen.getByPlaceholderText('seu usuário')).toBeInTheDocument()
  expect(screen.getByPlaceholderText('sua senha')).toBeInTheDocument()
})

test('exibe mensagem de erro quando login falha', async () => {
  vi.mocked(authApi.login).mockRejectedValueOnce(new Error('Usuário ou senha inválidos'))
  renderLogin()
  fireEvent.change(screen.getByPlaceholderText('seu usuário'), { target: { value: 'errado' } })
  fireEvent.change(screen.getByPlaceholderText('sua senha'), { target: { value: '123' } })
  fireEvent.click(screen.getByText('Entrar'))
  await waitFor(() => expect(screen.getByText('Usuário ou senha inválidos')).toBeInTheDocument())
})

test('botão mostra "Entrando..." durante loading', async () => {
  vi.mocked(authApi.login).mockImplementation(() => new Promise(() => {}))
  renderLogin()
  fireEvent.change(screen.getByPlaceholderText('seu usuário'), { target: { value: 'CTI' } })
  fireEvent.change(screen.getByPlaceholderText('sua senha'), { target: { value: '123456' } })
  fireEvent.click(screen.getByText('Entrar'))
  await waitFor(() => expect(screen.getByText('Entrando...')).toBeInTheDocument())
})
```

Run: `npx vitest run src/pages/LoginPage.test.tsx`
Expected: 3 passed.

- [ ] **Step 5: Commitar LoginPage com testes**

```bash
git add frontend/src/pages/LoginPage.tsx frontend/src/pages/LoginPage.css frontend/src/pages/LoginPage.test.tsx
git commit -m "feat: implement LoginPage with unit tests"
```

---

### Task 8: Hooks de dados (useUsuarios + useRegistros)

**Files:**
- Create: `frontend/src/hooks/useUsuarios.ts`
- Create: `frontend/src/hooks/useRegistros.ts`

- [ ] **Step 1: Criar frontend/src/hooks/useUsuarios.ts**

```ts
import { useState, useEffect, useCallback } from 'react'
import { listarUsuarios, criarUsuario, atualizarUsuario, desativarUsuario } from '../api/usuarios'
import type { Usuario } from '../types'

export function useUsuarios() {
  const [usuarios, setUsuarios] = useState<Usuario[]>([])
  const [loading, setLoading] = useState(true)
  const [erro, setErro] = useState('')

  const carregar = useCallback(async () => {
    setLoading(true)
    try {
      const res = await listarUsuarios()
      setUsuarios(res.dados)
    } catch (e: any) {
      setErro(e.message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { carregar() }, [carregar])

  const criar = async (dto: Parameters<typeof criarUsuario>[0]) => {
    const res = await criarUsuario(dto)
    await carregar()
    return res.dados
  }

  const atualizar = async (id: string, dto: Parameters<typeof atualizarUsuario>[1]) => {
    const res = await atualizarUsuario(id, dto)
    await carregar()
    return res.dados
  }

  const desativar = async (id: string) => {
    await desativarUsuario(id)
    await carregar()
  }

  return { usuarios, loading, erro, criar, atualizar, desativar, recarregar: carregar }
}
```

- [ ] **Step 2: Criar frontend/src/hooks/useRegistros.ts**

```ts
import { useState, useEffect, useCallback } from 'react'
import { listarRegistros, salvarRegistro, excluirRegistro, obterRegistroPorData } from '../api/registros'
import type { Registro } from '../types'

export function useRegistros(clienteId: string | null) {
  const [registros, setRegistros] = useState<Registro[]>([])
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true)
    try {
      const res = await listarRegistros(clienteId)
      setRegistros(res.dados ?? [])
    } catch (e: any) {
      setErro(e.message)
    } finally {
      setLoading(false)
    }
  }, [clienteId])

  useEffect(() => { carregar() }, [carregar])

  const salvar = async (dto: Parameters<typeof salvarRegistro>[0]) => {
    const res = await salvarRegistro(dto)
    await carregar()
    return res.dados
  }

  const excluir = async (data: string, motivo: string) => {
    if (!clienteId) return
    await excluirRegistro(clienteId, data, motivo)
    await carregar()
  }

  const buscarPorData = async (data: string) => {
    if (!clienteId) return null
    try {
      const res = await obterRegistroPorData(clienteId, data)
      return res.dados
    } catch {
      return null
    }
  }

  return { registros, loading, erro, salvar, excluir, buscarPorData, recarregar: carregar }
}
```

- [ ] **Step 3: Testar useUsuarios**

Criar `frontend/src/hooks/useUsuarios.test.tsx`:
```tsx
import { renderHook, waitFor } from '@testing-library/react'
import { useUsuarios } from './useUsuarios'
import * as api from '../api/usuarios'

vi.mock('../api/usuarios')

const mockUsuarios = [
  { id: '1', nomeUsuario: 'cli1', nomeCompleto: 'Cliente 1', nomeEstabelecimento: 'Loja 1', perfil: 'cliente', ativo: true, criadoEm: '', criadoPor: '' }
]

test('carrega usuários ao montar', async () => {
  vi.mocked(api.listarUsuarios).mockResolvedValue({ dados: mockUsuarios, codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useUsuarios())
  await waitFor(() => expect(result.current.loading).toBe(false))
  expect(result.current.usuarios).toHaveLength(1)
  expect(result.current.usuarios[0].nomeCompleto).toBe('Cliente 1')
})

test('expõe erro quando API falha', async () => {
  vi.mocked(api.listarUsuarios).mockRejectedValue(new Error('Sem conexão'))
  const { result } = renderHook(() => useUsuarios())
  await waitFor(() => expect(result.current.loading).toBe(false))
  expect(result.current.erro).toBe('Sem conexão')
})
```

- [ ] **Step 4: Testar useRegistros**

Criar `frontend/src/hooks/useRegistros.test.tsx`:
```tsx
import { renderHook, waitFor } from '@testing-library/react'
import { useRegistros } from './useRegistros'
import * as api from '../api/registros'

vi.mock('../api/registros')

const mockRegistros = [
  { id: 'r1', clienteId: 'c1', data: '2026-05-15', saldoInicio: 100, entrada: 200,
    saidas: [{ descricao: 'Despesa', valor: 50 }], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 250, saldoCalculado: 250, criadoEm: '' }
]

test('não carrega quando clienteId é null', () => {
  const { result } = renderHook(() => useRegistros(null))
  expect(result.current.loading).toBe(false)
  expect(result.current.registros).toHaveLength(0)
})

test('carrega registros quando clienteId é fornecido', async () => {
  vi.mocked(api.listarRegistros).mockResolvedValue({ dados: mockRegistros, codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useRegistros('c1'))
  await waitFor(() => expect(result.current.loading).toBe(false))
  expect(result.current.registros).toHaveLength(1)
  expect(result.current.registros[0].saldoConfirmado).toBe(250)
})

test('buscarPorData retorna null quando não encontrado', async () => {
  vi.mocked(api.listarRegistros).mockResolvedValue({ dados: [], codigoRetorno: 'OK', mensagem: '' })
  vi.mocked(api.obterRegistroPorData).mockRejectedValue(new Error('não encontrado'))
  const { result } = renderHook(() => useRegistros('c1'))
  await waitFor(() => expect(result.current.loading).toBe(false))
  const reg = await result.current.buscarPorData('2026-05-01')
  expect(reg).toBeNull()
})
```

Run: `npx vitest run src/hooks/`
Expected: 5 passed.

- [ ] **Step 5: Commitar hooks com testes**

```bash
git add frontend/src/hooks/
git commit -m "feat: add useUsuarios and useRegistros hooks with unit tests"
```

---

### Task 9: Páginas Admin (Overview + Clientes + Ver Caixa)

**Files:**
- Create: `frontend/src/pages/admin/AdminOverviewPage.tsx`
- Create: `frontend/src/pages/admin/AdminClientsPage.tsx`
- Create: `frontend/src/pages/admin/AdminCaixaPage.tsx`
- Create: `frontend/src/pages/admin/Admin.css`

- [ ] **Step 1: Criar frontend/src/pages/admin/Admin.css**

```css
.admin-grid {
  display: grid;
  grid-template-columns: 320px 1fr;
  gap: 20px;
}
@media (max-width: 900px) { .admin-grid { grid-template-columns: 1fr; } }

.client-list-item {
  display: flex; align-items: center; justify-content: space-between;
  padding: 10px 12px; border-radius: 10px; cursor: pointer;
  margin-bottom: 6px; border: 1px solid transparent; transition: all .2s;
}
.client-list-item:hover  { background: var(--bg-hover); border-color: var(--bd-str); }
.client-list-item.selected { background: #1a2a1a; border-color: #34c759; }
.client-name  { font-weight: 600; font-size: 14px; }
.client-store { font-size: 12px; color: var(--tx3); margin-top: 2px; }

.btn-add-client {
  width: 100%; padding: 10px;
  background: #1a2a1a; border: 1px dashed #2a5a2a;
  border-radius: 10px; color: #34c759; font-size: 13px; font-weight: 600; margin-top: 8px;
}
.btn-add-client:hover { background: #1f3a1f; }

.overview-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 14px; margin-bottom: 20px;
}
.ov-card {
  background: var(--bg-card); border: 1px solid var(--bd);
  border-radius: 14px; padding: 18px; transition: all .2s;
}
.ov-card:hover { border-color: #5ac8fa; background: var(--ov-hv); }
.ov-name  { font-weight: 700; font-size: 15px; }
.ov-store { font-size: 12px; color: var(--tx3); }
.ov-saldo { font-size: 22px; font-weight: 800; color: #34c759; margin: 8px 0; }
.ov-stats { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-top: 10px; }
.ov-stat  { background: var(--ov-stat); border-radius: 8px; padding: 8px 10px; }
.ov-stat-lbl { font-size: 11px; color: var(--tx4); }
.ov-stat-val { font-size: 14px; font-weight: 600; margin-top: 2px; }
.btn-view-client {
  margin-top: 12px; width: 100%; padding: 8px;
  background: var(--vc-bg); border: 1px solid var(--vc-bd);
  border-radius: 8px; color: #5ac8fa; font-size: 13px; font-weight: 600;
}
.btn-view-client:hover { background: #1a3a4a; }
```

- [ ] **Step 2: Criar frontend/src/pages/admin/AdminOverviewPage.tsx**

```tsx
import { useNavigate } from 'react-router-dom'
import { useUsuarios } from '../../hooks/useUsuarios'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL } from '../../utils/format'
import './Admin.css'

function ClientCard({ usuario }: { usuario: any }) {
  const { registros } = useRegistros(usuario.id)
  const navigate = useNavigate()
  const ultimo = registros[0]
  const mes = registros.filter(r => r.data.slice(0, 7) === new Date().toISOString().slice(0, 7))
  const totalEnt = mes.reduce((s, r) => s + r.entrada, 0)
  const totalSai = mes.reduce((s, r) => s + r.saidas.reduce((a: number, x: any) => a + x.valor, 0), 0)

  return (
    <div className="ov-card">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <div className="ov-name">{usuario.nomeCompleto}</div>
          <div className="ov-store">{usuario.nomeEstabelecimento}</div>
        </div>
      </div>
      <div className="ov-saldo">{fmtBRL(ultimo?.saldoConfirmado ?? 0)}</div>
      <div className="ov-stats">
        <div className="ov-stat">
          <div className="ov-stat-lbl">Entradas (mês)</div>
          <div className="ov-stat-val val-green">{fmtBRL(totalEnt)}</div>
        </div>
        <div className="ov-stat">
          <div className="ov-stat-lbl">Saídas (mês)</div>
          <div className="ov-stat-val val-red">{fmtBRL(totalSai)}</div>
        </div>
      </div>
      <button className="btn-view-client" onClick={() => navigate('/admin/caixa', { state: { clienteId: usuario.id } })}>
        Ver caixa →
      </button>
    </div>
  )
}

export default function AdminOverviewPage() {
  const { usuarios, loading } = useUsuarios()
  const clientes = usuarios.filter(u => u.perfil === 'cliente' && u.ativo)

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="stats-grid">
        <StatCard label="👥 Total clientes" value={String(clientes.length)} />
      </div>
      <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14, color: '#888' }}>
        Clientes — Último saldo registrado
      </h3>
      <div className="overview-cards">
        {clientes.map(u => <ClientCard key={u.id} usuario={u} />)}
      </div>
    </>
  )
}
```

- [ ] **Step 3: Criar frontend/src/pages/admin/AdminClientsPage.tsx**

```tsx
import { useState } from 'react'
import { useUsuarios } from '../../hooks/useUsuarios'
import Modal from '../../components/shared/Modal'
import type { Usuario } from '../../types'
import './Admin.css'

function ClientForm({ initial, onSave, onCancel }: {
  initial?: Partial<Usuario & { senha: string }>
  onSave: (d: any) => void
  onCancel: () => void
}) {
  const [nomeCompleto, setNomeCompleto] = useState(initial?.nomeCompleto ?? '')
  const [nomeEstabelecimento, setNomeEstabelecimento] = useState(initial?.nomeEstabelecimento ?? '')
  const [nomeUsuario, setNomeUsuario] = useState(initial?.nomeUsuario ?? '')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState('')

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setErro('')
    if (!nomeCompleto || !nomeUsuario) { setErro('Preencha todos os campos obrigatórios.'); return }
    onSave({ nomeCompleto, nomeEstabelecimento, nomeUsuario, senha: senha || undefined, perfil: 'cliente' })
  }

  return (
    <form onSubmit={handleSubmit}>
      {erro && <div className="err-box" style={{ marginBottom: 12, background: 'var(--err-bg)', border: '1px solid #ff3b30', color: '#ff6b6b', padding: '8px 12px', borderRadius: 8 }}>{erro}</div>}
      <div className="inp-group"><label>Nome completo *</label><input value={nomeCompleto} onChange={e => setNomeCompleto(e.target.value)} required /></div>
      <div className="inp-group"><label>Estabelecimento</label><input value={nomeEstabelecimento} onChange={e => setNomeEstabelecimento(e.target.value)} /></div>
      {!initial && <div className="inp-group"><label>Usuário *</label><input value={nomeUsuario} onChange={e => setNomeUsuario(e.target.value)} required /></div>}
      <div className="inp-group"><label>Senha {initial ? '(deixe em branco para manter)' : '*'}</label><input type="password" value={senha} onChange={e => setSenha(e.target.value)} required={!initial} /></div>
      <div className="modal-footer">
        <button type="button" className="btn-cancel" onClick={onCancel}>Cancelar</button>
        <button type="submit" className="btn-confirm">Salvar</button>
      </div>
    </form>
  )
}

export default function AdminClientsPage() {
  const { usuarios, loading, criar, atualizar, desativar } = useUsuarios()
  const clientes = usuarios.filter(u => u.perfil === 'cliente')
  const [selected, setSelected] = useState<Usuario | null>(null)
  const [showAdd, setShowAdd] = useState(false)
  const [showDelete, setShowDelete] = useState(false)
  const [erro, setErro] = useState('')

  async function handleCriar(dto: any) {
    try { await criar(dto); setShowAdd(false) }
    catch (e: any) { setErro(e.message) }
  }

  async function handleAtualizar(dto: any) {
    if (!selected) return
    try { await atualizar(selected.id, dto); setSelected(null) }
    catch (e: any) { setErro(e.message) }
  }

  async function handleDesativar() {
    if (!selected) return
    try { await desativar(selected.id); setSelected(null); setShowDelete(false) }
    catch (e: any) { setErro(e.message) }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      {erro && <div style={{ color: '#ff6b6b', marginBottom: 12 }}>{erro}</div>}
      <div className="admin-grid">
        <div className="panel">
          <div className="panel-title">
            <span>👥 Clientes</span>
            <span style={{ fontSize: 12, color: '#666' }}>{clientes.length} clientes</span>
          </div>
          {clientes.map(u => (
            <div key={u.id} className={`client-list-item ${selected?.id === u.id ? 'selected' : ''}`}
              onClick={() => setSelected(u)}>
              <div>
                <div className="client-name">{u.nomeCompleto}</div>
                <div className="client-store">{u.nomeEstabelecimento}</div>
              </div>
              {!u.ativo && <span style={{ fontSize: 11, color: '#ff6b6b' }}>Inativo</span>}
            </div>
          ))}
          <button className="btn-add-client" onClick={() => setShowAdd(true)}>＋ Novo cliente</button>
        </div>

        <div className="panel">
          {selected ? (
            <>
              <div className="panel-title">
                <span>{selected.nomeCompleto}</span>
                {selected.ativo && (
                  <button className="btn-sm" style={{ background: 'var(--rm-bg)', border: '1px solid var(--rm-bd)', color: '#ff6b6b' }}
                    onClick={() => setShowDelete(true)}>Excluir</button>
                )}
              </div>
              <ClientForm initial={selected} onSave={handleAtualizar} onCancel={() => setSelected(null)} />
            </>
          ) : (
            <>
              <div className="panel-title">Selecione um cliente</div>
              <p style={{ color: '#666', fontSize: 14 }}>Clique em um cliente para ver ou editar.</p>
            </>
          )}
        </div>
      </div>

      <Modal open={showAdd} title="Novo cliente" onClose={() => setShowAdd(false)}>
        <ClientForm onSave={handleCriar} onCancel={() => setShowAdd(false)} />
      </Modal>

      <Modal open={showDelete} title="Confirmar exclusão" onClose={() => setShowDelete(false)}
        footer={<><button className="btn-cancel" onClick={() => setShowDelete(false)}>Cancelar</button><button className="btn-danger" onClick={handleDesativar}>Excluir</button></>}>
        <p>Deseja desativar o cliente <strong>{selected?.nomeCompleto}</strong>?</p>
      </Modal>
    </>
  )
}
```

- [ ] **Step 4: Criar frontend/src/pages/admin/AdminCaixaPage.tsx**

```tsx
import { useLocation } from 'react-router-dom'
import ClientCaixaPage from '../client/ClientCaixaPage'
import ClientHistoricoPage from '../client/ClientHistoricoPage'
import { useState } from 'react'

export default function AdminCaixaPage() {
  const location = useLocation()
  const clienteId = (location.state as any)?.clienteId ?? null
  const [tab, setTab] = useState<'caixa' | 'hist'>('caixa')

  if (!clienteId) return <p style={{ color: 'var(--tx3)' }}>Selecione um cliente na Visão Geral.</p>

  return (
    <>
      <div style={{ display: 'flex', gap: 8, marginBottom: 20 }}>
        <button className={`btn-sm ${tab === 'caixa' ? 'btn-confirm' : ''}`} onClick={() => setTab('caixa')}>📋 Caixa</button>
        <button className={`btn-sm ${tab === 'hist' ? 'btn-confirm' : ''}`} onClick={() => setTab('hist')}>📈 Histórico</button>
      </div>
      {tab === 'caixa' ? <ClientCaixaPage clienteIdOverride={clienteId} /> : <ClientHistoricoPage clienteIdOverride={clienteId} />}
    </>
  )
}
```

- [ ] **Step 5: Commitar páginas admin**

```bash
git add frontend/src/pages/admin/
git commit -m "feat: implement admin pages (Overview, Clients, Caixa)"
```

---

### Task 10: Página ClientCaixaPage

**Files:**
- Create: `frontend/src/pages/client/ClientCaixaPage.tsx`
- Create: `frontend/src/pages/client/ClientCaixa.css`

- [ ] **Step 1: Criar frontend/src/pages/client/ClientCaixa.css**

```css
.saida-row, .prov-row {
  display: grid;
  grid-template-columns: 1fr 110px 32px;
  gap: 8px; margin-bottom: 8px; align-items: center;
}
.saida-row input, .prov-row input {
  padding: 9px 12px; background: var(--bg-input);
  border: 1px solid var(--bd); border-radius: 8px;
  color: var(--tx); font-size: 14px; width: 100%;
}
.saida-row input:focus { border-color: #ff3b30; outline: none; }
.prov-row input:focus  { border-color: #5ac8fa; outline: none; }

.btn-rm {
  background: var(--rm-bg); border: 1px solid var(--rm-bd);
  color: #ff6b6b; width: 32px; height: 36px; border-radius: 8px; font-size: 14px;
}
.btn-rm:hover { background: #3a1010; }
.btn-rm-prov {
  background: var(--rmp-bg); border: 1px solid var(--rmp-bd);
  color: #5ac8fa; width: 32px; height: 36px; border-radius: 8px; font-size: 14px;
}
.btn-rm-prov:hover { background: #1a3a4a; }

.btn-add-saida {
  width: 100%; padding: 9px;
  background: var(--rm-bg); border: 1px dashed var(--rm-bd);
  border-radius: 8px; color: #ff6b6b; font-size: 13px; font-weight: 600; margin-top: 4px;
}
.btn-add-saida:hover { background: #201010; }
.btn-add-receber {
  width: 100%; padding: 9px; border: 1px dashed;
  border-radius: 8px; font-size: 13px; font-weight: 600; margin-top: 4px;
  background: var(--ov-hv); border-color: var(--rmp-bd); color: #5ac8fa;
}
.btn-add-receber:hover { background: #0a2a3a; }
.btn-add-pagar {
  width: 100%; padding: 9px; border: 1px dashed;
  border-radius: 8px; font-size: 13px; font-weight: 600; margin-top: 4px;
  background: var(--rm-bg); border-color: var(--rm-bd); color: #ff6b6b;
}
.btn-add-pagar:hover { background: #1a1010; }

.saldo-box {
  background: var(--sl-bg, #0a1a0a); border: 1px solid var(--sl-bd, #1a4a1a);
  border-radius: 12px; padding: 16px 20px;
  display: flex; align-items: center; justify-content: space-between; margin: 16px 0;
}
.saldo-calc-lbl { font-size: 13px; color: var(--tx3); }
.saldo-calc-val { font-size: 24px; font-weight: 800; color: #34c759; }
.dif-msg { font-size: 13px; font-weight: 600; margin-top: 8px; min-height: 18px; }

.btn-save {
  width: 100%; padding: 14px;
  background: linear-gradient(135deg, #34c759, #30d158);
  color: #000; border: none; border-radius: 10px;
  font-size: 15px; font-weight: 700;
  display: flex; align-items: center; justify-content: center; gap: 8px; margin-top: 12px;
}
.btn-save:hover { opacity: .9; }
.btn-save:disabled { opacity: .5; cursor: not-allowed; }
```

- [ ] **Step 2: Criar frontend/src/pages/client/ClientCaixaPage.tsx**

```tsx
import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import DayNav from '../../components/shared/DayNav'
import { fmtBRL, todayISO, addDays } from '../../utils/format'
import type { Saida, ContaProvisionada } from '../../types'
import './ClientCaixa.css'

interface Props { clienteIdOverride?: string }

export default function ClientCaixaPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, salvar, buscarPorData } = useRegistros(clienteId)

  const [data, setData] = useState(todayISO())
  const [inicio, setInicio] = useState(0)
  const [entrada, setEntrada] = useState('')
  const [saidas, setSaidas] = useState<Saida[]>([{ descricao: '', valor: 0 }])
  const [aReceber, setAReceber] = useState<ContaProvisionada[]>([])
  const [aPagar, setAPagar] = useState<ContaProvisionada[]>([])
  const [confirmado, setConfirmado] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')

  const totalSaidas = saidas.reduce((s, x) => s + (Number(x.valor) || 0), 0)
  const calculado = inicio + (Number(entrada) || 0) - totalSaidas
  const dif = confirmado !== '' ? calculado - Number(confirmado) : null

  const carregarDia = useCallback(async (d: string) => {
    const reg = await buscarPorData(d)
    if (reg) {
      setInicio(reg.saldoInicio)
      setEntrada(String(reg.entrada))
      setSaidas(reg.saidas.length ? reg.saidas : [{ descricao: '', valor: 0 }])
      setAReceber(reg.contasAReceber)
      setAPagar(reg.contasAPagar)
      setConfirmado(String(reg.saldoConfirmado))
    } else {
      const prev = registros.find(r => r.data < d)
      setInicio(prev?.saldoConfirmado ?? 0)
      setEntrada('')
      setSaidas([{ descricao: '', valor: 0 }])
      setAReceber([])
      setAPagar([])
      setConfirmado('')
    }
  }, [buscarPorData, registros])

  useEffect(() => { carregarDia(data) }, [data])

  async function handleSave() {
    if (!clienteId) return
    setSaving(true)
    setMsg('')
    try {
      await salvar({
        clienteId,
        data,
        saldoInicio: inicio,
        entrada: Number(entrada) || 0,
        saidas: saidas.filter(s => s.descricao || s.valor),
        contasAReceber: aReceber.filter(s => s.descricao || s.valor),
        contasAPagar: aPagar.filter(s => s.descricao || s.valor),
        saldoConfirmado: Number(confirmado) || calculado,
      })
      setMsg('✅ Salvo com sucesso!')
    } catch (e: any) {
      setMsg(`❌ ${e.message}`)
    } finally {
      setSaving(false)
    }
  }

  function updateSaida(i: number, field: keyof Saida, val: string) {
    setSaidas(s => s.map((x, j) => j === i ? { ...x, [field]: field === 'valor' ? Number(val) : val } : x))
  }
  function updateProv(list: ContaProvisionada[], setList: (v: ContaProvisionada[]) => void, i: number, field: keyof ContaProvisionada, val: string) {
    setList(list.map((x, j) => j === i ? { ...x, [field]: field === 'valor' ? Number(val) : val } : x))
  }

  return (
    <>
      <DayNav date={data} onPrev={() => setData(d => addDays(d, -1))} onNext={() => setData(d => addDays(d, 1))} />
      <div className="stats-grid">
        <StatCard label="📥 Início" value={fmtBRL(inicio)} />
        <StatCard label="📤 Entrada" value={fmtBRL(Number(entrada) || 0)} className="val-green" />
        <StatCard label="💸 Saídas" value={fmtBRL(totalSaidas)} className="val-red" />
        <StatCard label="💰 Saldo" value={fmtBRL(calculado)} className="val-green" />
      </div>

      <div className="form-card">
        <h3>📋 Registro do dia</h3>
        <div className="inp-group">
          <label>Saldo início (preenchido automaticamente)</label>
          <input type="number" value={inicio} readOnly style={{ color: 'var(--tx4)', cursor: 'not-allowed' }} />
        </div>
        <div className="inp-group">
          <label>💵 Dinheiro que entrou hoje (R$)</label>
          <input type="number" value={entrada} onChange={e => setEntrada(e.target.value)} placeholder="0,00" step="0.01" min="0" />
        </div>
        <div className="inp-group">
          <label>💸 Saídas do dia</label>
          {saidas.map((s, i) => (
            <div key={i} className="saida-row">
              <input placeholder="Descrição" value={s.descricao} onChange={e => updateSaida(i, 'descricao', e.target.value)} />
              <input type="number" placeholder="R$" value={s.valor || ''} onChange={e => updateSaida(i, 'valor', e.target.value)} step="0.01" min="0" />
              <button className="btn-rm" onClick={() => setSaidas(s => s.filter((_, j) => j !== i))}>✕</button>
            </div>
          ))}
          <button className="btn-add-saida" onClick={() => setSaidas(s => [...s, { descricao: '', valor: 0 }])}>＋ Adicionar saída</button>
        </div>
      </div>

      <div className="form-card">
        <h3>📥 Contas a Receber (Provisionamento)</h3>
        {aReceber.map((s, i) => (
          <div key={i} className="prov-row">
            <input placeholder="Descrição" value={s.descricao} onChange={e => updateProv(aReceber, setAReceber, i, 'descricao', e.target.value)} />
            <input type="number" placeholder="R$" value={s.valor || ''} onChange={e => updateProv(aReceber, setAReceber, i, 'valor', e.target.value)} step="0.01" min="0" />
            <button className="btn-rm-prov" onClick={() => setAReceber(a => a.filter((_, j) => j !== i))}>✕</button>
          </div>
        ))}
        <button className="btn-add-receber" onClick={() => setAReceber(a => [...a, { descricao: '', valor: 0 }])}>＋ Adicionar a Receber</button>
      </div>

      <div className="form-card">
        <h3>📤 Contas a Pagar (Provisionamento)</h3>
        {aPagar.map((s, i) => (
          <div key={i} className="prov-row">
            <input placeholder="Descrição" value={s.descricao} onChange={e => updateProv(aPagar, setAPagar, i, 'descricao', e.target.value)} />
            <input type="number" placeholder="R$" value={s.valor || ''} onChange={e => updateProv(aPagar, setAPagar, i, 'valor', e.target.value)} step="0.01" min="0" />
            <button className="btn-rm-prov" onClick={() => setAPagar(a => a.filter((_, j) => j !== i))}>✕</button>
          </div>
        ))}
        <button className="btn-add-pagar" onClick={() => setAPagar(a => [...a, { descricao: '', valor: 0 }])}>＋ Adicionar a Pagar</button>
      </div>

      <div className="saldo-box">
        <div>
          <div className="saldo-calc-lbl">Saldo calculado</div>
          <div className="saldo-calc-val">{fmtBRL(calculado)}</div>
        </div>
        <div style={{ textAlign: 'right' }}>
          <div className="saldo-calc-lbl">Confirmar saldo (R$)</div>
          <input type="number" value={confirmado} onChange={e => setConfirmado(e.target.value)} placeholder="0,00" step="0.01"
            style={{ width: 140, padding: '8px 12px', background: '#111', border: '2px solid #34c759', borderRadius: 8, color: '#fff', fontSize: 17, fontWeight: 700, textAlign: 'right' }} />
        </div>
      </div>
      {dif !== null && (
        <div className={`dif-msg ${Math.abs(dif) < 0.01 ? 'val-green' : 'val-red'}`}>
          {Math.abs(dif) < 0.01 ? '✅ Saldo conferido!' : `⚠️ Diferença: ${fmtBRL(Math.abs(dif))}`}
        </div>
      )}
      {msg && <div style={{ marginTop: 8, fontWeight: 600, color: msg.startsWith('✅') ? '#34c759' : '#ff6b6b' }}>{msg}</div>}
      <button className="btn-save" onClick={handleSave} disabled={saving}>
        {saving ? 'Salvando...' : '☁️ Salvar e sincronizar'}
      </button>
    </>
  )
}
```

- [ ] **Step 3: Commitar ClientCaixaPage**

```bash
git add frontend/src/pages/client/
git commit -m "feat: implement ClientCaixaPage with full daily register"
```

---

### Task 11: Páginas ClientHistoricoPage e ClientGraficoPage

**Files:**
- Create: `frontend/src/pages/client/ClientHistoricoPage.tsx`
- Create: `frontend/src/pages/client/ClientGraficoPage.tsx`

- [ ] **Step 1: Criar frontend/src/pages/client/ClientHistoricoPage.tsx**

```tsx
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtDate, monthLabel } from '../../utils/format'
import StatCard from '../../components/shared/StatCard'
import { useState } from 'react'

interface Props { clienteIdOverride?: string }

export default function ClientHistoricoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, loading, excluir } = useRegistros(clienteId)
  const [openId, setOpenId] = useState<string | null>(null)
  const [delData, setDelData] = useState<string | null>(null)
  const [motivo, setMotivo] = useState('')

  const mesAtual = new Date().toISOString().slice(0, 7)
  const doMes = registros.filter(r => r.data.startsWith(mesAtual))
  const totalEnt = doMes.reduce((s, r) => s + r.entrada, 0)
  const totalSai = doMes.reduce((s, r) => s + r.saidas.reduce((a: number, x: any) => a + x.valor, 0), 0)
  const ultimo = doMes[0]?.saldoConfirmado ?? 0

  async function handleDelete() {
    if (!delData) return
    try { await excluir(delData, motivo); setDelData(null); setMotivo('') }
    catch (e: any) { alert(e.message) }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="stats-grid">
        <StatCard label="📅 Mês atual" value={monthLabel(mesAtual)} className="val-blue" />
        <StatCard label="📤 Total entradas" value={fmtBRL(totalEnt)} className="val-green" />
        <StatCard label="💸 Total saídas" value={fmtBRL(totalSai)} className="val-red" />
        <StatCard label="💰 Último saldo" value={fmtBRL(ultimo)} className="val-green" />
      </div>

      {registros.map(r => (
        <div key={r.id} className="hist-item" style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 10, marginBottom: 8, overflow: 'hidden' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 16px', cursor: 'pointer' }}
            onClick={() => setOpenId(openId === r.id ? null : r.id)}>
            <div>
              <div style={{ fontWeight: 600, fontSize: 14 }}>{fmtDate(r.data)}</div>
              <div style={{ fontSize: 12, color: 'var(--tx3)', marginTop: 2 }}>
                Entrada: {fmtBRL(r.entrada)} · Saídas: {fmtBRL(r.saidas.reduce((a: number, x: any) => a + x.valor, 0))}
              </div>
            </div>
            <div style={{ fontSize: 15, fontWeight: 700, color: '#34c759' }}>{fmtBRL(r.saldoConfirmado)}</div>
          </div>
          {openId === r.id && (
            <div style={{ padding: '12px 16px', borderTop: '1px solid var(--bd-sub)' }}>
              {r.saidas.map((s: any, i: number) => (
                <div key={i} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, padding: '4px 0', borderBottom: '1px solid var(--bg-card)' }}>
                  <span>{s.descricao}</span><span style={{ color: '#ff3b30' }}>-{fmtBRL(s.valor)}</span>
                </div>
              ))}
              <button style={{ marginTop: 8, background: 'none', border: '1px solid var(--bd)', borderRadius: 6, color: '#ff6b6b', fontSize: 12, padding: '3px 10px', cursor: 'pointer' }}
                onClick={() => setDelData(r.data)}>🗑 Excluir registro</button>
            </div>
          )}
        </div>
      ))}

      {delData && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.7)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: 'var(--bg-card)', borderRadius: 16, padding: 28, maxWidth: 400, width: '100%' }}>
            <div style={{ fontWeight: 700, fontSize: 17, marginBottom: 16 }}>Excluir registro de {delData}?</div>
            <div className="inp-group"><label>Motivo</label><input value={motivo} onChange={e => setMotivo(e.target.value)} placeholder="Informe o motivo" /></div>
            <div style={{ display: 'flex', gap: 10, marginTop: 16, justifyContent: 'flex-end' }}>
              <button className="btn-cancel" onClick={() => setDelData(null)}>Cancelar</button>
              <button className="btn-danger" onClick={handleDelete}>Excluir</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
```

- [ ] **Step 2: Criar frontend/src/pages/client/ClientGraficoPage.tsx**

```tsx
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL, fmtDate } from '../../utils/format'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'

interface Props { clienteIdOverride?: string }

export default function ClientGraficoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, loading } = useRegistros(clienteId)

  const mesAtual = new Date().toISOString().slice(0, 7)
  const doMes = registros.filter(r => r.data.startsWith(mesAtual)).slice().reverse()

  const data = doMes.map(r => ({
    dia: fmtDate(r.data).slice(0, 5),
    saldo: r.saldoConfirmado,
  }))

  const primeiro = doMes[0]?.saldoInicio ?? 0
  const ultimo = doMes[doMes.length - 1]?.saldoConfirmado ?? 0
  const totalEnt = doMes.reduce((s, r) => s + r.entrada, 0)
  const totalSai = doMes.reduce((s, r) => s + r.saidas.reduce((a: number, x: any) => a + x.valor, 0), 0)

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14, color: '#888' }}>📊 Evolução Mensal</h3>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 14, padding: 20, marginBottom: 24, height: 350 }}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
            <XAxis dataKey="dia" stroke="var(--tx3)" tick={{ fontSize: 12 }} />
            <YAxis stroke="var(--tx3)" tick={{ fontSize: 12 }} tickFormatter={v => fmtBRL(v).replace('R$ ', 'R$')} />
            <Tooltip formatter={(v: number) => fmtBRL(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
            <Line type="monotone" dataKey="saldo" stroke="#34c759" strokeWidth={2} dot={{ fill: '#34c759' }} />
          </LineChart>
        </ResponsiveContainer>
      </div>
      <div className="stats-grid">
        <StatCard label="💰 Saldo Inicial (Mês)" value={fmtBRL(primeiro)} className="val-blue" />
        <StatCard label="📈 Saldo Final (Mês)"   value={fmtBRL(ultimo)}   className="val-green" />
        <StatCard label="📤 Total Entradas"       value={fmtBRL(totalEnt)} className="val-green" />
        <StatCard label="💸 Total Saídas"         value={fmtBRL(totalSai)} className="val-red" />
      </div>
    </>
  )
}
```

- [ ] **Step 3: Commitar páginas cliente**

```bash
git add frontend/src/pages/client/
git commit -m "feat: implement ClientHistoricoPage and ClientGraficoPage"
```

---

### Task 12: Build e integração com wwwroot

**Files:**
- Modify: `frontend/package.json` (script deploy)
- Modify: `CaixaDiario.API/wwwroot/` (output do build)

- [ ] **Step 1: Verificar que vite.config.ts aponta para wwwroot**

O `outDir` já foi configurado na Task 1:
```ts
build: {
  outDir: '../CaixaDiario.API/wwwroot',
  emptyOutDir: true,
}
```

- [ ] **Step 2: Rodar build de produção**

```bash
cd frontend && npm run build
```

Expected: `dist` gerado em `CaixaDiario.API/wwwroot/` sem erros.

- [ ] **Step 3: Verificar que o backend serve o React**

```bash
cd CaixaDiario.API && dotnet run
```

Acesse `http://localhost:5131`. Deve aparecer a tela de login do React.

- [ ] **Step 4: Testar fluxo completo localmente**

- Login como admin → navegar nas abas → criar cliente → ver caixa
- Login como cliente → registrar caixa → ver histórico → ver gráfico
- Toggle de tema (claro/escuro)

- [ ] **Step 5: Adicionar wwwroot ao .gitignore parcialmente**

O `wwwroot/` contém o build gerado — não deve ir para o git (o Railway faz o build). Adicionar ao `.gitignore`:

```
CaixaDiario.API/wwwroot/
```

Porém o Railway precisará fazer o build. Adicionar script no `package.json` da raiz para facilitar:

Criar `package.json` na raiz do projeto:
```json
{
  "scripts": {
    "build:frontend": "cd frontend && npm install && npm run build"
  }
}
```

- [ ] **Step 6: Configurar Railway para buildar o frontend**

No Railway, na aba **Settings → Build**, alterar o custom build command para:
```
cd /app/../ && npm run build:frontend && cd /app && dotnet restore && dotnet publish -c Release -o out
```

Ou mais simples — adicionar um script de build ao `nixpacks.toml` já existente:

Atualizar `CaixaDiario.API/nixpacks.toml`:
```toml
[phases.setup]
cmds = ["curl -fsSL https://deb.nodesource.com/setup_20.x | bash -", "apt-get install -y nodejs"]

[phases.install]
cmds = ["npm --prefix ../frontend install", "dotnet restore"]

[phases.build]
cmds = ["npm --prefix ../frontend run build", "dotnet publish -c Release -o out"]

[start]
cmd = "dotnet out/CaixaDiario.API.dll"
```

- [ ] **Step 7: Commitar configuração de build**

```bash
git add CaixaDiario.API/nixpacks.toml package.json .gitignore
git commit -m "feat: configure build pipeline for React + .NET deploy"
```

---

### Task 13: Testes finais e abertura do PR

- [ ] **Step 1: Rodar todos os testes**

```bash
cd frontend && npx vitest run
```

Expected: 16+ testes passando, cobrindo:
- `utils/format.test.ts` — 6 testes (fmtBRL, fmtDate, addDays, monthLabel, todayISO)
- `contexts/AuthContext.test.tsx` — 2 testes (sem usuário, logout)
- `components/shared/StatCard.test.tsx` — 2 testes
- `components/shared/Modal.test.tsx` — 3 testes
- `components/shared/DayNav.test.tsx` — 3 testes
- `pages/LoginPage.test.tsx` — 3 testes (render, erro, loading)
- `hooks/useUsuarios.test.tsx` — 2 testes
- `hooks/useRegistros.test.tsx` — 3 testes

- [ ] **Step 2: Rodar build final**

```bash
npm run build
```

Expected: sem erros.

- [ ] **Step 3: Teste de regressão manual**

Com `dotnet run` no backend, testar:
- [ ] Login admin funciona
- [ ] Login cliente funciona
- [ ] Tema claro/escuro persiste ao recarregar
- [ ] Criar novo cliente
- [ ] Registrar caixa do dia
- [ ] Ver histórico com registros listados
- [ ] Gráfico exibe linha com dados

- [ ] **Step 4: Push da branch**

```bash
cd .. && git push -u origin feature/frontend-react
```

- [ ] **Step 5: Abrir PR no GitHub**

```bash
gh pr create \
  --title "feat: migrate frontend from HTML to React + Vite" \
  --body "Migração completa do frontend HTML puro para React + Vite. Mantém todas as funcionalidades existentes. Build integrado ao pipeline do Railway." \
  --base main \
  --head feature/frontend-react
```

- [ ] **Step 6: Aguardar aprovação do PR antes de mergear na main**

Revisar o PR e mergear apenas após aprovação. **Não fazer merge direto na main.**
