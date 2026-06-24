# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> Written in English on purpose: it is the working language of the AI agent and keeps instructions unambiguous. Any additional docs added to this repo should also be read by the agent — see "Other docs the agent must read" at the end.
>
> **Audience note:** part of this team is not a professional software developer. Sections marked **[FOR NON-DEVS]** explain *why* a rule exists, not just the command. Treat those rules as mandatory, not optional.

---

## What this project is

Daily cash-flow control system ("Caixa Diário") for a small business with multiple clients. A single deployable: a **React + TypeScript (Vite)** frontend that is compiled into static files and **served by the ASP.NET Core (.NET 10) backend**. Data lives in **PostgreSQL (Supabase)**. Auth is **JWT**.

There is only one running process in production: the .NET API. It serves both the JSON API (`/api/...`) and the built frontend (everything else).

---

## Architecture — the big picture

### The frontend/backend coupling (read this first)

The frontend build output goes **into the backend**. `frontend/vite.config.ts` sets `outDir: '../CaixaDiario.API/wwwroot'`. The backend serves `wwwroot/` as static files.

**Consequence:** `CaixaDiario.API/wwwroot/` is git-ignored and starts empty. If you run the backend without building the frontend first, the API works but every page returns 404. **Always build the frontend before running/serving the backend** (the dev hot-reload flow below is the exception).

### Backend layering

```
Controller → Service → Repository → DbContext (EF Core) → PostgreSQL
```

- **Controllers** — HTTP only: receive request, check auth, delegate. No business logic.
- **Services** — all business rules (validation, calculations, permission checks). Services **never** touch `DbContext` directly — they go through a Repository. This is what makes Services unit-testable with a mocked Repository (no real database).
- **Repositories** — the only layer that talks to EF Core / the database.
- **Models** — EF Core entities. **DTOs** — API input/output contracts (never expose Models directly).

Each layer knows only the one directly below it. When adding a feature, add it through all layers in this order; do not let a Controller skip to the database.

### API response shape (keep consistent)

Success: `{ "codigo": "SUCESSO", "dados": { ... } }`
Error: `{ "status": 400, "codigo": "DADOS_INVALIDOS", "mensagem": "...", "campo": "nomeUsuario" }`

Errors are produced centrally via `ApiException` + the error-handling middleware. Throw an `ApiException` from a Service instead of returning ad-hoc error responses.

### Frontend structure

`src/api/` is the only place that calls the backend (all requests go through `apiFetch` in `src/api/client.ts`, which attaches the JWT and redirects to `/login` on 401). `src/pages/` (admin vs client areas), `src/components/`, `src/hooks/`, `src/contexts/AuthContext.tsx`. Roles: **admin** manages users/clients and sees everything; **client** sees only their own records.

---

## Commands

Run all commands from the directory shown. Prerequisites: **.NET 10 SDK** and **Node.js 18+**.

### First run / after `git clone` or `git clean`
```bash
# 1. Build the frontend → generates CaixaDiario.API/wwwroot/
cd frontend
npm install
npm run build

# 2. Run the backend (now serves the built frontend)
cd ../CaixaDiario.API
dotnet run            # → http://localhost:5131
```
The backend needs `CaixaDiario.API/appsettings.Development.json` (git-ignored — DB connection string + JWT secret). See `CaixaDiario.API/README.md` for its contents and the one-time admin-user setup.

### Day-to-day frontend development (hot reload)
```bash
# Terminal 1 — backend
cd CaixaDiario.API && dotnet run

# Terminal 2 — Vite dev server (proxies API calls to the backend)
cd frontend && npm run dev
```
In this flow you do **not** need `npm run build`; Vite serves the frontend live. Build is only for producing the static `wwwroot/`.

### Tests
```bash
# Backend (xUnit + Moq)
dotnet test CaixaDiario.Tests/
dotnet test CaixaDiario.Tests/ --filter "FullyQualifiedName~RegistroServiceTests"   # single class/test

# Frontend (Vitest)
cd frontend && npm test                 # watch mode
cd frontend && npm test -- ClientCaixaPage   # single file/pattern
cd frontend && npm run test:coverage    # with coverage
```
See **Testing standards** below for the rules these tests must follow.

### Lint
```bash
cd frontend && npm run lint   # ESLint (frontend only; backend relies on the C# compiler + nullable refs)
```

### Database migrations (EF Core)
```bash
cd CaixaDiario.API
dotnet ef migrations add <DescriptiveName>   # after changing a Model
dotnet ef database update                    # apply to the configured database
```
Requires `dotnet-ef`: `dotnet tool install --global dotnet-ef`.

### Production build (what Docker/Railway does)
Multi-stage `Dockerfile` at the repo root: builds the frontend, copies `wwwroot` into the API image, publishes the .NET app. You rarely run this locally; the platform builds it on deploy.

---

## Git workflow — **[FOR NON-DEVS]**

**Rule 1 — Never commit directly to `main`.** `main` is the production code. A bad commit there can break the live system for the client. Always work on a branch.

**Rule 2 — One branch per task, and always branch from an up-to-date `main`.** Before starting work:
```bash
git checkout main
git fetch origin
git status -sb                 # confirm "## main...origin/main" with no "behind"
git pull                       # only needed if behind; brings main up to date
git checkout -b feat/short-description
```
Branch name prefixes used here: `feat/` (new feature), `fix/` (bug fix), `test/` (tests only), `chore/` (tooling/config). Example: `feat/export-pdf`.

> **[FOR THE AGENT — mandatory]** Never create a parallel/feature branch from a stale `main`. Before `git checkout -b`, you MUST verify `main` is up to date with `origin/main`: run `git fetch origin` then `git rev-list --left-right --count main...origin/main` and confirm the right-hand number (commits behind) is `0`. If it is not `0`, update `main` first (`git pull --rebase origin main` or `git pull`) and re-check before branching. **Why:** branching from a stale `main` means developing against old code — the new branch misses recent merges, which causes avoidable merge conflicts and bugs at PR time, and can silently reintroduce code others already changed.

**Rule 3 — Commit in small, clear steps** using *Conventional Commits*. See **Conventional Commits** below for the full format. In short:
```
feat: add CSV export button to client page
fix: correct balance calculation when entry is zero
test: cover RegistroService auto-balance
```
Keep the subject short and in the imperative ("add", not "added"). Why: the history becomes a readable log of *why* each change happened.

**Rule 4 — Open a Pull Request (PR), don't merge blindly.**
```bash
git push -u origin feat/short-description
```
Then on GitHub: open a PR from your branch into `main`. A PR is a request for review *before* the code reaches production — it lets someone (or the agent) check the change first. Wait for tests to pass and for review before merging. Why this matters: it is the safety net that keeps broken code out of production.

**Before pushing**, always make sure tests pass locally (see Tests above). Don't push red tests and hope CI sorts it out.

---

## Code conventions

- **Comments: short and only when needed.** Comment *why*, not *what* the code already says. No long paragraphs. If a comment is needed to explain *what* a block does, prefer renaming/refactoring so the code is self-explanatory.
- **Backend:** nullable reference types are enabled — respect non-null contracts. Business logic goes in Services, not Controllers. Throw `ApiException` for expected error cases. Expose DTOs, never EF Models.
- **Frontend:** all network calls go through `src/api/`; never `fetch` directly from a component. TypeScript is strict — keep it compiling (`npm run build` runs `tsc -b`).
- **Match the surrounding code.** Naming, formatting, and patterns should look like the file you're editing. Domain names are in Portuguese (`Usuario`, `Registro`, `Conta`, `Meta`) — keep that vocabulary; the agent-facing docs are English.
- Never commit secrets. `appsettings.Development.json` and `.env` are git-ignored and must stay that way.

---

## Conventional Commits

Every commit message follows the *Conventional Commits* spec. This keeps history readable and lets tooling derive changelogs and version bumps automatically.

**Format:**

```text
<type>(<optional scope>): <short summary in the imperative>

<optional body — why the change, not what>

<optional footer — e.g. BREAKING CHANGE: ... or Closes #123>
```

**Rules:**

- `<type>` is required and lowercase. Allowed types:
  - `feat` — a new feature (user-facing capability).
  - `fix` — a bug fix.
  - `test` — adding or fixing tests only.
  - `refactor` — code change that neither fixes a bug nor adds a feature.
  - `perf` — a performance improvement.
  - `docs` — documentation only.
  - `style` — formatting/whitespace, no logic change.
  - `chore` — build, tooling, deps, config.
  - `ci` — CI/CD pipeline changes.
  - `build` — build system or external dependencies.
  - `revert` — reverts a previous commit.
- `<scope>` is optional and names the area touched, e.g. `feat(registros)`, `fix(auth)`, `test(backend)`. Use it when it adds clarity.
- The summary is **imperative, lowercase, no trailing period**, and ≤ ~72 chars ("add export", not "Added export.").
- A **breaking change** is flagged with `!` after the type/scope **and** a `BREAKING CHANGE:` footer: `feat(api)!: change response envelope`.
- Subject may be in English or Portuguese — match the existing history; the domain vocabulary stays Portuguese.

**Examples:**

```text
feat(registros): add CSV export button to client page
fix(auth): reject expired JWT before hitting the database
test(backend): cover RegistroService auto-balance
refactor: extract balance calculation into RegistroService
chore: bump Vite to 8.0.12
feat(api)!: change error envelope to include "campo"
```

---

## Testing standards

**Coverage is a signal, not a target.** The backend gate is **≥ 80%**, and a change must not drop coverage below it — but chasing the number is wrong. Prioritize business rules and risky paths over trivially-covered lines.

Both stacks follow **AAA** (Arrange, Act, Assert), **one behavior per test** (a single Act, a single reason to fail), and must be **deterministic** — no real clock, no real network, no dependence on test execution order.

### Frontend (React + TS + Vite)

- **Test behavior, not implementation.** Use **Vitest + React Testing Library**.
- Query by **`getByRole` / `getByLabelText`**; avoid `getByTestId` and CSS-class selectors.
- Use **`userEvent`** (async), not `fireEvent`.
- Mock the network with **MSW**, not by stubbing `fetch`/`axios`.
- Use async queries (**`findBy`**, **`waitFor`**) — never a manual `setTimeout`.
- Test custom hooks with **`renderHook`**.
- Build a typed **`renderWithProviders`** helper for Context / Store / Router.
- Use snapshots sparingly — only for small, stable output.
- Type your mocks and fixtures; avoid `as any`.
- Clean state between tests (`afterEach`; rely on RTL's automatic cleanup).
- Descriptive `describe`/`it` names — **"should ... when ..."**.

### Backend (.NET / C#)

- **Test the domain** (entities, value objects, aggregates) **without mocks**.
- **xUnit** with `[Theory]` / `[InlineData]` for parameterized cases.
- Correct `async`/`await` — return `Task`, never `async void`.
- Abstract time with **`TimeProvider` / `FakeTimeProvider`**.
- Mock **only the ports** (repositories, gateways), never the framework.
- Use **FluentAssertions** (`Should().BeEquivalentTo(...)`) for readability.
- Leverage xUnit isolation: constructor = setup, `IDisposable` = teardown.
- Prefer **in-memory fakes** over behavioral mocks when possible.
- Use **Test Data Builders** to assemble complex aggregates.
- Name tests **`Method_Scenario_ExpectedResult`**, grounded in the domain.
- Keep **unit tests separate from integration tests** (`WebApplicationFactory` / Testcontainers live apart).

---

## Other docs the agent must read

When new guidance documents are added to this repo, list them here so future sessions pick them up automatically:

- `README.md` — end-user / setup overview (Portuguese).
- `CaixaDiario.API/README.md` — backend setup, env vars, endpoints, admin-user seeding, deploy steps.
- `frontend/README.md` — frontend stack, commands, `src/` structure, and the dev vs. build flows.
- `docs/` — feature specs and implementation plans.
