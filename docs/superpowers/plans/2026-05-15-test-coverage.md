# Test Coverage 80% Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Atingir cobertura mínima de 80% em backend (.NET/xUnit) e frontend (React/Vitest), configurando relatórios HTML e thresholds que bloqueiam o build.

**Architecture:** Backend — extrair interfaces de serviço para permitir mocking nos testes de controller; adicionar testes de controller (unit), TokenService e middleware. Frontend — instalar @vitest/coverage-v8, configurar thresholds no vite.config.ts, adicionar testes para páginas, API modules e hooks ausentes.

**Tech Stack:** .NET 10, xUnit, Moq, coverlet.msbuild; React 19, Vitest 4, @testing-library/react, @vitest/coverage-v8.

---

## File Structure

### Backend — criados/modificados

| Ação | Arquivo |
|---|---|
| Criar | `CaixaDiario.API/Services/IAuthService.cs` |
| Criar | `CaixaDiario.API/Services/IRegistroService.cs` |
| Criar | `CaixaDiario.API/Services/IUsuarioService.cs` |
| Modificar | `CaixaDiario.API/Services/AuthService.cs` — adicionar `: IAuthService` |
| Modificar | `CaixaDiario.API/Services/RegistroService.cs` — adicionar `: IRegistroService` |
| Modificar | `CaixaDiario.API/Services/UsuarioService.cs` — adicionar `: IUsuarioService` |
| Modificar | `CaixaDiario.API/Controllers/AuthController.cs` — usar `IAuthService` |
| Modificar | `CaixaDiario.API/Controllers/RegistrosController.cs` — usar `IRegistroService` |
| Modificar | `CaixaDiario.API/Controllers/UsuariosController.cs` — usar `IUsuarioService` |
| Modificar | `CaixaDiario.API/Program.cs` — registrar interfaces no DI |
| Modificar | `CaixaDiario.Tests/CaixaDiario.Tests.csproj` — coverlet.msbuild + thresholds |
| Criar | `CaixaDiario.Tests/Controllers/AuthControllerTests.cs` |
| Criar | `CaixaDiario.Tests/Controllers/RegistrosControllerTests.cs` |
| Criar | `CaixaDiario.Tests/Controllers/UsuariosControllerTests.cs` |
| Criar | `CaixaDiario.Tests/Services/TokenServiceTests.cs` |
| Criar | `CaixaDiario.Tests/Middleware/ErrorHandlingMiddlewareTests.cs` |

### Frontend — criados/modificados

| Ação | Arquivo |
|---|---|
| Modificar | `frontend/package.json` — adicionar `@vitest/coverage-v8` e script `test:coverage` |
| Modificar | `frontend/vite.config.ts` — bloco `coverage` com thresholds e excludes |
| Criar | `frontend/src/api/client.test.ts` |
| Criar | `frontend/src/api/auth.test.ts` |
| Criar | `frontend/src/api/registros.test.ts` |
| Criar | `frontend/src/pages/admin/AdminCaixaPage.test.tsx` |
| Criar | `frontend/src/pages/admin/AdminClientsPage.test.tsx` |
| Criar | `frontend/src/pages/admin/AdminOverviewPage.test.tsx` |
| Criar | `frontend/src/pages/client/ClientCaixaPage.test.tsx` |
| Criar | `frontend/src/pages/client/ClientGraficoPage.test.tsx` |
| Criar | `frontend/src/pages/client/ClientHistoricoPage.test.tsx` |

---

## Task 1: Extrair interfaces de serviço e atualizar DI

**Files:**

- Create: `CaixaDiario.API/Services/IAuthService.cs`
- Create: `CaixaDiario.API/Services/IRegistroService.cs`
- Create: `CaixaDiario.API/Services/IUsuarioService.cs`
- Modify: `CaixaDiario.API/Services/AuthService.cs`
- Modify: `CaixaDiario.API/Services/RegistroService.cs`
- Modify: `CaixaDiario.API/Services/UsuarioService.cs`
- Modify: `CaixaDiario.API/Controllers/AuthController.cs`
- Modify: `CaixaDiario.API/Controllers/RegistrosController.cs`
- Modify: `CaixaDiario.API/Controllers/UsuariosController.cs`
- Modify: `CaixaDiario.API/Program.cs`

- [ ] **Step 1: Criar IAuthService.cs**

```csharp
// CaixaDiario.API/Services/IAuthService.cs
using CaixaDiario.API.DTOs.Auth;

namespace CaixaDiario.API.Services;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto);
}
```

- [ ] **Step 2: Criar IRegistroService.cs**

```csharp
// CaixaDiario.API/Services/IRegistroService.cs
using CaixaDiario.API.DTOs.Registros;

namespace CaixaDiario.API.Services;

public interface IRegistroService
{
    Task<List<RegistroDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<RegistroDto> ObterPorDataAsync(Guid clienteId, DateOnly data, Guid usuarioLogadoId, string perfil);
    Task<(RegistroDto dto, bool criado)> SalvarAsync(CriarRegistroDto dto, string nomeUsuario);
    Task ExcluirAsync(Guid clienteId, DateOnly data, string motivo, Guid usuarioLogadoId, string perfil);
}
```

- [ ] **Step 3: Criar IUsuarioService.cs**

```csharp
// CaixaDiario.API/Services/IUsuarioService.cs
using CaixaDiario.API.DTOs.Usuarios;

namespace CaixaDiario.API.Services;

public interface IUsuarioService
{
    Task<List<UsuarioDto>> ListarAsync();
    Task<UsuarioDto> ObterPorIdAsync(Guid id);
    Task<UsuarioDto> CriarAsync(CriarUsuarioDto dto, string nomeUsuarioLogado);
    Task<UsuarioDto> AtualizarAsync(Guid id, AtualizarUsuarioDto dto, string nomeUsuarioLogado);
    Task DesativarAsync(Guid id, string nomeUsuarioLogado);
}
```

- [ ] **Step 4: Fazer serviços implementarem suas interfaces**

Em `AuthService.cs`, linha 1 da declaração de classe:
```csharp
// antes:
public class AuthService
// depois:
public class AuthService : IAuthService
```

Em `RegistroService.cs`:
```csharp
// antes:
public class RegistroService
// depois:
public class RegistroService : IRegistroService
```

Em `UsuarioService.cs`:
```csharp
// antes:
public class UsuarioService
// depois:
public class UsuarioService : IUsuarioService
```

- [ ] **Step 5: Atualizar AuthController para usar IAuthService**

```csharp
// CaixaDiario.API/Controllers/AuthController.cs — substituir inteiro
using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var resultado = await _authService.LoginAsync(dto);
        return Ok(new ApiResponse<LoginResponseDto> { Dados = resultado });
    }
}
```

- [ ] **Step 6: Atualizar RegistrosController para usar IRegistroService**

```csharp
// CaixaDiario.API/Controllers/RegistrosController.cs — substituir inteiro
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/registros")]
[Authorize]
public class RegistrosController : ControllerBase
{
    private readonly IRegistroService _registroService;

    public RegistrosController(IRegistroService registroService) => _registroService = registroService;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;
    private string ObterNomeUsuario() => User.FindFirst("nome_usuario")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var registros = await _registroService.ListarPorClienteAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<RegistroDto>> { Dados = registros });
    }

    [HttpGet("{clienteId:guid}/{data}")]
    public async Task<IActionResult> ObterPorData(Guid clienteId, DateOnly data)
    {
        var registro = await _registroService.ObterPorDataAsync(clienteId, data, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<RegistroDto> { Dados = registro });
    }

    [HttpPost]
    public async Task<IActionResult> Salvar([FromBody] CriarRegistroDto dto)
    {
        var (resultado, criado) = await _registroService.SalvarAsync(dto, ObterNomeUsuario());
        if (criado)
            return CreatedAtAction(nameof(ObterPorData), new { clienteId = resultado.ClienteId, data = resultado.Data },
                new ApiResponse<RegistroDto> { Dados = resultado });
        return Ok(new ApiResponse<RegistroDto> { Dados = resultado });
    }

    [HttpDelete("{clienteId:guid}/{data}")]
    public async Task<IActionResult> Excluir(Guid clienteId, DateOnly data, [FromBody] ExcluirRegistroDto dto)
    {
        await _registroService.ExcluirAsync(clienteId, data, dto.MotivoExclusao, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
```

- [ ] **Step 7: Atualizar UsuariosController para usar IUsuarioService**

```csharp
// CaixaDiario.API/Controllers/UsuariosController.cs — substituir inteiro
using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(IUsuarioService usuarioService) => _usuarioService = usuarioService;

    private void VerificarAdmin()
    {
        if (User.FindFirst("perfil")?.Value != "admin")
            throw new ApiException(403, CodigoRetorno.SEM_PERMISSAO, "Acesso restrito a administradores.");
    }

    private string ObterNomeUsuario() => User.FindFirst("nome_usuario")!.Value;

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        VerificarAdmin();
        var usuarios = await _usuarioService.ListarAsync();
        return Ok(new ApiResponse<List<UsuarioDto>> { Dados = usuarios });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        VerificarAdmin();
        var usuario = await _usuarioService.ObterPorIdAsync(id);
        return Ok(new ApiResponse<UsuarioDto> { Dados = usuario });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarUsuarioDto dto)
    {
        VerificarAdmin();
        var usuario = await _usuarioService.CriarAsync(dto, ObterNomeUsuario());
        return CreatedAtAction(nameof(ObterPorId), new { id = usuario.Id }, new ApiResponse<UsuarioDto> { Dados = usuario });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarUsuarioDto dto)
    {
        VerificarAdmin();
        var usuario = await _usuarioService.AtualizarAsync(id, dto, ObterNomeUsuario());
        return Ok(new ApiResponse<UsuarioDto> { Dados = usuario });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        VerificarAdmin();
        await _usuarioService.DesativarAsync(id, ObterNomeUsuario());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
```

- [ ] **Step 8: Atualizar registros de DI em Program.cs**

Localizar as três linhas abaixo em `CaixaDiario.API/Program.cs`:
```csharp
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<RegistroService>();
```

Substituir por:
```csharp
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IRegistroService, RegistroService>();
```

- [ ] **Step 9: Compilar e garantir que os testes existentes passam**

```
dotnet build CaixaDiario.sln
dotnet test CaixaDiario.Tests/CaixaDiario.Tests.csproj
```

Esperado: build verde, todos os testes existentes passando.

- [ ] **Step 10: Commit**

```bash
git add CaixaDiario.API/Services/IAuthService.cs \
        CaixaDiario.API/Services/IRegistroService.cs \
        CaixaDiario.API/Services/IUsuarioService.cs \
        CaixaDiario.API/Services/AuthService.cs \
        CaixaDiario.API/Services/RegistroService.cs \
        CaixaDiario.API/Services/UsuarioService.cs \
        CaixaDiario.API/Controllers/AuthController.cs \
        CaixaDiario.API/Controllers/RegistrosController.cs \
        CaixaDiario.API/Controllers/UsuariosController.cs \
        CaixaDiario.API/Program.cs
git commit -m "refactor: extrair interfaces de serviço para viabilizar testes de controller"
```

---

## Task 2: Configurar cobertura do backend (coverlet.msbuild)

**Files:**

- Modify: `CaixaDiario.Tests/CaixaDiario.Tests.csproj`

- [ ] **Step 1: Adicionar coverlet.msbuild e configurar PropertyGroup**

Substituir o conteúdo de `CaixaDiario.Tests/CaixaDiario.Tests.csproj` por:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <CollectCoverage>true</CollectCoverage>
    <CoverletOutputFormat>html</CoverletOutputFormat>
    <CoverletOutput>./TestResults/coverage/</CoverletOutput>
    <Threshold>80</Threshold>
    <ThresholdType>line,branch,method</ThresholdType>
    <Exclude>[*]CaixaDiario.API.Migrations.*%2C[*]CaixaDiario.API.DTOs.*%2C[*]CaixaDiario.API.Models.*%2C[*]CaixaDiario.API.Data.*%2C[*]CaixaDiario.API.Responses.*</Exclude>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.2.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CaixaDiario.API\CaixaDiario.API.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Restaurar pacotes**

```
dotnet restore CaixaDiario.Tests/CaixaDiario.Tests.csproj
```

Esperado: pacote `coverlet.msbuild` baixado sem erros.

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.Tests/CaixaDiario.Tests.csproj
git commit -m "chore: configurar coverlet.msbuild com threshold 80% e exclusões"
```

---

## Task 3: AuthControllerTests

**Files:**

- Create: `CaixaDiario.Tests/Controllers/AuthControllerTests.cs`

- [ ] **Step 1: Criar o arquivo de teste**

```csharp
// CaixaDiario.Tests/Controllers/AuthControllerTests.cs
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _serviceMock = new();
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _sut = new AuthController(_serviceMock.Object);
    }

    [Fact]
    public async Task Login_CredenciaisValidas_RetornaOkComToken()
    {
        var response = new LoginResponseDto
        {
            Token = "jwt-token",
            Nome = "João",
            Perfil = "cliente",
            Id = Guid.NewGuid()
        };
        _serviceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequestDto>())).ReturnsAsync(response);

        var result = await _sut.Login(new LoginRequestDto { NomeUsuario = "joao", Senha = "senha" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<LoginResponseDto>>(ok.Value);
        Assert.Equal("jwt-token", body.Dados!.Token);
        Assert.Equal("João", body.Dados.Nome);
    }

    [Fact]
    public async Task Login_PropagaExcecaoDoServico()
    {
        _serviceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequestDto>()))
            .ThrowsAsync(new CaixaDiario.API.Exceptions.ApiException(401,
                CaixaDiario.API.Enums.CodigoRetorno.CREDENCIAIS_INVALIDAS, "Credenciais inválidas"));

        await Assert.ThrowsAsync<CaixaDiario.API.Exceptions.ApiException>(
            () => _sut.Login(new LoginRequestDto { NomeUsuario = "x", Senha = "y" }));
    }
}
```

- [ ] **Step 2: Rodar e verificar que o teste passa**

```
dotnet test CaixaDiario.Tests/CaixaDiario.Tests.csproj --filter "FullyQualifiedName~AuthControllerTests"
```

Esperado: 2 testes passando.

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.Tests/Controllers/AuthControllerTests.cs
git commit -m "test: adicionar testes de AuthController"
```

---

## Task 4: RegistrosControllerTests

**Files:**

- Create: `CaixaDiario.Tests/Controllers/RegistrosControllerTests.cs`

- [ ] **Step 1: Criar o arquivo de teste**

```csharp
// CaixaDiario.Tests/Controllers/RegistrosControllerTests.cs
using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class RegistrosControllerTests
{
    private readonly Mock<IRegistroService> _serviceMock = new();
    private readonly RegistrosController _sut;
    private readonly Guid _usuarioId = Guid.NewGuid();

    public RegistrosControllerTests()
    {
        _sut = new RegistrosController(_serviceMock.Object);
        _sut.ControllerContext = CriarContexto(_usuarioId, "admin", "testuser");
    }

    private static ControllerContext CriarContexto(Guid usuarioId, string perfil, string nomeUsuario)
    {
        var claims = new[]
        {
            new Claim("id", usuarioId.ToString()),
            new Claim("perfil", perfil),
            new Claim("nome_usuario", nomeUsuario)
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }

    private static RegistroDto CriarRegistroDto(Guid clienteId) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = clienteId,
        Data = DateOnly.FromDateTime(DateTime.UtcNow),
        Inicio = 100m,
        Entrada = 200m,
        Saidas = new(),
        ContasReceber = new(),
        ContasPagar = new(),
        SaldoFinal = 300m,
        SalvoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task Listar_RetornaOkComLista()
    {
        var clienteId = Guid.NewGuid();
        var lista = new List<RegistroDto> { CriarRegistroDto(clienteId) };
        _serviceMock.Setup(s => s.ListarPorClienteAsync(clienteId, _usuarioId, "admin")).ReturnsAsync(lista);

        var result = await _sut.Listar(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<List<RegistroDto>>>(ok.Value);
        Assert.Single(body.Dados!);
    }

    [Fact]
    public async Task ObterPorData_RetornaOkComRegistro()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);
        var dto = CriarRegistroDto(clienteId);
        _serviceMock.Setup(s => s.ObterPorDataAsync(clienteId, data, _usuarioId, "admin")).ReturnsAsync(dto);

        var result = await _sut.ObterPorData(clienteId, data);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<RegistroDto>>(ok.Value);
        Assert.Equal(dto.Id, body.Dados!.Id);
    }

    [Fact]
    public async Task Salvar_RegistroNovo_RetornaCreated201()
    {
        var dto = CriarRegistroDto(Guid.NewGuid());
        var criarDto = new CriarRegistroDto { ClienteId = dto.ClienteId, Data = dto.Data };
        _serviceMock.Setup(s => s.SalvarAsync(criarDto, "testuser")).ReturnsAsync((dto, true));

        var result = await _sut.Salvar(criarDto);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task Salvar_RegistroExistente_RetornaOk200()
    {
        var dto = CriarRegistroDto(Guid.NewGuid());
        var criarDto = new CriarRegistroDto { ClienteId = dto.ClienteId, Data = dto.Data };
        _serviceMock.Setup(s => s.SalvarAsync(criarDto, "testuser")).ReturnsAsync((dto, false));

        var result = await _sut.Salvar(criarDto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Excluir_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);
        var excluirDto = new ExcluirRegistroDto { MotivoExclusao = "teste" };
        _serviceMock.Setup(s => s.ExcluirAsync(clienteId, data, "teste", _usuarioId, "admin")).Returns(Task.CompletedTask);

        var result = await _sut.Excluir(clienteId, data, excluirDto);

        Assert.IsType<OkObjectResult>(result);
    }
}
```

- [ ] **Step 2: Rodar e verificar**

```
dotnet test CaixaDiario.Tests/CaixaDiario.Tests.csproj --filter "FullyQualifiedName~RegistrosControllerTests"
```

Esperado: 5 testes passando.

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.Tests/Controllers/RegistrosControllerTests.cs
git commit -m "test: adicionar testes de RegistrosController"
```

---

## Task 5: UsuariosControllerTests

**Files:**

- Create: `CaixaDiario.Tests/Controllers/UsuariosControllerTests.cs`

- [ ] **Step 1: Criar o arquivo de teste**

```csharp
// CaixaDiario.Tests/Controllers/UsuariosControllerTests.cs
using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class UsuariosControllerTests
{
    private readonly Mock<IUsuarioService> _serviceMock = new();

    private UsuariosController CriarController(string perfil)
    {
        var claims = new[]
        {
            new Claim("perfil", perfil),
            new Claim("nome_usuario", "adminuser")
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        var controller = new UsuariosController(_serviceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    private static UsuarioDto CriarUsuarioDto() => new()
    {
        Id = Guid.NewGuid(),
        NomeUsuario = "cli1",
        Nome = "Cliente Um",
        Perfil = "cliente",
        Ativo = true,
        CriadoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task Listar_Admin_RetornaOkComLista()
    {
        var sut = CriarController("admin");
        var lista = new List<UsuarioDto> { CriarUsuarioDto() };
        _serviceMock.Setup(s => s.ListarAsync()).ReturnsAsync(lista);

        var result = await sut.Listar();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<List<UsuarioDto>>>(ok.Value);
        Assert.Single(body.Dados!);
    }

    [Fact]
    public async Task Listar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.Listar());

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }

    [Fact]
    public async Task ObterPorId_Admin_RetornaOk()
    {
        var sut = CriarController("admin");
        var dto = CriarUsuarioDto();
        _serviceMock.Setup(s => s.ObterPorIdAsync(dto.Id)).ReturnsAsync(dto);

        var result = await sut.ObterPorId(dto.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<UsuarioDto>>(ok.Value);
        Assert.Equal(dto.Id, body.Dados!.Id);
    }

    [Fact]
    public async Task Criar_Admin_RetornaCreated201()
    {
        var sut = CriarController("admin");
        var dto = CriarUsuarioDto();
        var criarDto = new CriarUsuarioDto { NomeUsuario = "cli1", Nome = "Cliente Um", Senha = "1234" };
        _serviceMock.Setup(s => s.CriarAsync(criarDto, "adminuser")).ReturnsAsync(dto);

        var result = await sut.Criar(criarDto);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task Atualizar_Admin_RetornaOk()
    {
        var sut = CriarController("admin");
        var id = Guid.NewGuid();
        var dto = CriarUsuarioDto();
        var atualizarDto = new AtualizarUsuarioDto { NomeUsuario = "cli1", Nome = "Cliente Um" };
        _serviceMock.Setup(s => s.AtualizarAsync(id, atualizarDto, "adminuser")).ReturnsAsync(dto);

        var result = await sut.Atualizar(id, atualizarDto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Desativar_Admin_RetornaOk()
    {
        var sut = CriarController("admin");
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.DesativarAsync(id, "adminuser")).Returns(Task.CompletedTask);

        var result = await sut.Desativar(id);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Desativar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.Desativar(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }
}
```

- [ ] **Step 2: Rodar e verificar**

```
dotnet test CaixaDiario.Tests/CaixaDiario.Tests.csproj --filter "FullyQualifiedName~UsuariosControllerTests"
```

Esperado: 7 testes passando.

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.Tests/Controllers/UsuariosControllerTests.cs
git commit -m "test: adicionar testes de UsuariosController"
```

---

## Task 6: TokenServiceTests

**Files:**

- Create: `CaixaDiario.Tests/Services/TokenServiceTests.cs`

- [ ] **Step 1: Criar o arquivo de teste**

```csharp
// CaixaDiario.Tests/Services/TokenServiceTests.cs
using System.IdentityModel.Tokens.Jwt;
using CaixaDiario.API.Models;
using CaixaDiario.API.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace CaixaDiario.Tests.Services;

public class TokenServiceTests
{
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Jwt:SecretKey"]).Returns("chave-super-secreta-testes-1234567890abcdef");
        configMock.Setup(c => c["Jwt:Issuer"]).Returns("CaixaDiario");
        configMock.Setup(c => c["Jwt:Audience"]).Returns("CaixaDiarioApp");
        configMock.Setup(c => c["Jwt:ExpiresInHours"]).Returns("24");
        _sut = new TokenService(configMock.Object);
    }

    private static Usuario CriarUsuario() => new()
    {
        Id = Guid.NewGuid(),
        NomeUsuario = "joao",
        SenhaHash = "hash",
        Nome = "João Silva",
        Perfil = "cliente",
        Ativo = true,
        CriadoEm = DateTime.UtcNow
    };

    [Fact]
    public void GerarToken_RetornaStringNaoVazia()
    {
        var token = _sut.GerarToken(CriarUsuario());
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GerarToken_IncluiClaimId()
    {
        var usuario = CriarUsuario();
        var token = _sut.GerarToken(usuario);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(usuario.Id.ToString(), jwt.Claims.First(c => c.Type == "id").Value);
    }

    [Fact]
    public void GerarToken_IncluiClaimNomeUsuario()
    {
        var usuario = CriarUsuario();
        var token = _sut.GerarToken(usuario);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("joao", jwt.Claims.First(c => c.Type == "nome_usuario").Value);
    }

    [Fact]
    public void GerarToken_IncluiClaimPerfil()
    {
        var usuario = CriarUsuario();
        var token = _sut.GerarToken(usuario);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("cliente", jwt.Claims.First(c => c.Type == "perfil").Value);
    }
}
```

- [ ] **Step 2: Rodar e verificar**

```
dotnet test CaixaDiario.Tests/CaixaDiario.Tests.csproj --filter "FullyQualifiedName~TokenServiceTests"
```

Esperado: 4 testes passando.

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.Tests/Services/TokenServiceTests.cs
git commit -m "test: adicionar testes de TokenService"
```

---

## Task 7: ErrorHandlingMiddlewareTests

**Files:**

- Create: `CaixaDiario.Tests/Middleware/ErrorHandlingMiddlewareTests.cs`

- [ ] **Step 1: Criar o arquivo de teste**

```csharp
// CaixaDiario.Tests/Middleware/ErrorHandlingMiddlewareTests.cs
using System.Text.Json;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Middleware;
using CaixaDiario.API.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace CaixaDiario.Tests.Middleware;

public class ErrorHandlingMiddlewareTests
{
    private static (ErrorHandlingMiddleware middleware, DefaultHttpContext context) Criar(RequestDelegate next)
    {
        var logger = new Mock<ILogger<ErrorHandlingMiddleware>>().Object;
        var middleware = new ErrorHandlingMiddleware(next, logger);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return (middleware, context);
    }

    [Fact]
    public async Task InvokeAsync_SemExcecao_ChamaNext()
    {
        bool nextChamado = false;
        var (middleware, context) = Criar(_ => { nextChamado = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.True(nextChamado);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ApiException_RetornaStatusCorreto()
    {
        var (middleware, context) = Criar(_ =>
            throw new ApiException(422, CodigoRetorno.DADOS_INVALIDOS, "Dados inválidos"));

        await middleware.InvokeAsync(context);

        Assert.Equal(422, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ApiException_RetornaJsonComCodigo()
    {
        var (middleware, context) = Criar(_ =>
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Erro teste"));

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var erro = JsonSerializer.Deserialize<ErroResponse>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("DADOS_INVALIDOS", erro!.Codigo);
        Assert.Equal("Erro teste", erro.Mensagem);
    }

    [Fact]
    public async Task InvokeAsync_ExcecaoGenerica_Retorna500()
    {
        var (middleware, context) = Criar(_ => throw new Exception("boom"));

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ApiExceptionComCampo_RetornaCampoNoJson()
    {
        var (middleware, context) = Criar(_ =>
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Campo inválido", "nome_usuario"));

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var erro = JsonSerializer.Deserialize<ErroResponse>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("nome_usuario", erro!.Campo);
    }
}
```

- [ ] **Step 2: Rodar e verificar**

```
dotnet test CaixaDiario.Tests/CaixaDiario.Tests.csproj --filter "FullyQualifiedName~ErrorHandlingMiddlewareTests"
```

Esperado: 5 testes passando.

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.Tests/Middleware/ErrorHandlingMiddlewareTests.cs
git commit -m "test: adicionar testes de ErrorHandlingMiddleware"
```

---

## Task 8: Verificar cobertura backend ≥ 80%

- [ ] **Step 1: Rodar todos os testes com cobertura**

```
dotnet test CaixaDiario.Tests/CaixaDiario.Tests.csproj
```

Esperado: todos os testes passam; cobertura ≥ 80% em line, branch e method (coverlet.msbuild reporta ao final). Se a cobertura falhar, o build retorna exit code diferente de zero com mensagem indicando qual métrica ficou abaixo.

- [ ] **Step 2: Abrir relatório HTML (opcional)**

Abrir `CaixaDiario.Tests/TestResults/coverage/index.html` no browser para ver quais linhas não estão cobertas.

- [ ] **Step 3: Se cobertura < 80%, identificar gaps e adicionar testes**

Inspecionar o relatório e adicionar casos de teste nos arquivos existentes para cobrir os branches ausentes. Repetir Step 1 até passar.

---

## Task 9: Configurar cobertura do frontend

**Files:**

- Modify: `frontend/package.json`
- Modify: `frontend/vite.config.ts`

- [ ] **Step 1: Instalar @vitest/coverage-v8**

```
cd frontend && npm install --save-dev @vitest/coverage-v8
```

Esperado: pacote adicionado em `devDependencies` do `package.json`.

- [ ] **Step 2: Atualizar vite.config.ts**

```typescript
// frontend/vite.config.ts — substituir inteiro
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/setupTests.ts',
    coverage: {
      provider: 'v8',
      thresholds: { lines: 80, functions: 80, branches: 80, statements: 80 },
      exclude: [
        'src/main.tsx',
        'src/types.ts',
        'src/setupTests.ts',
        '**/*.css',
        'src/styles/**',
      ],
      reporter: ['text', 'html'],
    },
  },
  build: {
    outDir: '../CaixaDiario.API/wwwroot',
    emptyOutDir: true,
  },
})
```

- [ ] **Step 3: Adicionar script test:coverage ao package.json**

No bloco `"scripts"` de `frontend/package.json`, adicionar:
```json
"test:coverage": "vitest run --coverage"
```

- [ ] **Step 4: Verificar que os testes existentes ainda passam**

```
cd frontend && npm test -- --run
```

Esperado: todos os testes passam.

- [ ] **Step 5: Commit**

```bash
git add frontend/package.json frontend/vite.config.ts frontend/package-lock.json
git commit -m "chore: configurar @vitest/coverage-v8 com threshold 80%"
```

---

## Task 10: api/client.test.ts

**Files:**

- Create: `frontend/src/api/client.test.ts`

- [ ] **Step 1: Criar o arquivo de teste**

```typescript
// frontend/src/api/client.test.ts
import { apiFetch } from './client'

const originalLocation = window.location

beforeEach(() => {
  localStorage.clear()
  vi.stubGlobal('fetch', vi.fn())
  Object.defineProperty(window, 'location', { writable: true, value: { href: '' } })
})

afterEach(() => {
  vi.unstubAllGlobals()
  Object.defineProperty(window, 'location', { writable: true, value: originalLocation })
})

function mockFetch(status: number, body: unknown) {
  vi.mocked(fetch).mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as Response)
}

test('inclui Content-Type application/json em todas as requisições', async () => {
  mockFetch(200, { dados: null })
  await apiFetch('/api/test')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(
    '/api/test',
    expect.objectContaining({
      headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
    })
  )
})

test('inclui Authorization Bearer quando token existe no localStorage', async () => {
  localStorage.setItem('token', 'meu-token')
  mockFetch(200, { dados: null })
  await apiFetch('/api/test')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(
    '/api/test',
    expect.objectContaining({
      headers: expect.objectContaining({ Authorization: 'Bearer meu-token' }),
    })
  )
})

test('não inclui Authorization quando localStorage não tem token', async () => {
  mockFetch(200, { dados: null })
  await apiFetch('/api/test')
  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  expect((call.headers as Record<string, string>).Authorization).toBeUndefined()
})

test('lança Error com mensagem da API quando !res.ok', async () => {
  mockFetch(400, { mensagem: 'Dados inválidos' })
  await expect(apiFetch('/api/test')).rejects.toThrow('Dados inválidos')
})

test('lança Error com "Erro 500" quando body não tem mensagem', async () => {
  mockFetch(500, {})
  await expect(apiFetch('/api/test')).rejects.toThrow('Erro 500')
})

test('limpa localStorage e redireciona para /login quando status 401', async () => {
  localStorage.setItem('token', 'tok')
  localStorage.setItem('user', '{}')
  mockFetch(401, { mensagem: 'Não autorizado' })
  await expect(apiFetch('/api/test')).rejects.toThrow('Sessão expirada')
  expect(localStorage.getItem('token')).toBeNull()
  expect(localStorage.getItem('user')).toBeNull()
  expect(window.location.href).toBe('/login')
})

test('retorna JSON parseado quando resposta ok', async () => {
  mockFetch(200, { dados: [1, 2, 3] })
  const result = await apiFetch<{ dados: number[] }>('/api/test')
  expect(result.dados).toEqual([1, 2, 3])
})
```

- [ ] **Step 2: Rodar e verificar**

```
cd frontend && npm test -- --run --reporter=verbose src/api/client.test.ts
```

Esperado: 7 testes passando.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/api/client.test.ts
git commit -m "test: adicionar testes de apiFetch (api/client)"
```

---

## Task 11: api/auth.test.ts e api/registros.test.ts

**Files:**

- Create: `frontend/src/api/auth.test.ts`
- Create: `frontend/src/api/registros.test.ts`

- [ ] **Step 1: Criar auth.test.ts**

```typescript
// frontend/src/api/auth.test.ts
import { login } from './auth'

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn())
  vi.mocked(fetch).mockResolvedValue({
    ok: true,
    status: 200,
    json: async () => ({ dados: { token: 'jwt', nome: 'CTI', perfil: 'admin', id: '1' } }),
  } as Response)
})

afterEach(() => vi.unstubAllGlobals())

test('login faz POST em /api/auth/login', async () => {
  await login('CTI', 'senha123')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(
    '/api/auth/login',
    expect.objectContaining({ method: 'POST' })
  )
})

test('login envia nomeUsuario e senha no body JSON', async () => {
  await login('CTI', 'senha123')
  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  expect(JSON.parse(call.body as string)).toEqual({ nomeUsuario: 'CTI', senha: 'senha123' })
})
```

- [ ] **Step 2: Criar registros.test.ts**

```typescript
// frontend/src/api/registros.test.ts
import { listarRegistros, obterRegistroPorData, salvarRegistro, excluirRegistro } from './registros'

const mockOk = (body: unknown) =>
  vi.mocked(fetch).mockResolvedValue({
    ok: true, status: 200, json: async () => body,
  } as Response)

beforeEach(() => vi.stubGlobal('fetch', vi.fn()))
afterEach(() => vi.unstubAllGlobals())

test('listarRegistros faz GET em /api/registros/:clienteId', async () => {
  mockOk({ dados: [] })
  await listarRegistros('abc-123')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/registros/abc-123', expect.any(Object))
})

test('obterRegistroPorData faz GET em /api/registros/:clienteId/:data', async () => {
  mockOk({ dados: null })
  await obterRegistroPorData('abc-123', '2026-05-15')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/registros/abc-123/2026-05-15', expect.any(Object))
})

test('salvarRegistro faz POST em /api/registros', async () => {
  mockOk({ dados: {} })
  await salvarRegistro({
    clienteId: 'c1', data: '2026-05-15', saldoInicio: 0,
    entrada: 0, saidas: [], contasAReceber: [], contasAPagar: [], saldoConfirmado: 0,
  })
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(
    '/api/registros',
    expect.objectContaining({ method: 'POST' })
  )
})

test('excluirRegistro faz DELETE com motivoExclusao no body', async () => {
  mockOk({ dados: null })
  await excluirRegistro('c1', '2026-05-15', 'erro de digitação')
  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  expect(call.method).toBe('DELETE')
  expect(JSON.parse(call.body as string)).toEqual({ motivoExclusao: 'erro de digitação' })
})
```

- [ ] **Step 3: Rodar e verificar**

```
cd frontend && npm test -- --run --reporter=verbose src/api/auth.test.ts src/api/registros.test.ts
```

Esperado: 6 testes passando.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/api/auth.test.ts frontend/src/api/registros.test.ts
git commit -m "test: adicionar testes de módulos de API (auth e registros)"
```

---

## Task 12: AdminCaixaPage.test.tsx

**Files:**

- Create: `frontend/src/pages/admin/AdminCaixaPage.test.tsx`

- [ ] **Step 1: Criar o arquivo de teste**

```typescript
// frontend/src/pages/admin/AdminCaixaPage.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import AdminCaixaPage from './AdminCaixaPage'

vi.mock('../client/ClientCaixaPage', () => ({ default: () => <div>ClientCaixaPage</div> }))
vi.mock('../client/ClientHistoricoPage', () => ({ default: () => <div>ClientHistoricoPage</div> }))

function renderWithParam(clienteId?: string) {
  const path = clienteId ? `/admin/caixa/${clienteId}` : '/admin/caixa'
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/admin/caixa/:clienteId" element={<AdminCaixaPage />} />
        <Route path="/admin/caixa" element={<AdminCaixaPage />} />
      </Routes>
    </MemoryRouter>
  )
}

test('exibe aviso quando clienteId está ausente', () => {
  renderWithParam()
  expect(screen.getByText(/Selecione um cliente/)).toBeInTheDocument()
})

test('exibe ClientCaixaPage por padrão quando clienteId presente', () => {
  renderWithParam('id-cliente-123')
  expect(screen.getByText('ClientCaixaPage')).toBeInTheDocument()
})

test('botão Caixa tem classe btn-confirm por padrão', () => {
  renderWithParam('id-cliente-123')
  const btnCaixa = screen.getByText(/Caixa/)
  expect(btnCaixa.className).toContain('btn-confirm')
})

test('troca para ClientHistoricoPage ao clicar em Histórico', () => {
  renderWithParam('id-cliente-123')
  fireEvent.click(screen.getByText(/Histórico/))
  expect(screen.getByText('ClientHistoricoPage')).toBeInTheDocument()
})

test('botão Histórico fica ativo após clique', () => {
  renderWithParam('id-cliente-123')
  fireEvent.click(screen.getByText(/Histórico/))
  const btnHist = screen.getByText(/Histórico/)
  expect(btnHist.className).toContain('btn-confirm')
})
```

- [ ] **Step 2: Rodar e verificar**

```
cd frontend && npm test -- --run --reporter=verbose src/pages/admin/AdminCaixaPage.test.tsx
```

Esperado: 5 testes passando.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/admin/AdminCaixaPage.test.tsx
git commit -m "test: adicionar testes de AdminCaixaPage"
```

---

## Task 13: AdminClientsPage.test.tsx

**Files:**

- Create: `frontend/src/pages/admin/AdminClientsPage.test.tsx`

- [ ] **Step 1: Criar o arquivo de teste**

```typescript
// frontend/src/pages/admin/AdminClientsPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import AdminClientsPage from './AdminClientsPage'
import * as useUsuariosHook from '../../hooks/useUsuarios'
import type { Usuario } from '../../types'

vi.mock('../../hooks/useUsuarios')

const mockClientes: Usuario[] = [
  { id: 'u1', nomeUsuario: 'cli1', nomeCompleto: 'Cliente Um', nomeEstabelecimento: 'Loja Um', perfil: 'cliente', ativo: true, criadoEm: '' },
  { id: 'u2', nomeUsuario: 'cli2', nomeCompleto: 'Cliente Dois', nomeEstabelecimento: 'Loja Dois', perfil: 'cliente', ativo: true, criadoEm: '' },
]

function mockHook(overrides: Partial<ReturnType<typeof useUsuariosHook.useUsuarios>> = {}) {
  vi.mocked(useUsuariosHook.useUsuarios).mockReturnValue({
    usuarios: mockClientes,
    loading: false,
    erro: '',
    criar: vi.fn().mockResolvedValue({}),
    atualizar: vi.fn().mockResolvedValue({}),
    desativar: vi.fn().mockResolvedValue(undefined),
    recarregar: vi.fn(),
    ...overrides,
  } as ReturnType<typeof useUsuariosHook.useUsuarios>)
}

test('exibe loading enquanto carrega', () => {
  mockHook({ loading: true, usuarios: [] })
  render(<AdminClientsPage />)
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza lista de clientes filtrados por perfil cliente', () => {
  mockHook()
  render(<AdminClientsPage />)
  expect(screen.getByText('Cliente Um')).toBeInTheDocument()
  expect(screen.getByText('Cliente Dois')).toBeInTheDocument()
})

test('exibe contagem de clientes no painel', () => {
  mockHook()
  render(<AdminClientsPage />)
  expect(screen.getByText(/2 clientes/)).toBeInTheDocument()
})

test('abre modal ao clicar em Novo cliente', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText(/Novo cliente/))
  expect(screen.getByText('Novo cliente', { selector: 'h2, h3, div' })).toBeInTheDocument()
})

test('exibe mensagem de erro ao submeter modal vazio', async () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText(/Novo cliente/))
  fireEvent.click(screen.getByText('Salvar'))
  await waitFor(() =>
    expect(screen.getByText(/Preencha todos os campos/)).toBeInTheDocument()
  )
})

test('exibe painel de edição ao clicar em cliente', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  expect(screen.getByText('Excluir')).toBeInTheDocument()
})

test('abre modal de confirmação ao clicar em Excluir', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  fireEvent.click(screen.getByText('Excluir'))
  expect(screen.getByText(/Confirmar exclusão/)).toBeInTheDocument()
})

test('chama desativar ao confirmar exclusão', async () => {
  const desativar = vi.fn().mockResolvedValue(undefined)
  mockHook({ desativar })
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  fireEvent.click(screen.getByText('Excluir'))
  const botoesExcluir = screen.getAllByText('Excluir')
  fireEvent.click(botoesExcluir[botoesExcluir.length - 1])
  await waitFor(() => expect(desativar).toHaveBeenCalledWith('u1'))
})

test('exibe erro quando criar falha', async () => {
  const criar = vi.fn().mockRejectedValue(new Error('Usuário já existe'))
  mockHook({ criar })
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText(/Novo cliente/))
  const inputs = screen.getAllByRole('textbox')
  fireEvent.change(inputs[0], { target: { value: 'Novo Cliente' } })
  fireEvent.change(inputs[2], { target: { value: 'novouser' } })
  const senhaInput = document.querySelector('input[type="password"]')!
  fireEvent.change(senhaInput, { target: { value: '1234' } })
  fireEvent.click(screen.getByText('Salvar'))
  await waitFor(() =>
    expect(screen.getByText('Usuário já existe')).toBeInTheDocument()
  )
})
```

- [ ] **Step 2: Rodar e verificar**

```
cd frontend && npm test -- --run --reporter=verbose src/pages/admin/AdminClientsPage.test.tsx
```

Esperado: 9 testes passando. Se algum falhar por seletor, ajustar o seletor de acordo com o que o componente renderiza (verificar com `screen.debug()`).

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/admin/AdminClientsPage.test.tsx
git commit -m "test: adicionar testes de AdminClientsPage"
```

---

## Task 14: AdminOverviewPage.test.tsx

**Files:**

- Create: `frontend/src/pages/admin/AdminOverviewPage.test.tsx`

- [ ] **Step 1: Criar o arquivo de teste**

```typescript
// frontend/src/pages/admin/AdminOverviewPage.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import AdminOverviewPage from './AdminOverviewPage'
import * as useUsuariosHook from '../../hooks/useUsuarios'
import * as useRegistrosHook from '../../hooks/useRegistros'
import type { Usuario } from '../../types'

vi.mock('../../hooks/useUsuarios')
vi.mock('../../hooks/useRegistros')

const mockClientes: Usuario[] = [
  { id: 'u1', nomeUsuario: 'cli1', nomeCompleto: 'Cliente Um', nomeEstabelecimento: 'Loja Um', perfil: 'cliente', ativo: true, criadoEm: '' },
  { id: 'u2', nomeUsuario: 'cli2', nomeCompleto: 'Cliente Dois', nomeEstabelecimento: 'Loja Dois', perfil: 'cliente', ativo: true, criadoEm: '' },
]

function mockHooks(usuariosOverride: Partial<ReturnType<typeof useUsuariosHook.useUsuarios>> = {}) {
  vi.mocked(useUsuariosHook.useUsuarios).mockReturnValue({
    usuarios: mockClientes,
    loading: false,
    erro: '',
    criar: vi.fn(),
    atualizar: vi.fn(),
    desativar: vi.fn(),
    recarregar: vi.fn(),
    ...usuariosOverride,
  } as ReturnType<typeof useUsuariosHook.useUsuarios>)

  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros: [],
    loading: false,
    erro: '',
    salvar: vi.fn(),
    excluir: vi.fn(),
    buscarPorData: vi.fn(),
    recarregar: vi.fn(),
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
}

function renderPage() {
  return render(<MemoryRouter><AdminOverviewPage /></MemoryRouter>)
}

test('exibe loading enquanto carrega', () => {
  mockHooks({ loading: true, usuarios: [] })
  renderPage()
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza StatCard com contagem de clientes ativos', () => {
  mockHooks()
  renderPage()
  expect(screen.getByText('2')).toBeInTheDocument()
})

test('renderiza um card para cada cliente ativo', () => {
  mockHooks()
  renderPage()
  expect(screen.getByText('Cliente Um')).toBeInTheDocument()
  expect(screen.getByText('Cliente Dois')).toBeInTheDocument()
})

test('exibe nome do estabelecimento em cada card', () => {
  mockHooks()
  renderPage()
  expect(screen.getByText('Loja Um')).toBeInTheDocument()
  expect(screen.getByText('Loja Dois')).toBeInTheDocument()
})

test('não renderiza clientes inativos', () => {
  const comInativo = [
    ...mockClientes,
    { id: 'u3', nomeUsuario: 'inat', nomeCompleto: 'Inativo', nomeEstabelecimento: '', perfil: 'cliente', ativo: false, criadoEm: '' },
  ]
  mockHooks({ usuarios: comInativo })
  renderPage()
  expect(screen.queryByText('Inativo')).not.toBeInTheDocument()
})
```

- [ ] **Step 2: Rodar e verificar**

```
cd frontend && npm test -- --run --reporter=verbose src/pages/admin/AdminOverviewPage.test.tsx
```

Esperado: 5 testes passando.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/admin/AdminOverviewPage.test.tsx
git commit -m "test: adicionar testes de AdminOverviewPage"
```

---

## Task 15: ClientCaixaPage.test.tsx

**Files:**

- Create: `frontend/src/pages/client/ClientCaixaPage.test.tsx`

- [ ] **Step 1: Criar o arquivo de teste**

```typescript
// frontend/src/pages/client/ClientCaixaPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import ClientCaixaPage from './ClientCaixaPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente', nomeCompleto: 'Cliente Um', nomeEstabelecimento: '', token: 'tok' }

function mockHooks(overrides: Partial<ReturnType<typeof useRegistrosHook.useRegistros>> = {}) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({
    user: mockUser,
    login: vi.fn(),
    logout: vi.fn(),
  })
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros: [],
    loading: false,
    erro: '',
    salvar: vi.fn().mockResolvedValue({}),
    excluir: vi.fn(),
    buscarPorData: vi.fn().mockResolvedValue(null),
    recarregar: vi.fn(),
    ...overrides,
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
}

test('renderiza campos do formulário de caixa', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.getByPlaceholderText('0,00')).toBeInTheDocument()
  expect(screen.getByText(/Salvar e sincronizar/)).toBeInTheDocument()
})

test('exibe StatCards com labels corretos', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.getByText(/Início/)).toBeInTheDocument()
  expect(screen.getByText(/Entrada/)).toBeInTheDocument()
  expect(screen.getByText(/Saídas/)).toBeInTheDocument()
  expect(screen.getByText(/Saldo/)).toBeInTheDocument()
})

test('exibe mensagem de sucesso após salvar', async () => {
  const salvar = vi.fn().mockResolvedValue({})
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText(/Salvo com sucesso/)).toBeInTheDocument())
})

test('exibe mensagem de erro quando salvar falha', async () => {
  const salvar = vi.fn().mockRejectedValue(new Error('Falha de rede'))
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText(/Falha de rede/)).toBeInTheDocument())
})

test('botão fica desabilitado durante salvamento', async () => {
  const salvar = vi.fn().mockImplementation(() => new Promise(() => {}))
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText('Salvando...')).toBeDisabled())
})

test('adiciona nova linha de saída ao clicar em Adicionar saída', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  const antes = screen.getAllByPlaceholderText('Descrição').length
  fireEvent.click(screen.getByText(/Adicionar saída/))
  expect(screen.getAllByPlaceholderText('Descrição')).toHaveLength(antes + 1)
})

test('botões de navegação de dia estão presentes', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.getByText('‹')).toBeInTheDocument()
  expect(screen.getByText('›')).toBeInTheDocument()
})
```

- [ ] **Step 2: Rodar e verificar**

```
cd frontend && npm test -- --run --reporter=verbose src/pages/client/ClientCaixaPage.test.tsx
```

Esperado: 7 testes passando. Se algum falhar por texto exato (ex: `‹`/`›` nos botões de DayNav), inspecionar com `screen.debug()` e ajustar o seletor.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/client/ClientCaixaPage.test.tsx
git commit -m "test: adicionar testes de ClientCaixaPage"
```

---

## Task 16: ClientGraficoPage.test.tsx e ClientHistoricoPage.test.tsx

**Files:**

- Create: `frontend/src/pages/client/ClientGraficoPage.test.tsx`
- Create: `frontend/src/pages/client/ClientHistoricoPage.test.tsx`

- [ ] **Step 1: Criar ClientGraficoPage.test.tsx**

```typescript
// frontend/src/pages/client/ClientGraficoPage.test.tsx
import { render, screen } from '@testing-library/react'
import ClientGraficoPage from './ClientGraficoPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'
import type { Registro } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')

// Recharts usa ResizeObserver; precisamos de um stub
global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
}

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente', nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

const mesAtual = new Date().toISOString().slice(0, 7)
const mockRegistros: Registro[] = [
  {
    id: 'r1', clienteId: 'u1', data: `${mesAtual}-01`,
    saldoInicio: 1000, entrada: 500,
    saidas: [{ descricao: 'Aluguel', valor: 200 }],
    contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 1300, saldoCalculado: 1300, criadoEm: '',
  },
]

function mockHooks(registros = mockRegistros, loading = false) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros,
    loading,
    erro: '',
    salvar: vi.fn(),
    excluir: vi.fn(),
    buscarPorData: vi.fn(),
    recarregar: vi.fn(),
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
}

test('exibe loading enquanto carrega', () => {
  mockHooks([], true)
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza título da seção após carregar', () => {
  mockHooks()
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Evolução Mensal/)).toBeInTheDocument()
})

test('renderiza StatCards com totais do mês', () => {
  mockHooks()
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Total Entradas/)).toBeInTheDocument()
  expect(screen.getByText(/Total Saídas/)).toBeInTheDocument()
})

test('renderiza sem crash quando não há registros', () => {
  mockHooks([])
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Evolução Mensal/)).toBeInTheDocument()
})
```

- [ ] **Step 2: Criar ClientHistoricoPage.test.tsx**

```typescript
// frontend/src/pages/client/ClientHistoricoPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import ClientHistoricoPage from './ClientHistoricoPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'
import type { Registro } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente', nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

const mesAtual = new Date().toISOString().slice(0, 7)
const mockRegistros: Registro[] = [
  {
    id: 'r1', clienteId: 'u1', data: `${mesAtual}-10`,
    saldoInicio: 100, entrada: 500,
    saidas: [{ descricao: 'Aluguel', valor: 200 }],
    contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 400, saldoCalculado: 400, criadoEm: '',
  },
]

function mockHooks(overrides: Partial<ReturnType<typeof useRegistrosHook.useRegistros>> = {}) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros: mockRegistros,
    loading: false,
    erro: '',
    salvar: vi.fn(),
    excluir: vi.fn().mockResolvedValue(undefined),
    buscarPorData: vi.fn(),
    recarregar: vi.fn(),
    ...overrides,
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
}

test('exibe loading enquanto carrega', () => {
  mockHooks({ loading: true, registros: [] })
  render(<ClientHistoricoPage />)
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza StatCards com totais do mês', () => {
  mockHooks()
  render(<ClientHistoricoPage />)
  expect(screen.getByText(/Total entradas/)).toBeInTheDocument()
  expect(screen.getByText(/Total saídas/)).toBeInTheDocument()
})

test('renderiza item de histórico com data e saldo', () => {
  mockHooks()
  render(<ClientHistoricoPage />)
  expect(screen.getByText(/R\$/)).toBeInTheDocument()
})

test('expande detalhes ao clicar no item de histórico', async () => {
  mockHooks()
  render(<ClientHistoricoPage />)
  const items = screen.getAllByText(/Entrada:/)
  fireEvent.click(items[0].closest('div')!.parentElement!)
  await waitFor(() =>
    expect(screen.getByText('Aluguel')).toBeInTheDocument()
  )
})

test('abre modal de exclusão ao clicar em Excluir registro', async () => {
  mockHooks()
  render(<ClientHistoricoPage />)
  const items = screen.getAllByText(/Entrada:/)
  fireEvent.click(items[0].closest('div')!.parentElement!)
  await waitFor(() => screen.getByText(/Excluir registro/))
  fireEvent.click(screen.getByText(/Excluir registro/))
  expect(screen.getByText(/Motivo/)).toBeInTheDocument()
})

test('chama excluir com motivo ao confirmar', async () => {
  const excluir = vi.fn().mockResolvedValue(undefined)
  mockHooks({ excluir })
  render(<ClientHistoricoPage />)
  const items = screen.getAllByText(/Entrada:/)
  fireEvent.click(items[0].closest('div')!.parentElement!)
  await waitFor(() => screen.getByText(/Excluir registro/))
  fireEvent.click(screen.getByText(/Excluir registro/))
  fireEvent.change(screen.getByPlaceholderText(/Informe o motivo/), { target: { value: 'teste' } })
  fireEvent.click(screen.getByText('Excluir'))
  await waitFor(() => expect(excluir).toHaveBeenCalledWith(`${mesAtual}-10`, 'teste'))
})
```

- [ ] **Step 3: Rodar e verificar**

```
cd frontend && npm test -- --run --reporter=verbose src/pages/client/ClientGraficoPage.test.tsx src/pages/client/ClientHistoricoPage.test.tsx
```

Esperado: 10 testes passando. Se algum falhar por seletor DOM, usar `screen.debug()` e ajustar.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/pages/client/ClientGraficoPage.test.tsx \
        frontend/src/pages/client/ClientHistoricoPage.test.tsx
git commit -m "test: adicionar testes de ClientGraficoPage e ClientHistoricoPage"
```

---

## Task 17: Verificar cobertura frontend ≥ 80%

- [ ] **Step 1: Rodar suite completa com cobertura**

```
cd frontend && npm run test:coverage
```

Esperado: todos os testes passam; output mostra cobertura ≥ 80% em lines, functions, branches e statements. Se a cobertura falhar, Vitest imprime um erro similar a:

```
ERROR: Coverage for lines (72%) does not meet global threshold (80%)
```

- [ ] **Step 2: Abrir relatório HTML (opcional)**

Abrir `frontend/coverage/index.html` no browser. Identificar arquivos com baixa cobertura e adicionar casos de teste nos arquivos de teste correspondentes.

- [ ] **Step 3: Se cobertura < 80%, adicionar testes nos gaps**

Inspecionar o relatório, identificar branches não cobertos, e adicionar casos de teste específicos nos arquivos de teste já criados. Repetir Step 1 até passar.

- [ ] **Step 4: Commit final**

```bash
git add -A
git commit -m "test: garantir cobertura >= 80% em frontend e backend"
```
