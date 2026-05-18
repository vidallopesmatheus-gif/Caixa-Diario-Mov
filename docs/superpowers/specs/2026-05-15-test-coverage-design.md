# Design: Cobertura de Testes 80% — Backend e Frontend

**Data:** 2026-05-15  
**Projeto:** Caixa-Diario-Mov  
**Branch:** feature/frontend-react

---

## Contexto

O projeto possui testes de qualidade existentes (xUnit + Moq no backend; Vitest + React Testing Library no frontend), mas a cobertura está incompleta. Controllers, TokenService e middleware não têm testes no backend. Seis páginas de UI e os módulos de API não têm testes no frontend. A cobertura de código não está configurada em nenhuma das duas stacks.

**Meta:** cobertura mínima de 80% em linhas, funções, branches e statements, em ambas as stacks.

---

## Abordagem escolhida: Pragmática com Exclusões

Excluir arquivos sem lógica de negócio do cálculo de cobertura (DTOs, Models, Migrations no backend; `main.tsx`, `types.ts` no frontend). Escrever testes comportamentais para todas as camadas que possuem lógica real.

---

## Backend (.NET / xUnit)

### Refactor pré-testes

Os controllers recebem classes concretas de serviço, impedindo mock com Moq. Extrair três interfaces:

- `IAuthService` — `LoginAsync(LoginRequestDto) : Task<LoginResponseDto>`
- `IRegistroService` — `ListarPorClienteAsync`, `ObterPorDataAsync`, `SalvarAsync`, `ExcluirAsync`
- `IUsuarioService` — `ListarAsync`, `ObterPorIdAsync`, `CriarAsync`, `AtualizarAsync`, `DesativarAsync`

Cada serviço implementa sua interface. Controllers passam a depender da interface. `Program.cs` registra `services.AddScoped<IAuthService, AuthService>()` etc.

### Novos arquivos de teste

**`CaixaDiario.Tests/Controllers/AuthControllerTests.cs`**
- `Login_CredenciaisValidas_RetornaOkComToken` — mock `IAuthService.LoginAsync` retorna `LoginResponseDto`; verifica `OkObjectResult` com `ApiResponse<LoginResponseDto>.Dados.Token`

**`CaixaDiario.Tests/Controllers/RegistrosControllerTests.cs`**  
Setup: `DefaultHttpContext` com `ClaimsPrincipal` contendo claims `id`, `perfil`, `nome_usuario`.
- `Listar_RetornaOkComLista`
- `ObterPorData_RetornaOkComRegistro`
- `Salvar_RegistroNovo_RetornaCreated201`
- `Salvar_RegistroExistente_RetornaOk200`
- `Excluir_RetornaOk`

**`CaixaDiario.Tests/Controllers/UsuariosControllerTests.cs`**  
Setup: dois contextos — admin (perfil `"admin"`) e não-admin (perfil `"cliente"`).
- `Listar_Admin_RetornaOkComLista`
- `Listar_NaoAdmin_LancaSemPermissao`
- `ObterPorId_Admin_RetornaOk`
- `Criar_Admin_RetornaCreated201`
- `Atualizar_Admin_RetornaOk`
- `Desativar_Admin_RetornaOk`

**`CaixaDiario.Tests/Services/TokenServiceTests.cs`**  
Mock `IConfiguration` com valores JWT válidos (SecretKey 32+ chars, Issuer, Audience, ExpiresInHours).
- `GerarToken_IncluiClaimId`
- `GerarToken_IncluiClaimNomeUsuario`
- `GerarToken_IncluiClaimPerfil`
- `GerarToken_TokenEhValidoEDecodificavel`

**`CaixaDiario.Tests/Middleware/ErrorHandlingMiddlewareTests.cs`**  
Setup: `DefaultHttpContext` com `MemoryStream` no body; `RequestDelegate` mockado para lançar exceção.
- `InvokeAsync_ApiException_RetornaStatusECorpoCorretos`
- `InvokeAsync_ExcecaoGenerica_Retorna500`
- `InvokeAsync_SemExcecao_ChamaNext`

### Configuração de cobertura

**`.runsettings`** na raiz do projeto com exclusões:
```
[*]CaixaDiario.API.Migrations.*
[*]CaixaDiario.API.DTOs.*
[*]CaixaDiario.API.Models.*
[*]CaixaDiario.API.Data.*
[*]CaixaDiario.API.Responses.*
```

Comando de execução com threshold e relatório HTML:
```
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings
```

Adicionar `coverlet.msbuild` ao `CaixaDiario.Tests/CaixaDiario.Tests.csproj` para threshold enforcement (`Threshold=80`, `ThresholdType=line,branch,method`).

---

## Frontend (React / Vitest)

### Configuração de cobertura

Instalar `@vitest/coverage-v8` (provider nativo do Vitest, sem dependências extras).

Adicionar ao `vite.config.ts`:
```ts
coverage: {
  provider: 'v8',
  thresholds: { lines: 80, functions: 80, branches: 80, statements: 80 },
  exclude: ['src/main.tsx', 'src/types.ts', 'src/setupTests.ts', '**/*.css'],
  reporter: ['text', 'html'],
}
```

Novo script no `package.json`: `"test:coverage": "vitest run --coverage"`.

### Novos arquivos de teste — Páginas

Convenção: mockar hooks com `vi.mock('../../../hooks/useRegistros')` etc., retornando valores controlados via `vi.mocked(...).mockReturnValue(...)`.

**`AdminCaixaPage.test.tsx`**
- `exibe aviso quando clienteId ausente` — renderizar sem param; verificar texto "Selecione um cliente"
- `exibe aba Caixa por padrão`
- `troca para aba Histórico ao clicar no botão`

**`AdminClientsPage.test.tsx`**
- `renderiza lista de clientes do hook`
- `exibe "Novo cliente" ao clicar no botão de adicionar`
- `valida campos obrigatórios no formulário` — submeter sem preencher; verificar mensagem de erro
- `chama criar() ao submeter formulário válido`
- `abre modal de confirmação ao clicar Excluir`
- `chama desativar() ao confirmar exclusão`
- `exibe erro quando criar() falha`

**`AdminOverviewPage.test.tsx`**
- `renderiza StatCard com contagem de clientes ativos`
- `renderiza ClientCard para cada cliente`
- `navega para /admin/caixa/:id ao clicar "Ver caixa →"`

**`ClientCaixaPage.test.tsx`**
- `renderiza campos do formulário`
- `carrega dados do dia via buscarPorData`
- `exibe saldo calculado corretamente`
- `exibe mensagem de sucesso após salvar`
- `exibe mensagem de erro quando salvar falha`
- `navega para dia anterior/próximo via DayNav`

**`ClientGraficoPage.test.tsx`**
- `renderiza sem crash`
- `exibe loading enquanto carrega`
- `renderiza área do gráfico com dados mockados`

**`ClientHistoricoPage.test.tsx`**
- `renderiza lista de registros`
- `exibe saldo formatado em BRL`
- `exibe estado vazio quando não há registros`

### Novos arquivos de teste — Módulos de API

Convenção: `vi.stubGlobal('fetch', vi.fn())` com `mockResolvedValue({ ok: true, json: async () => dados, status: 200 })`.

**`api/client.test.ts`**
- `inclui header Authorization quando token existe no localStorage`
- `não inclui Authorization quando não há token`
- `lança Error com mensagem da API quando !res.ok`
- `limpa localStorage e redireciona quando status é 401`

**`api/registros.test.ts`**
- `listarRegistros faz GET em /api/registros/:clienteId`
- `obterRegistroPorData faz GET em /api/registros/:clienteId/:data`
- `salvarRegistro faz POST em /api/registros com body correto`
- `excluirRegistro faz DELETE com motivoExclusao no body`

**`api/auth.test.ts`**
- `login faz POST em /api/auth/login com nomeUsuario e senha`

---

## Critério de conclusão

- `dotnet test` passa com cobertura ≥ 80% em linha, branch e método (após exclusões)
- `npm run test:coverage` passa sem falhas de threshold
- Relatório HTML gerado em ambas as stacks para inspeção visual
- Nenhum teste usa `// eslint-disable` ou supressores de cobertura artificiais (`[ExcludeFromCodeCoverage]`)
