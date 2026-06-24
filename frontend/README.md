# Caixa Diário — Frontend

Interface web do Caixa Diário. **React 19 + TypeScript + Vite**, com **React Router** e gráficos via **Recharts**. Testes com **Vitest + React Testing Library**.

> Este frontend é compilado em arquivos estáticos e **servido pelo backend .NET**. O `npm run build` gera a saída diretamente em `../CaixaDiario.API/wwwroot/` (veja `vite.config.ts`). Por isso, ao rodar o backend sem buildar o frontend antes, as páginas retornam 404.

---

## Pré-requisitos

- Node.js 18+

## Comandos

```bash
npm install            # instalar dependências (primeira vez)

npm run dev            # servidor de desenvolvimento com hot-reload (proxy p/ a API)
npm run build          # compila e gera ../CaixaDiario.API/wwwroot/ (tsc -b + vite build)
npm run preview        # pré-visualiza o build de produção

npm test               # testes (Vitest, modo watch)
npm test -- LoginPage  # roda apenas os testes que casam com o padrão
npm run test:coverage  # testes com relatório de cobertura

npm run lint           # ESLint
```

### Dois fluxos de execução

- **Desenvolver o frontend:** rode o backend (`dotnet run` em `CaixaDiario.API/`) num terminal e `npm run dev` em outro. O Vite serve a interface ao vivo e encaminha as chamadas de API para o backend — **não** precisa de `npm run build`.
- **Servir tudo pelo backend:** rode `npm run build` para gerar o `wwwroot/` e então o backend serve a interface já compilada.

---

## Estrutura (`src/`)

- `api/` — **única** camada que fala com o backend. Todas as requisições passam por `apiFetch` (`api/client.ts`), que anexa o token JWT e redireciona para `/login` em caso de 401. Nunca chame `fetch` direto de um componente.
- `pages/` — telas, separadas por perfil: `admin/` e `client/`.
- `components/` — componentes reutilizáveis (`Layout/`, `shared/`).
- `hooks/` — hooks customizados (ex.: `useRegistros`, `useUsuarios`).
- `contexts/` — `AuthContext` (estado de autenticação e perfil do usuário).
- `utils/`, `types.ts`, `styles/` — utilitários, tipos compartilhados e estilos.

Perfis: **admin** gerencia usuários/clientes e vê tudo; **client** vê apenas os próprios registros.

---

## Convenções e testes

As regras de código, fluxo de Git/PR e padrões de teste do frontend estão no [`CLAUDE.md`](../CLAUDE.md) na raiz do repositório. Para a API consumida por este frontend, veja [`../CaixaDiario.API/README.md`](../CaixaDiario.API/README.md).

A variável `VITE_API_URL` define a base das chamadas de API (vazia por padrão, usando o mesmo host que serve a interface).
