# Backend C# — Caixa Diário — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar um backend ASP.NET Core C# com PostgreSQL (Supabase) para substituir o GitHub Gist, resolver os problemas de segurança e permitir publicação do Caixa Diário para 3 clientes.

**Architecture:** Arquitetura em Camadas com Repository Pattern. Fluxo: `Controller → Service → Repository → DbContext (EF Core) → PostgreSQL`. Cada camada conhece apenas a imediatamente abaixo. Services são testados via mock dos Repositories.

**Tech Stack:** .NET 8, ASP.NET Core Web API, Entity Framework Core 8, Npgsql, BCrypt.Net-Next, System.IdentityModel.Tokens.Jwt, xUnit, Moq.

---

## Mapa de Arquivos

| Arquivo | Criar / Modificar |
|---|---|
| `CaixaDiario.API/Models/Usuario.cs` | Criar |
| `CaixaDiario.API/Models/RegistroDiario.cs` | Criar |
| `CaixaDiario.API/Models/ItemFinanceiro.cs` | Criar |
| `CaixaDiario.API/Enums/CodigoRetorno.cs` | Criar |
| `CaixaDiario.API/Exceptions/ApiException.cs` | Criar |
| `CaixaDiario.API/Responses/ApiResponse.cs` | Criar |
| `CaixaDiario.API/Responses/ErroResponse.cs` | Criar |
| `CaixaDiario.API/DTOs/Auth/LoginRequestDto.cs` | Criar |
| `CaixaDiario.API/DTOs/Auth/LoginResponseDto.cs` | Criar |
| `CaixaDiario.API/DTOs/Usuarios/UsuarioDto.cs` | Criar |
| `CaixaDiario.API/DTOs/Usuarios/CriarUsuarioDto.cs` | Criar |
| `CaixaDiario.API/DTOs/Usuarios/AtualizarUsuarioDto.cs` | Criar |
| `CaixaDiario.API/DTOs/Registros/ItemFinanceiroDto.cs` | Criar |
| `CaixaDiario.API/DTOs/Registros/RegistroDto.cs` | Criar |
| `CaixaDiario.API/DTOs/Registros/CriarRegistroDto.cs` | Criar |
| `CaixaDiario.API/DTOs/Registros/ExcluirRegistroDto.cs` | Criar |
| `CaixaDiario.API/Data/AppDbContext.cs` | Criar |
| `CaixaDiario.API/Repositories/Interfaces/IUsuarioRepository.cs` | Criar |
| `CaixaDiario.API/Repositories/Interfaces/IRegistroRepository.cs` | Criar |
| `CaixaDiario.API/Repositories/UsuarioRepository.cs` | Criar |
| `CaixaDiario.API/Repositories/RegistroRepository.cs` | Criar |
| `CaixaDiario.API/Services/ITokenService.cs` | Criar |
| `CaixaDiario.API/Services/TokenService.cs` | Criar |
| `CaixaDiario.API/Services/AuthService.cs` | Criar |
| `CaixaDiario.API/Services/UsuarioService.cs` | Criar |
| `CaixaDiario.API/Services/RegistroService.cs` | Criar |
| `CaixaDiario.API/Middleware/ErrorHandlingMiddleware.cs` | Criar |
| `CaixaDiario.API/Controllers/AuthController.cs` | Criar |
| `CaixaDiario.API/Controllers/UsuariosController.cs` | Criar |
| `CaixaDiario.API/Controllers/RegistrosController.cs` | Criar |
| `CaixaDiario.API/Program.cs` | Criar |
| `CaixaDiario.API/appsettings.json` | Criar |
| `CaixaDiario.API/appsettings.Development.json` | Criar |
| `CaixaDiario.Tests/Services/AuthServiceTests.cs` | Criar |
| `CaixaDiario.Tests/Services/UsuarioServiceTests.cs` | Criar |
| `CaixaDiario.Tests/Services/RegistroServiceTests.cs` | Criar |
| `index.html` | Modificar |
| `CaixaDiario.API/README.md` | Criar |

---

## Task 1: Criar estrutura da solução

**Files:**
- Criar: `CaixaDiario.sln`
- Criar: `CaixaDiario.API/CaixaDiario.API.csproj`
- Criar: `CaixaDiario.Tests/CaixaDiario.Tests.csproj`

- [ ] **Step 1: Criar a solução e os projetos**

```bash
cd C:\Users\gabri\source\repos\Freelance\Caixa-Diario-Mov
dotnet new sln -n CaixaDiario
dotnet new webapi -n CaixaDiario.API --no-openapi -f net8.0
dotnet new xunit -n CaixaDiario.Tests -f net8.0
dotnet sln add CaixaDiario.API/CaixaDiario.API.csproj
dotnet sln add CaixaDiario.Tests/CaixaDiario.Tests.csproj
```

- [ ] **Step 2: Adicionar referência do projeto de testes para a API**

```bash
dotnet add CaixaDiario.Tests/CaixaDiario.Tests.csproj reference CaixaDiario.API/CaixaDiario.API.csproj
```

- [ ] **Step 3: Verificar que a solução compila**

```bash
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.sln CaixaDiario.API/ CaixaDiario.Tests/
git commit -m "feat: criar estrutura da solucao CaixaDiario"
```

---

## Task 2: Instalar pacotes NuGet

**Files:**
- Modificar: `CaixaDiario.API/CaixaDiario.API.csproj`
- Modificar: `CaixaDiario.Tests/CaixaDiario.Tests.csproj`

- [ ] **Step 1: Instalar pacotes na API**

```bash
cd CaixaDiario.API
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package BCrypt.Net-Next --version 4.0.3
```

- [ ] **Step 2: Instalar pacotes nos Testes**

```bash
cd ../CaixaDiario.Tests
dotnet add package Moq --version 4.20.70
dotnet add package BCrypt.Net-Next --version 4.0.3
```

- [ ] **Step 3: Verificar build**

```bash
cd ..
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.API/CaixaDiario.API.csproj CaixaDiario.Tests/CaixaDiario.Tests.csproj
git commit -m "feat: instalar pacotes NuGet (JWT, EF Core, Npgsql, BCrypt, Moq)"
```

---

## Task 3: Models e tipos base

**Files:**
- Criar: `CaixaDiario.API/Models/ItemFinanceiro.cs`
- Criar: `CaixaDiario.API/Models/Usuario.cs`
- Criar: `CaixaDiario.API/Models/RegistroDiario.cs`
- Criar: `CaixaDiario.API/Enums/CodigoRetorno.cs`
- Criar: `CaixaDiario.API/Exceptions/ApiException.cs`
- Criar: `CaixaDiario.API/Responses/ApiResponse.cs`
- Criar: `CaixaDiario.API/Responses/ErroResponse.cs`

- [ ] **Step 1: Criar ItemFinanceiro**

```csharp
// CaixaDiario.API/Models/ItemFinanceiro.cs
namespace CaixaDiario.API.Models;

public class ItemFinanceiro
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}
```

- [ ] **Step 2: Criar model Usuario**

```csharp
// CaixaDiario.API/Models/Usuario.cs
namespace CaixaDiario.API.Models;

public class Usuario
{
    public Guid Id { get; set; }
    public string NomeUsuario { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Loja { get; set; }
    public string Perfil { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public string? UsuarioAtualizacao { get; set; }

    public ICollection<RegistroDiario> Registros { get; set; } = new List<RegistroDiario>();
}
```

- [ ] **Step 3: Criar model RegistroDiario**

```csharp
// CaixaDiario.API/Models/RegistroDiario.cs
namespace CaixaDiario.API.Models;

public class RegistroDiario
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public decimal Entrada { get; set; }
    public List<ItemFinanceiro> Saidas { get; set; } = new();
    public List<ItemFinanceiro> ContasReceber { get; set; } = new();
    public List<ItemFinanceiro> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
    public bool Excluido { get; set; } = false;
    public string? MotivoExclusao { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime SalvoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public string? UsuarioAtualizacao { get; set; }

    public Usuario Cliente { get; set; } = null!;
}
```

- [ ] **Step 4: Criar enum CodigoRetorno**

```csharp
// CaixaDiario.API/Enums/CodigoRetorno.cs
namespace CaixaDiario.API.Enums;

public enum CodigoRetorno
{
    SUCESSO,
    DADOS_INVALIDOS,
    CREDENCIAIS_INVALIDAS,
    USUARIO_INATIVO,
    TOKEN_INVALIDO,
    SEM_PERMISSAO,
    SENHA_MUITO_CURTA,
    USUARIO_NAO_ENCONTRADO,
    NOME_USUARIO_DUPLICADO,
    DATA_FUTURA,
    MOTIVO_OBRIGATORIO,
    REGISTRO_NAO_ENCONTRADO,
    ACESSO_NEGADO,
    ERRO_INTERNO
}
```

- [ ] **Step 5: Criar ApiException**

```csharp
// CaixaDiario.API/Exceptions/ApiException.cs
using CaixaDiario.API.Enums;

namespace CaixaDiario.API.Exceptions;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public CodigoRetorno Codigo { get; }
    public string? Campo { get; }

    public ApiException(int statusCode, CodigoRetorno codigo, string mensagem, string? campo = null)
        : base(mensagem)
    {
        StatusCode = statusCode;
        Codigo = codigo;
        Campo = campo;
    }
}
```

- [ ] **Step 6: Criar ApiResponse e ErroResponse**

```csharp
// CaixaDiario.API/Responses/ApiResponse.cs
using CaixaDiario.API.Enums;

namespace CaixaDiario.API.Responses;

public class ApiResponse<T>
{
    public string Codigo { get; set; } = CodigoRetorno.SUCESSO.ToString();
    public T? Dados { get; set; }
}
```

```csharp
// CaixaDiario.API/Responses/ErroResponse.cs
namespace CaixaDiario.API.Responses;

public class ErroResponse
{
    public int Status { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public string? Campo { get; set; }
}
```

- [ ] **Step 7: Verificar build**

```bash
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
git add CaixaDiario.API/Models/ CaixaDiario.API/Enums/ CaixaDiario.API/Exceptions/ CaixaDiario.API/Responses/
git commit -m "feat: adicionar models, enum CodigoRetorno, ApiException e responses"
```

---

## Task 4: DTOs

**Files:**
- Criar: `CaixaDiario.API/DTOs/Auth/LoginRequestDto.cs`
- Criar: `CaixaDiario.API/DTOs/Auth/LoginResponseDto.cs`
- Criar: `CaixaDiario.API/DTOs/Usuarios/UsuarioDto.cs`
- Criar: `CaixaDiario.API/DTOs/Usuarios/CriarUsuarioDto.cs`
- Criar: `CaixaDiario.API/DTOs/Usuarios/AtualizarUsuarioDto.cs`
- Criar: `CaixaDiario.API/DTOs/Registros/ItemFinanceiroDto.cs`
- Criar: `CaixaDiario.API/DTOs/Registros/RegistroDto.cs`
- Criar: `CaixaDiario.API/DTOs/Registros/CriarRegistroDto.cs`
- Criar: `CaixaDiario.API/DTOs/Registros/ExcluirRegistroDto.cs`

- [ ] **Step 1: DTOs de Auth**

```csharp
// CaixaDiario.API/DTOs/Auth/LoginRequestDto.cs
using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Auth;

public class LoginRequestDto
{
    [Required] public string NomeUsuario { get; set; } = string.Empty;
    [Required] public string Senha { get; set; } = string.Empty;
}
```

```csharp
// CaixaDiario.API/DTOs/Auth/LoginResponseDto.cs
namespace CaixaDiario.API.DTOs.Auth;

public class LoginResponseDto
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
}
```

- [ ] **Step 2: DTOs de Usuários**

```csharp
// CaixaDiario.API/DTOs/Usuarios/UsuarioDto.cs
namespace CaixaDiario.API.DTOs.Usuarios;

public class UsuarioDto
{
    public Guid Id { get; set; }
    public string NomeUsuario { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Loja { get; set; }
    public string Perfil { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime CriadoEm { get; set; }
}
```

```csharp
// CaixaDiario.API/DTOs/Usuarios/CriarUsuarioDto.cs
using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Usuarios;

public class CriarUsuarioDto
{
    [Required] public string NomeUsuario { get; set; } = string.Empty;
    [Required] public string Senha { get; set; } = string.Empty;
    [Required] public string Nome { get; set; } = string.Empty;
    public string? Loja { get; set; }
}
```

```csharp
// CaixaDiario.API/DTOs/Usuarios/AtualizarUsuarioDto.cs
using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Usuarios;

public class AtualizarUsuarioDto
{
    [Required] public string NomeUsuario { get; set; } = string.Empty;
    public string? Senha { get; set; }
    [Required] public string Nome { get; set; } = string.Empty;
    public string? Loja { get; set; }
}
```

- [ ] **Step 3: DTOs de Registros**

```csharp
// CaixaDiario.API/DTOs/Registros/ItemFinanceiroDto.cs
namespace CaixaDiario.API.DTOs.Registros;

public class ItemFinanceiroDto
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}
```

```csharp
// CaixaDiario.API/DTOs/Registros/RegistroDto.cs
namespace CaixaDiario.API.DTOs.Registros;

public class RegistroDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public decimal Entrada { get; set; }
    public List<ItemFinanceiroDto> Saidas { get; set; } = new();
    public List<ItemFinanceiroDto> ContasReceber { get; set; } = new();
    public List<ItemFinanceiroDto> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
    public DateTime SalvoEm { get; set; }
}
```

```csharp
// CaixaDiario.API/DTOs/Registros/CriarRegistroDto.cs
using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Registros;

public class CriarRegistroDto
{
    [Required] public Guid ClienteId { get; set; }
    [Required] public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public decimal Entrada { get; set; }
    public List<ItemFinanceiroDto> Saidas { get; set; } = new();
    public List<ItemFinanceiroDto> ContasReceber { get; set; } = new();
    public List<ItemFinanceiroDto> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
}
```

```csharp
// CaixaDiario.API/DTOs/Registros/ExcluirRegistroDto.cs
using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Registros;

public class ExcluirRegistroDto
{
    [Required] public string MotivoExclusao { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Verificar build**

```bash
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add CaixaDiario.API/DTOs/
git commit -m "feat: adicionar DTOs de auth, usuarios e registros"
```

---

## Task 5: DbContext e configuração

**Files:**
- Criar: `CaixaDiario.API/Data/AppDbContext.cs`
- Criar: `CaixaDiario.API/appsettings.json`
- Criar: `CaixaDiario.API/appsettings.Development.json`
- Criar: `CaixaDiario.API/.gitignore` (para proteger appsettings.Development.json)

- [ ] **Step 1: Criar AppDbContext**

```csharp
// CaixaDiario.API/Data/AppDbContext.cs
using System.Text.Json;
using CaixaDiario.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Data;

public class AppDbContext : DbContext
{
    private static readonly JsonSerializerOptions _jsonOptions = new();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<RegistroDiario> RegistrosDiarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.NomeUsuario).HasColumnName("nome_usuario").IsRequired();
            entity.HasIndex(e => e.NomeUsuario).IsUnique();
            entity.Property(e => e.SenhaHash).HasColumnName("senha_hash").IsRequired();
            entity.Property(e => e.Nome).HasColumnName("nome").IsRequired();
            entity.Property(e => e.Loja).HasColumnName("loja");
            entity.Property(e => e.Perfil).HasColumnName("perfil").IsRequired();
            entity.Property(e => e.Ativo).HasColumnName("ativo").HasDefaultValue(true);
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
            entity.Property(e => e.UsuarioAtualizacao).HasColumnName("usuario_atualizacao");
        });

        modelBuilder.Entity<RegistroDiario>(entity =>
        {
            entity.ToTable("registros_diarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.Data).HasColumnName("data");
            entity.Property(e => e.Inicio).HasColumnName("inicio").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Entrada).HasColumnName("entrada").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Saidas).HasColumnName("saidas").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ItemFinanceiro>>(v, _jsonOptions) ?? new());
            entity.Property(e => e.ContasReceber).HasColumnName("contas_receber").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ItemFinanceiro>>(v, _jsonOptions) ?? new());
            entity.Property(e => e.ContasPagar).HasColumnName("contas_pagar").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ItemFinanceiro>>(v, _jsonOptions) ?? new());
            entity.Property(e => e.SaldoFinal).HasColumnName("saldo_final").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Excluido).HasColumnName("excluido").HasDefaultValue(false);
            entity.Property(e => e.MotivoExclusao).HasColumnName("motivo_exclusao");
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em");
            entity.Property(e => e.SalvoEm).HasColumnName("salvo_em");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
            entity.Property(e => e.UsuarioAtualizacao).HasColumnName("usuario_atualizacao");

            entity.HasOne(e => e.Cliente)
                .WithMany(u => u.Registros)
                .HasForeignKey(e => e.ClienteId);

            entity.HasIndex(e => new { e.ClienteId, e.Data }).IsUnique();
        });
    }
}
```

- [ ] **Step 2: Criar appsettings.json (sem segredos)**

```json
// CaixaDiario.API/appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Jwt": {
    "SecretKey": "",
    "Issuer": "CaixaDiario",
    "Audience": "CaixaDiario",
    "ExpiresInHours": "24"
  },
  "Cors": {
    "AllowedOrigins": ""
  }
}
```

- [ ] **Step 3: Criar appsettings.Development.json com valores reais (não commitar)**

```json
// CaixaDiario.API/appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<host-supabase>;Database=postgres;Username=postgres;Password=<sua-senha>;SSL Mode=Require;Trust Server Certificate=true"
  },
  "Jwt": {
    "SecretKey": "<chave-secreta-minimo-32-caracteres>",
    "Issuer": "CaixaDiario",
    "Audience": "CaixaDiario",
    "ExpiresInHours": "24"
  },
  "Cors": {
    "AllowedOrigins": "http://localhost:5500,http://127.0.0.1:5500"
  }
}
```

- [ ] **Step 4: Proteger appsettings.Development.json no .gitignore**

Adicionar ao `.gitignore` da raiz (ou criar se não existir):

```
# Segredos de desenvolvimento
CaixaDiario.API/appsettings.Development.json
```

- [ ] **Step 5: Verificar build**

```bash
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add CaixaDiario.API/Data/ CaixaDiario.API/appsettings.json .gitignore
git commit -m "feat: adicionar AppDbContext, configuracao appsettings e gitignore"
```

---

## Task 6: Interfaces dos Repositories

**Files:**
- Criar: `CaixaDiario.API/Repositories/Interfaces/IUsuarioRepository.cs`
- Criar: `CaixaDiario.API/Repositories/Interfaces/IRegistroRepository.cs`

- [ ] **Step 1: Criar IUsuarioRepository**

```csharp
// CaixaDiario.API/Repositories/Interfaces/IUsuarioRepository.cs
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(Guid id);
    Task<Usuario?> ObterPorNomeUsuarioAsync(string nomeUsuario);
    Task<List<Usuario>> ListarAtivosAsync();
    Task<bool> ExisteNomeUsuarioAsync(string nomeUsuario, Guid? excluirId = null);
    Task<Usuario> AdicionarAsync(Usuario usuario);
    Task<Usuario> AtualizarAsync(Usuario usuario);
}
```

- [ ] **Step 2: Criar IRegistroRepository**

```csharp
// CaixaDiario.API/Repositories/Interfaces/IRegistroRepository.cs
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IRegistroRepository
{
    Task<RegistroDiario?> ObterPorClienteEDataAsync(Guid clienteId, DateOnly data);
    Task<List<RegistroDiario>> ListarPorClienteAsync(Guid clienteId);
    Task<RegistroDiario> AdicionarAsync(RegistroDiario registro);
    Task<RegistroDiario> AtualizarAsync(RegistroDiario registro);
}
```

- [ ] **Step 3: Verificar build**

```bash
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.API/Repositories/Interfaces/
git commit -m "feat: adicionar interfaces IUsuarioRepository e IRegistroRepository"
```

---

## Task 7: Implementação dos Repositories

**Files:**
- Criar: `CaixaDiario.API/Repositories/UsuarioRepository.cs`
- Criar: `CaixaDiario.API/Repositories/RegistroRepository.cs`

- [ ] **Step 1: Criar UsuarioRepository**

```csharp
// CaixaDiario.API/Repositories/UsuarioRepository.cs
using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _context;

    public UsuarioRepository(AppDbContext context) => _context = context;

    public async Task<Usuario?> ObterPorIdAsync(Guid id) =>
        await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<Usuario?> ObterPorNomeUsuarioAsync(string nomeUsuario) =>
        await _context.Usuarios.FirstOrDefaultAsync(u => u.NomeUsuario == nomeUsuario);

    public async Task<List<Usuario>> ListarAtivosAsync() =>
        await _context.Usuarios.Where(u => u.Ativo).ToListAsync();

    public async Task<bool> ExisteNomeUsuarioAsync(string nomeUsuario, Guid? excluirId = null)
    {
        var query = _context.Usuarios.Where(u => u.NomeUsuario == nomeUsuario);
        if (excluirId.HasValue)
            query = query.Where(u => u.Id != excluirId.Value);
        return await query.AnyAsync();
    }

    public async Task<Usuario> AdicionarAsync(Usuario usuario)
    {
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
        return usuario;
    }

    public async Task<Usuario> AtualizarAsync(Usuario usuario)
    {
        _context.Usuarios.Update(usuario);
        await _context.SaveChangesAsync();
        return usuario;
    }
}
```

- [ ] **Step 2: Criar RegistroRepository**

```csharp
// CaixaDiario.API/Repositories/RegistroRepository.cs
using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class RegistroRepository : IRegistroRepository
{
    private readonly AppDbContext _context;

    public RegistroRepository(AppDbContext context) => _context = context;

    public async Task<RegistroDiario?> ObterPorClienteEDataAsync(Guid clienteId, DateOnly data) =>
        await _context.RegistrosDiarios
            .FirstOrDefaultAsync(r => r.ClienteId == clienteId && r.Data == data && !r.Excluido);

    public async Task<List<RegistroDiario>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.RegistrosDiarios
            .Where(r => r.ClienteId == clienteId && !r.Excluido)
            .OrderByDescending(r => r.Data)
            .ToListAsync();

    public async Task<RegistroDiario> AdicionarAsync(RegistroDiario registro)
    {
        _context.RegistrosDiarios.Add(registro);
        await _context.SaveChangesAsync();
        return registro;
    }

    public async Task<RegistroDiario> AtualizarAsync(RegistroDiario registro)
    {
        _context.RegistrosDiarios.Update(registro);
        await _context.SaveChangesAsync();
        return registro;
    }
}
```

- [ ] **Step 3: Verificar build**

```bash
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.API/Repositories/UsuarioRepository.cs CaixaDiario.API/Repositories/RegistroRepository.cs
git commit -m "feat: implementar UsuarioRepository e RegistroRepository"
```

---

## Task 8: TokenService

**Files:**
- Criar: `CaixaDiario.API/Services/ITokenService.cs`
- Criar: `CaixaDiario.API/Services/TokenService.cs`

- [ ] **Step 1: Criar ITokenService**

```csharp
// CaixaDiario.API/Services/ITokenService.cs
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface ITokenService
{
    string GerarToken(Usuario usuario);
}
```

- [ ] **Step 2: Criar TokenService**

```csharp
// CaixaDiario.API/Services/TokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CaixaDiario.API.Models;
using Microsoft.IdentityModel.Tokens;

namespace CaixaDiario.API.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config) => _config = config;

    public string GerarToken(Usuario usuario)
    {
        var secretKey = _config["Jwt:SecretKey"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;
        var expiresInHours = int.Parse(_config["Jwt:ExpiresInHours"] ?? "24");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("id", usuario.Id.ToString()),
            new Claim("nome_usuario", usuario.NomeUsuario),
            new Claim("perfil", usuario.Perfil)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiresInHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 3: Verificar build**

```bash
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.API/Services/ITokenService.cs CaixaDiario.API/Services/TokenService.cs
git commit -m "feat: adicionar ITokenService e TokenService com geracao de JWT"
```

---

## Task 9: AuthService + Testes

**Files:**
- Criar: `CaixaDiario.API/Services/AuthService.cs`
- Criar: `CaixaDiario.Tests/Services/AuthServiceTests.cs`

- [ ] **Step 1: Escrever os testes (falham primeiro — TDD)**

```csharp
// CaixaDiario.Tests/Services/AuthServiceTests.cs
using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUsuarioRepository> _repoMock = new();
    private readonly Mock<ITokenService> _tokenMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_repoMock.Object, _tokenMock.Object);
    }

    private static Usuario CriarUsuarioAtivo(string senha = "senha123") => new()
    {
        Id = Guid.NewGuid(),
        NomeUsuario = "joao",
        SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha),
        Nome = "João Silva",
        Perfil = "cliente",
        Ativo = true,
        CriadoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task Login_CredenciaisValidas_RetornaLoginResponse()
    {
        var usuario = CriarUsuarioAtivo("senha123");
        _repoMock.Setup(r => r.ObterPorNomeUsuarioAsync("joao")).ReturnsAsync(usuario);
        _tokenMock.Setup(t => t.GerarToken(usuario)).Returns("token-gerado");

        var resultado = await _sut.LoginAsync(new LoginRequestDto { NomeUsuario = "joao", Senha = "senha123" });

        Assert.Equal("token-gerado", resultado.Token);
        Assert.Equal(usuario.Nome, resultado.Nome);
        Assert.Equal(usuario.Perfil, resultado.Perfil);
        Assert.Equal(usuario.Id, resultado.Id);
    }

    [Fact]
    public async Task Login_SenhaErrada_LancaCredenciaisInvalidas()
    {
        var usuario = CriarUsuarioAtivo("correta");
        _repoMock.Setup(r => r.ObterPorNomeUsuarioAsync("joao")).ReturnsAsync(usuario);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.LoginAsync(new LoginRequestDto { NomeUsuario = "joao", Senha = "errada" }));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CREDENCIAIS_INVALIDAS, ex.Codigo);
    }

    [Fact]
    public async Task Login_UsuarioInexistente_LancaCredenciaisInvalidas()
    {
        _repoMock.Setup(r => r.ObterPorNomeUsuarioAsync("naoexiste")).ReturnsAsync((Usuario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.LoginAsync(new LoginRequestDto { NomeUsuario = "naoexiste", Senha = "qualquer" }));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CREDENCIAIS_INVALIDAS, ex.Codigo);
    }

    [Fact]
    public async Task Login_UsuarioInativo_LancaUsuarioInativo()
    {
        var usuario = CriarUsuarioAtivo();
        usuario.Ativo = false;
        _repoMock.Setup(r => r.ObterPorNomeUsuarioAsync("joao")).ReturnsAsync(usuario);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.LoginAsync(new LoginRequestDto { NomeUsuario = "joao", Senha = "senha123" }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.USUARIO_INATIVO, ex.Codigo);
    }

    [Theory]
    [InlineData("", "senha")]
    [InlineData("joao", "")]
    public async Task Login_CamposVazios_LancaDadosInvalidos(string nomeUsuario, string senha)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.LoginAsync(new LoginRequestDto { NomeUsuario = nomeUsuario, Senha = senha }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }
}
```

- [ ] **Step 2: Rodar testes — devem falhar pois AuthService não existe ainda**

```bash
dotnet test CaixaDiario.Tests/ --filter "FullyQualifiedName~AuthServiceTests"
```
Esperado: erro de compilação — `AuthService` não encontrado.

- [ ] **Step 3: Implementar AuthService**

```csharp
// CaixaDiario.API/Services/AuthService.cs
using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class AuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ITokenService _tokenService;

    public AuthService(IUsuarioRepository usuarioRepository, ITokenService tokenService)
    {
        _usuarioRepository = usuarioRepository;
        _tokenService = tokenService;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NomeUsuario) || string.IsNullOrWhiteSpace(dto.Senha))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Nome de usuário e senha são obrigatórios.");

        var usuario = await _usuarioRepository.ObterPorNomeUsuarioAsync(dto.NomeUsuario);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Senha, usuario.SenhaHash))
            throw new ApiException(401, CodigoRetorno.CREDENCIAIS_INVALIDAS, "Usuário ou senha incorretos.");

        if (!usuario.Ativo)
            throw new ApiException(403, CodigoRetorno.USUARIO_INATIVO, "Usuário inativo.");

        return new LoginResponseDto
        {
            Id = usuario.Id,
            Token = _tokenService.GerarToken(usuario),
            Nome = usuario.Nome,
            Perfil = usuario.Perfil
        };
    }
}
```

- [ ] **Step 4: Rodar testes — devem passar**

```bash
dotnet test CaixaDiario.Tests/ --filter "FullyQualifiedName~AuthServiceTests"
```
Esperado: `Passed! - 6`

- [ ] **Step 5: Commit**

```bash
git add CaixaDiario.API/Services/AuthService.cs CaixaDiario.Tests/Services/AuthServiceTests.cs
git commit -m "feat: adicionar AuthService com testes (login, credenciais invalidas, inativo)"
```

---

## Task 10: UsuarioService + Testes

**Files:**
- Criar: `CaixaDiario.API/Services/UsuarioService.cs`
- Criar: `CaixaDiario.Tests/Services/UsuarioServiceTests.cs`

- [ ] **Step 1: Escrever os testes**

```csharp
// CaixaDiario.Tests/Services/UsuarioServiceTests.cs
using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class UsuarioServiceTests
{
    private readonly Mock<IUsuarioRepository> _repoMock = new();
    private readonly UsuarioService _sut;

    public UsuarioServiceTests()
    {
        _sut = new UsuarioService(_repoMock.Object);
    }

    private static Usuario CriarUsuario() => new()
    {
        Id = Guid.NewGuid(),
        NomeUsuario = "cliente1",
        SenhaHash = BCrypt.Net.BCrypt.HashPassword("senha1"),
        Nome = "Cliente Um",
        Perfil = "cliente",
        Ativo = true,
        CriadoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task Criar_DadosValidos_RetornaUsuarioCriado()
    {
        _repoMock.Setup(r => r.ExisteNomeUsuarioAsync("novocliente", null)).ReturnsAsync(false);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        var dto = new CriarUsuarioDto { NomeUsuario = "novocliente", Senha = "senha1", Nome = "Novo Cliente", Loja = "Loja X" };
        var resultado = await _sut.CriarAsync(dto, "admin");

        Assert.Equal("novocliente", resultado.NomeUsuario);
        Assert.Equal("Novo Cliente", resultado.Nome);
        Assert.Equal("cliente", resultado.Perfil);
        Assert.True(resultado.Ativo);
    }

    [Fact]
    public async Task Criar_NomeUsuarioDuplicado_LancaNomeUsuarioDuplicado()
    {
        _repoMock.Setup(r => r.ExisteNomeUsuarioAsync("duplicado", null)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarUsuarioDto { NomeUsuario = "duplicado", Senha = "senha1", Nome = "Teste" }, "admin"));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(CodigoRetorno.NOME_USUARIO_DUPLICADO, ex.Codigo);
    }

    [Fact]
    public async Task Criar_SenhaCurta_LancaSenhaMuitoCurta()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarUsuarioDto { NomeUsuario = "user", Senha = "abc", Nome = "Teste" }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SENHA_MUITO_CURTA, ex.Codigo);
    }

    [Theory]
    [InlineData("", "senha1", "Nome")]
    [InlineData("user", "senha1", "")]
    public async Task Criar_CamposObrigatoriosVazios_LancaDadosInvalidos(string nomeUsuario, string senha, string nome)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarUsuarioDto { NomeUsuario = nomeUsuario, Senha = senha, Nome = nome }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Desativar_UsuarioInexistente_LancaUsuarioNaoEncontrado()
    {
        _repoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.DesativarAsync(Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.USUARIO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task Desativar_UsuarioExistente_SetaAtivoFalse()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        await _sut.DesativarAsync(usuario.Id, "admin");

        _repoMock.Verify(r => r.AtualizarAsync(It.Is<Usuario>(u => !u.Ativo)), Times.Once);
    }
}
```

- [ ] **Step 2: Rodar testes — devem falhar**

```bash
dotnet test CaixaDiario.Tests/ --filter "FullyQualifiedName~UsuarioServiceTests"
```
Esperado: erro de compilação — `UsuarioService` não encontrado.

- [ ] **Step 3: Implementar UsuarioService**

```csharp
// CaixaDiario.API/Services/UsuarioService.cs
using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class UsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;

    public UsuarioService(IUsuarioRepository usuarioRepository) => _usuarioRepository = usuarioRepository;

    public async Task<List<UsuarioDto>> ListarAsync()
    {
        var usuarios = await _usuarioRepository.ListarAtivosAsync();
        return usuarios.Select(MapToDto).ToList();
    }

    public async Task<UsuarioDto> ObterPorIdAsync(Guid id)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.USUARIO_NAO_ENCONTRADO, "Usuário não encontrado.");
        return MapToDto(usuario);
    }

    public async Task<UsuarioDto> CriarAsync(CriarUsuarioDto dto, string nomeUsuarioLogado)
    {
        if (string.IsNullOrWhiteSpace(dto.NomeUsuario) || string.IsNullOrWhiteSpace(dto.Nome))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Nome e usuário são obrigatórios.");

        if (dto.Senha.Length < 4)
            throw new ApiException(400, CodigoRetorno.SENHA_MUITO_CURTA, "Senha deve ter no mínimo 4 caracteres.", "senha");

        if (await _usuarioRepository.ExisteNomeUsuarioAsync(dto.NomeUsuario))
            throw new ApiException(409, CodigoRetorno.NOME_USUARIO_DUPLICADO, "Nome de usuário já existe.", "nome_usuario");

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeUsuario = dto.NomeUsuario.ToLower(),
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(dto.Senha),
            Nome = dto.Nome,
            Loja = dto.Loja,
            Perfil = "cliente",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            UsuarioAtualizacao = nomeUsuarioLogado
        };

        var criado = await _usuarioRepository.AdicionarAsync(usuario);
        return MapToDto(criado);
    }

    public async Task<UsuarioDto> AtualizarAsync(Guid id, AtualizarUsuarioDto dto, string nomeUsuarioLogado)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.USUARIO_NAO_ENCONTRADO, "Usuário não encontrado.");

        if (string.IsNullOrWhiteSpace(dto.NomeUsuario) || string.IsNullOrWhiteSpace(dto.Nome))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Nome e usuário são obrigatórios.");

        if (!string.IsNullOrWhiteSpace(dto.Senha) && dto.Senha.Length < 4)
            throw new ApiException(400, CodigoRetorno.SENHA_MUITO_CURTA, "Senha deve ter no mínimo 4 caracteres.", "senha");

        if (await _usuarioRepository.ExisteNomeUsuarioAsync(dto.NomeUsuario, id))
            throw new ApiException(409, CodigoRetorno.NOME_USUARIO_DUPLICADO, "Nome de usuário já existe.", "nome_usuario");

        usuario.NomeUsuario = dto.NomeUsuario.ToLower();
        usuario.Nome = dto.Nome;
        usuario.Loja = dto.Loja;
        usuario.AtualizadoEm = DateTime.UtcNow;
        usuario.UsuarioAtualizacao = nomeUsuarioLogado;

        if (!string.IsNullOrWhiteSpace(dto.Senha))
            usuario.SenhaHash = BCrypt.Net.BCrypt.HashPassword(dto.Senha);

        var atualizado = await _usuarioRepository.AtualizarAsync(usuario);
        return MapToDto(atualizado);
    }

    public async Task DesativarAsync(Guid id, string nomeUsuarioLogado)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.USUARIO_NAO_ENCONTRADO, "Usuário não encontrado.");

        usuario.Ativo = false;
        usuario.AtualizadoEm = DateTime.UtcNow;
        usuario.UsuarioAtualizacao = nomeUsuarioLogado;

        await _usuarioRepository.AtualizarAsync(usuario);
    }

    private static UsuarioDto MapToDto(Usuario u) => new()
    {
        Id = u.Id,
        NomeUsuario = u.NomeUsuario,
        Nome = u.Nome,
        Loja = u.Loja,
        Perfil = u.Perfil,
        Ativo = u.Ativo,
        CriadoEm = u.CriadoEm
    };
}
```

- [ ] **Step 4: Rodar testes — devem passar**

```bash
dotnet test CaixaDiario.Tests/ --filter "FullyQualifiedName~UsuarioServiceTests"
```
Esperado: `Passed! - 7`

- [ ] **Step 5: Commit**

```bash
git add CaixaDiario.API/Services/UsuarioService.cs CaixaDiario.Tests/Services/UsuarioServiceTests.cs
git commit -m "feat: adicionar UsuarioService com testes (criar, duplicado, desativar)"
```

---

## Task 11: RegistroService + Testes

**Files:**
- Criar: `CaixaDiario.API/Services/RegistroService.cs`
- Criar: `CaixaDiario.Tests/Services/RegistroServiceTests.cs`

- [ ] **Step 1: Escrever os testes**

```csharp
// CaixaDiario.Tests/Services/RegistroServiceTests.cs
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class RegistroServiceTests
{
    private readonly Mock<IRegistroRepository> _repoMock = new();
    private readonly RegistroService _sut;

    public RegistroServiceTests()
    {
        _sut = new RegistroService(_repoMock.Object);
    }

    private static CriarRegistroDto CriarDto(DateOnly? data = null)
    {
        var clienteId = Guid.NewGuid();
        return new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = data ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Inicio = 100m,
            Entrada = 500m,
            Saidas = new List<ItemFinanceiroDto> { new() { Descricao = "Aluguel", Valor = 200m } },
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = 400m
        };
    }

    [Fact]
    public async Task Salvar_DataFutura_LancaDataFutura()
    {
        var dto = CriarDto(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "joao"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DATA_FUTURA, ex.Codigo);
    }

    [Fact]
    public async Task Salvar_RegistroNovo_RetornaCriadoTrue()
    {
        var dto = CriarDto();
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, criado) = await _sut.SalvarAsync(dto, "joao");

        Assert.True(criado);
        Assert.Equal(dto.SaldoFinal, resultado.SaldoFinal);
        Assert.Equal(dto.ClienteId, resultado.ClienteId);
    }

    [Fact]
    public async Task Salvar_RegistroExistente_RetornaCriadoFalse()
    {
        var dto = CriarDto();
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = dto.ClienteId, Data = dto.Data,
            SaldoFinal = 0, Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, criado) = await _sut.SalvarAsync(dto, "joao");

        Assert.False(criado);
        Assert.Equal(dto.SaldoFinal, resultado.SaldoFinal);
    }

    [Fact]
    public async Task Excluir_SemMotivo_LancaMotivoObrigatorio()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ExcluirAsync(clienteId, data, "", clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.MOTIVO_OBRIGATORIO, ex.Codigo);
    }

    [Fact]
    public async Task Excluir_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var usuarioLogadoId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ExcluirAsync(clienteId, data, "motivo", usuarioLogadoId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    [Fact]
    public async Task Listar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var usuarioLogadoId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ListarPorClienteAsync(clienteId, usuarioLogadoId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }
}
```

- [ ] **Step 2: Rodar testes — devem falhar**

```bash
dotnet test CaixaDiario.Tests/ --filter "FullyQualifiedName~RegistroServiceTests"
```
Esperado: erro de compilação — `RegistroService` não encontrado.

- [ ] **Step 3: Implementar RegistroService**

```csharp
// CaixaDiario.API/Services/RegistroService.cs
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class RegistroService
{
    private readonly IRegistroRepository _registroRepository;

    public RegistroService(IRegistroRepository registroRepository) => _registroRepository = registroRepository;

    public async Task<List<RegistroDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registros = await _registroRepository.ListarPorClienteAsync(clienteId);
        return registros.Select(MapToDto).ToList();
    }

    public async Task<RegistroDto> ObterPorDataAsync(Guid clienteId, DateOnly data, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registro = await _registroRepository.ObterPorClienteEDataAsync(clienteId, data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Registro não encontrado.");

        return MapToDto(registro);
    }

    public async Task<(RegistroDto dto, bool criado)> SalvarAsync(CriarRegistroDto dto, string nomeUsuarioLogado)
    {
        if (dto.Data > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ApiException(400, CodigoRetorno.DATA_FUTURA, "Não é possível registrar data futura.", "data");

        var existente = await _registroRepository.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data);

        if (existente != null)
        {
            existente.Inicio = dto.Inicio;
            existente.Entrada = dto.Entrada;
            existente.Saidas = dto.Saidas.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList();
            existente.ContasReceber = dto.ContasReceber.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList();
            existente.ContasPagar = dto.ContasPagar.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList();
            existente.SaldoFinal = dto.SaldoFinal;
            existente.SalvoEm = DateTime.UtcNow;
            existente.AtualizadoEm = DateTime.UtcNow;
            existente.UsuarioAtualizacao = nomeUsuarioLogado;

            var atualizado = await _registroRepository.AtualizarAsync(existente);
            return (MapToDto(atualizado), false);
        }

        var novo = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            Data = dto.Data,
            Inicio = dto.Inicio,
            Entrada = dto.Entrada,
            Saidas = dto.Saidas.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
            ContasReceber = dto.ContasReceber.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
            ContasPagar = dto.ContasPagar.Select(s => new ItemFinanceiro { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
            SaldoFinal = dto.SaldoFinal,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
            UsuarioAtualizacao = nomeUsuarioLogado
        };

        var criado = await _registroRepository.AdicionarAsync(novo);
        return (MapToDto(criado), true);
    }

    public async Task ExcluirAsync(Guid clienteId, DateOnly data, string motivo, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ApiException(400, CodigoRetorno.MOTIVO_OBRIGATORIO, "Motivo de exclusão é obrigatório.", "motivo_exclusao");

        var registro = await _registroRepository.ObterPorClienteEDataAsync(clienteId, data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Registro não encontrado.");

        registro.Excluido = true;
        registro.MotivoExclusao = motivo;
        registro.AtualizadoEm = DateTime.UtcNow;
        registro.UsuarioAtualizacao = usuarioLogadoId.ToString();

        await _registroRepository.AtualizarAsync(registro);
    }

    private static RegistroDto MapToDto(RegistroDiario r) => new()
    {
        Id = r.Id,
        ClienteId = r.ClienteId,
        Data = r.Data,
        Inicio = r.Inicio,
        Entrada = r.Entrada,
        Saidas = r.Saidas.Select(s => new ItemFinanceiroDto { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
        ContasReceber = r.ContasReceber.Select(s => new ItemFinanceiroDto { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
        ContasPagar = r.ContasPagar.Select(s => new ItemFinanceiroDto { Descricao = s.Descricao, Valor = s.Valor }).ToList(),
        SaldoFinal = r.SaldoFinal,
        SalvoEm = r.SalvoEm
    };
}
```

- [ ] **Step 4: Rodar todos os testes**

```bash
dotnet test CaixaDiario.Tests/
```
Esperado: `Passed! - 19` (6 auth + 7 usuario + 6 registro)

- [ ] **Step 5: Commit**

```bash
git add CaixaDiario.API/Services/RegistroService.cs CaixaDiario.Tests/Services/RegistroServiceTests.cs
git commit -m "feat: adicionar RegistroService com testes (salvar, excluir, acesso negado)"
```

---

## Task 12: Middleware e Program.cs

**Files:**
- Criar: `CaixaDiario.API/Middleware/ErrorHandlingMiddleware.cs`
- Criar: `CaixaDiario.API/Program.cs`

- [ ] **Step 1: Criar ErrorHandlingMiddleware**

```csharp
// CaixaDiario.API/Middleware/ErrorHandlingMiddleware.cs
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Responses;

namespace CaixaDiario.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErroResponse
            {
                Status = ex.StatusCode,
                Codigo = ex.Codigo.ToString(),
                Mensagem = ex.Message,
                Campo = ex.Campo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro nao tratado");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErroResponse
            {
                Status = 500,
                Codigo = CodigoRetorno.ERRO_INTERNO.ToString(),
                Mensagem = "Ocorreu um erro interno. Tente novamente mais tarde."
            });
        }
    }
}
```

- [ ] **Step 2: Criar Program.cs**

```csharp
// CaixaDiario.API/Program.cs
using System.Text;
using CaixaDiario.API.Data;
using CaixaDiario.API.Middleware;
using CaixaDiario.API.Repositories;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Banco de dados
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Autenticação JWT
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });

builder.Services.AddAuthorization();

// CORS
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',') ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// Injeção de dependência
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IRegistroRepository, RegistroRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<RegistroService>();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

- [ ] **Step 3: Verificar build**

```bash
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.API/Middleware/ CaixaDiario.API/Program.cs
git commit -m "feat: adicionar ErrorHandlingMiddleware e Program.cs com DI e JWT"
```

---

## Task 13: Controllers

**Files:**
- Criar: `CaixaDiario.API/Controllers/AuthController.cs`
- Criar: `CaixaDiario.API/Controllers/UsuariosController.cs`
- Criar: `CaixaDiario.API/Controllers/RegistrosController.cs`

- [ ] **Step 1: Criar AuthController**

```csharp
// CaixaDiario.API/Controllers/AuthController.cs
using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService) => _authService = authService;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var resultado = await _authService.LoginAsync(dto);
        return Ok(new ApiResponse<LoginResponseDto> { Dados = resultado });
    }
}
```

- [ ] **Step 2: Criar UsuariosController**

```csharp
// CaixaDiario.API/Controllers/UsuariosController.cs
using System.Security.Claims;
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
    private readonly UsuarioService _usuarioService;

    public UsuariosController(UsuarioService usuarioService) => _usuarioService = usuarioService;

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

- [ ] **Step 3: Criar RegistrosController**

```csharp
// CaixaDiario.API/Controllers/RegistrosController.cs
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
    private readonly RegistroService _registroService;

    public RegistrosController(RegistroService registroService) => _registroService = registroService;

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

- [ ] **Step 4: Verificar build completo**

```bash
dotnet build CaixaDiario.sln
```
Esperado: `Build succeeded.`

- [ ] **Step 5: Rodar todos os testes**

```bash
dotnet test CaixaDiario.Tests/
```
Esperado: `Passed! - 19`

- [ ] **Step 6: Commit**

```bash
git add CaixaDiario.API/Controllers/
git commit -m "feat: adicionar AuthController, UsuariosController e RegistrosController"
```

---

## Task 14: Migrations e seed do admin

**Files:**
- Criar: `CaixaDiario.API/Data/Migrations/` (gerado automaticamente)

- [ ] **Step 1: Instalar a ferramenta EF Core CLI (se não tiver)**

```bash
dotnet tool install --global dotnet-ef
```
Esperado: instalado ou já instalado.

- [ ] **Step 2: Gerar a migration inicial**

```bash
cd CaixaDiario.API
dotnet ef migrations add CriacaoInicial
```
Esperado: pasta `Data/Migrations/` criada com 3 arquivos.

- [ ] **Step 3: Aplicar migration no banco Supabase**

Garantir que `appsettings.Development.json` tem a connection string correta do Supabase, depois:

```bash
dotnet ef database update
```
Esperado: `Done.` — tabelas `usuarios` e `registros_diarios` criadas no Supabase.

- [ ] **Step 4: Inserir o usuário admin no banco**

Executar diretamente no Supabase (SQL Editor no painel do Supabase):

```sql
INSERT INTO usuarios (id, nome_usuario, senha_hash, nome, loja, perfil, ativo, criado_em, usuario_atualizacao)
VALUES (
  gen_random_uuid(),
  'admin',
  '$2a$11$placeholder_substituir_pelo_hash_gerado_abaixo',
  'Administrador',
  NULL,
  'admin',
  true,
  NOW(),
  'sistema'
);
```

Para gerar o hash da senha `admin123`, rodar este script C# temporário:

```csharp
// Rodar com: dotnet script ou em um projeto temporário
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("admin123"));
```

Substituir o placeholder pelo hash gerado antes de executar o SQL.

- [ ] **Step 5: Testar a API localmente**

```bash
cd CaixaDiario.API
dotnet run
```

Em outro terminal, testar o login:

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"nomeUsuario": "admin", "senha": "admin123"}'
```
Esperado: `{"codigo":"SUCESSO","dados":{"id":"...","token":"eyJ...","nome":"Administrador","perfil":"admin"}}`

- [ ] **Step 6: Commit das migrations**

```bash
git add CaixaDiario.API/Data/Migrations/
git commit -m "feat: adicionar migration inicial (tabelas usuarios e registros_diarios)"
```

---

## Task 15: Adaptação do Frontend

**Files:**
- Modificar: `index.html`

- [ ] **Step 1: Substituir a seção de configuração do Gist e variáveis globais**

Remover do topo do `<script>`:
```js
let session=null,cloud={},gistId='',gistTok='';
```

Substituir por:
```js
const API_URL = 'http://localhost:5000';
let session = null;
```

- [ ] **Step 2: Remover funções do Gist e substituir por funções de API**

Remover completamente as funções: `loadGistCfg`, `saveGistCfg`, `toggleGistPanel`, `setSyncBadge`, `gistLoad`, `gistSave`, `syncNow`, `loadLocalCloud`, `saveLocalCloud`.

Substituir por estas funções de API:

```js
function getAuthHeaders() {
  const token = localStorage.getItem('caixa_token');
  return { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` };
}

async function apiGet(path) {
  const r = await fetch(`${API_URL}${path}`, { headers: getAuthHeaders() });
  if (r.status === 401) { doLogout(); return null; }
  return r.json();
}

async function apiPost(path, body) {
  const r = await fetch(`${API_URL}${path}`, {
    method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(body)
  });
  return { status: r.status, data: await r.json() };
}

async function apiPut(path, body) {
  const r = await fetch(`${API_URL}${path}`, {
    method: 'PUT', headers: getAuthHeaders(), body: JSON.stringify(body)
  });
  return { status: r.status, data: await r.json() };
}

async function apiDelete(path, body = null) {
  const r = await fetch(`${API_URL}${path}`, {
    method: 'DELETE', headers: getAuthHeaders(),
    body: body ? JSON.stringify(body) : undefined
  });
  return { status: r.status, data: await r.json() };
}
```

- [ ] **Step 3: Reescrever doLogin**

```js
async function doLogin() {
  const u = document.getElementById('loginUser').value.trim();
  const p = document.getElementById('loginPass').value;
  if (!u || !p) { showLoginErr('Preencha usuário e senha.'); return; }

  const r = await fetch(`${API_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ nomeUsuario: u, senha: p })
  });
  const json = await r.json();

  if (!r.ok) {
    const msgs = {
      CREDENCIAIS_INVALIDAS: '❌ Usuário ou senha incorretos.',
      USUARIO_INATIVO: '❌ Usuário inativo. Entre em contato com o administrador.',
      DADOS_INVALIDOS: '❌ Preencha todos os campos.'
    };
    showLoginErr(msgs[json.codigo] || '❌ Erro ao fazer login.');
    return;
  }

  const { id, token, nome, perfil } = json.dados;
  localStorage.setItem('caixa_token', token);
  session = { id, user: id, role: perfil, name: nome };
  startApp();
}
```

- [ ] **Step 4: Reescrever saveClient (criar/editar cliente)**

```js
async function saveClient() {
  const name = document.getElementById('mClientName').value.trim();
  const user = document.getElementById('mClientUser').value.trim().toLowerCase();
  const pass = document.getElementById('mClientPass').value;
  const store = document.getElementById('mClientStore').value.trim();
  if (!name || !user) { alert('Nome e usuário são obrigatórios.'); return; }

  if (editingClientId) {
    const body = { nomeUsuario: user, nome: name, loja: store };
    if (pass) body.senha = pass;
    const { status, data } = await apiPut(`/api/usuarios/${editingClientId}`, body);
    if (!status.toString().startsWith('2')) { alert(data.mensagem || 'Erro ao atualizar.'); return; }
  } else {
    if (!pass || pass.length < 4) { alert('Senha mínima: 4 caracteres.'); return; }
    const { status, data } = await apiPost('/api/usuarios', { nomeUsuario: user, senha: pass, nome: name, loja: store });
    if (!status.toString().startsWith('2')) { alert(data.mensagem || 'Erro ao criar cliente.'); return; }
  }

  closeModal('modalClient');
  await carregarClientes();
  renderAdminOverview();
  alert('✅ Cliente salvo!');
}
```

- [ ] **Step 5: Reescrever carregarClientes e renderAdminOverview para usar API**

```js
async function carregarClientes() {
  const json = await apiGet('/api/usuarios');
  if (!json) return;
  cloud.users = {};
  (json.dados || []).forEach(u => { cloud.users[u.id] = { ...u, username: u.nomeUsuario }; });
  renderClientList();
}

async function carregarRegistros(clienteId) {
  const json = await apiGet(`/api/registros/${clienteId}`);
  if (!json) return [];
  return json.dados || [];
}
```

- [ ] **Step 6: Reescrever saveDay para usar API**

```js
async function saveDay() {
  const btn = document.getElementById('btnSave');
  btn.disabled = true; btn.textContent = '⏳ Salvando...';
  const k = dayKey(currentDay, currentMonth, currentYear);
  const ini = parseFloat(document.getElementById('inpInicio').value) || 0;
  const ent = parseFloat(document.getElementById('inpEntrada').value) || 0;
  const saidas = getSaidas();
  const prov = getProvisioning();
  const saldoFinal = ini + ent - saidas.reduce((a, s) => a + s.val, 0);

  const body = {
    clienteId: session.id,
    data: k,
    inicio: ini,
    entrada: ent,
    saidas: saidas.map(s => ({ descricao: s.desc, valor: s.val })),
    contasReceber: prov.receber.map(s => ({ descricao: s.desc, valor: s.val })),
    contasPagar: prov.pagar.map(s => ({ descricao: s.desc, valor: s.val })),
    saldoFinal
  };

  const { status, data } = await apiPost('/api/registros', body);
  btn.disabled = false; btn.innerHTML = '☁️ Salvar e sincronizar';
  if (status === 200 || status === 201) {
    alert(`✅ Salvo! Saldo final: ${fmt(saldoFinal)}`);
  } else {
    alert(`❌ Erro ao salvar: ${data.mensagem || 'tente novamente.'}`);
  }
  recalc();
}
```

- [ ] **Step 7: Reescrever confirmDelClient para usar API**

```js
async function confirmDelClient() {
  if (!pendingDelId) return;
  const { status, data } = await apiDelete(`/api/usuarios/${pendingDelId}`);
  if (!status.toString().startsWith('2')) { alert(data.mensagem || 'Erro ao excluir.'); return; }
  closeModal('modalDel');
  selectedClientId = null;
  await carregarClientes();
  renderAdminOverview();
  document.getElementById('clientDetailPanel').innerHTML =
    '<div class="panel-title">Selecione um cliente</div><p style="color:#666;font-size:14px">Clique em um cliente para ver ou editar.</p>';
}
```

- [ ] **Step 8: Reescrever confirmDelRecord para usar API**

```js
async function confirmDelRecord() {
  if (!pendingDelRecordDate) return;
  const motivo = prompt('Informe o motivo da exclusão:');
  if (!motivo) return;
  const { status, data } = await apiDelete(`/api/registros/${session.id}/${pendingDelRecordDate}`, { motivoExclusao: motivo });
  if (!status.toString().startsWith('2')) { alert(data.mensagem || 'Erro ao excluir.'); return; }
  closeModal('modalDelRec');
  renderHist();
}
```

- [ ] **Step 9: Atualizar doLogout para limpar o token**

```js
function doLogout() {
  if (!confirm('Sair?')) return;
  session = null;
  localStorage.removeItem('caixa_token');
  document.getElementById('appWrap').classList.remove('active');
  document.getElementById('loginWrap').style.display = 'flex';
  document.getElementById('loginUser').value = '';
  document.getElementById('loginPass').value = '';
  document.getElementById('loginErr').classList.remove('show');
}
```

- [ ] **Step 10: Atualizar o window.onload para remover loadGistCfg**

```js
window.addEventListener('load', () => {
  document.getElementById('loginPass').addEventListener('keydown', e => { if (e.key === 'Enter') doLogin(); });
  document.getElementById('loginUser').addEventListener('keydown', e => { if (e.key === 'Enter') doLogin(); });
});
```

- [ ] **Step 11: Remover o painel Gist do HTML**

Remover do HTML o bloco inteiro do painel Gist (linhas 171–181 do index.html original):

```html
<!-- Remover este bloco completo -->
<div style="margin-top:16px;text-align:center">
  <span class="gist-toggle" onclick="toggleGistPanel()">☁️ Configurar sincronização (Gist)</span>
</div>
<div id="gistPanel" class="gist-cfg" style="margin-top:14px;display:none">
  ...
</div>
```

- [ ] **Step 12: Testar o fluxo completo no browser**

1. Abrir `index.html` via Live Server (VS Code) ou `http-server`
2. Fazer login com `admin` / `admin123` — deve redirecionar para visão geral
3. Criar um cliente — deve aparecer na lista
4. Fazer logout, logar com o cliente — deve ver o caixa diário
5. Salvar um registro do dia — deve aparecer no histórico
6. Excluir um registro com motivo — deve sumir da lista

- [ ] **Step 13: Commit**

```bash
git add index.html
git commit -m "feat: adaptar frontend para consumir a API REST (remover Gist)"
```

---

## Task 16: README

**Files:**
- Criar: `CaixaDiario.API/README.md`

- [ ] **Step 1: Criar README.md**

```markdown
# Caixa Diário — Backend API

Sistema de controle de caixa diário para pequenos negócios. API REST construída com ASP.NET Core C# e banco de dados PostgreSQL (Supabase).

---

## Visão Geral

O backend expõe uma API REST que autentica usuários, gerencia clientes e persiste registros diários de caixa. Substitui a solução anterior baseada em GitHub Gist, resolvendo problemas de segurança e permitindo publicação para múltiplos clientes.

---

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Conta no [Supabase](https://supabase.com) (gratuita) — ou qualquer banco PostgreSQL
- [dotnet-ef](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`

---

## Configuração Local

**1. Clonar o repositório e navegar para a pasta da API:**
```bash
git clone <url-do-repo>
cd CaixaDiario.API
```

**2. Criar o arquivo de configuração de desenvolvimento (não é versionado):**
```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<host-supabase>;Database=postgres;Username=postgres;Password=<sua-senha>;SSL Mode=Require;Trust Server Certificate=true"
  },
  "Jwt": {
    "SecretKey": "<chave-secreta-minimo-32-caracteres>",
    "Issuer": "CaixaDiario",
    "Audience": "CaixaDiario",
    "ExpiresInHours": "24"
  },
  "Cors": {
    "AllowedOrigins": "http://localhost:5500,http://127.0.0.1:5500"
  }
}
```

**3. Rodar as migrations:**
```bash
dotnet ef database update
```

**4. Inserir o usuário admin** (no SQL Editor do Supabase — ver Task 14 do plano para gerar o hash):
```sql
INSERT INTO usuarios (id, nome_usuario, senha_hash, nome, perfil, ativo, criado_em, usuario_atualizacao)
VALUES (gen_random_uuid(), 'admin', '<hash-bcrypt>', 'Administrador', 'admin', true, NOW(), 'sistema');
```

**5. Iniciar a API:**
```bash
dotnet run
```
A API estará disponível em `http://localhost:5000`.

---

## Variáveis de Ambiente

| Variável | Descrição | Exemplo |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Connection string do PostgreSQL | `Host=db.xxx.supabase.co;...` |
| `Jwt__SecretKey` | Chave secreta para assinar tokens JWT (mínimo 32 chars) | `minha-chave-super-secreta-32chars` |
| `Jwt__Issuer` | Emissor do token JWT | `CaixaDiario` |
| `Jwt__Audience` | Audiência do token JWT | `CaixaDiario` |
| `Jwt__ExpiresInHours` | Validade do token em horas | `24` |
| `Cors__AllowedOrigins` | URLs do frontend separadas por vírgula | `https://meusite.com` |

---

## Arquitetura

O projeto segue **Arquitetura em Camadas com Repository Pattern**:

```
Controller → Service → Repository → DbContext (EF Core) → PostgreSQL
```

- **Controllers** — recebem requisições HTTP, validam autenticação e delegam para Services
- **Services** — contêm regras de negócio (validações, cálculos, permissões)
- **Repositories** — isolam o acesso ao banco via Entity Framework Core
- **Models** — entidades mapeadas pelo EF Core
- **DTOs** — contratos de entrada e saída da API

---

## Estrutura do Projeto

```
CaixaDiario.API/
├── Controllers/       # Endpoints HTTP
├── Data/              # DbContext e Migrations
├── DTOs/              # Objetos de transferência (entrada/saída)
├── Enums/             # CodigoRetorno
├── Exceptions/        # ApiException
├── Middleware/        # Tratamento global de erros
├── Models/            # Entidades do banco
├── Repositories/      # Acesso ao banco + interfaces
├── Responses/         # Formato padrão de resposta
├── Services/          # Regras de negócio
├── Program.cs         # Configuração e DI
└── appsettings.json   # Configuração (sem segredos)

CaixaDiario.Tests/
└── Services/          # Testes unitários dos Services (xUnit + Moq)
```

---

## Endpoints

| Método | Rota | Acesso |
|---|---|---|
| POST | `/api/auth/login` | Público |
| GET | `/api/usuarios` | Admin |
| GET | `/api/usuarios/{id}` | Admin |
| POST | `/api/usuarios` | Admin |
| PUT | `/api/usuarios/{id}` | Admin |
| DELETE | `/api/usuarios/{id}` | Admin |
| GET | `/api/registros/{clienteId}` | Admin + próprio cliente |
| GET | `/api/registros/{clienteId}/{data}` | Admin + próprio cliente |
| POST | `/api/registros` | Cliente |
| DELETE | `/api/registros/{clienteId}/{data}` | Admin + próprio cliente |

Todas as rotas autenticadas exigem o header: `Authorization: Bearer <token>`

Para detalhes completos (status codes, códigos de retorno), ver o [spec](../docs/superpowers/specs/2026-05-14-backend-csharp-design.md).

---

## Testes

```bash
dotnet test CaixaDiario.Tests/
```

Testes unitários cobrem os Services (AuthService, UsuarioService, RegistroService) com mock dos Repositories via Moq. Esperado: 19 testes passando.

---

## Deploy (Railway)

1. Criar conta em [railway.app](https://railway.app)
2. Criar novo projeto → "Deploy from GitHub repo"
3. Selecionar o repositório e a pasta `CaixaDiario.API`
4. Configurar as variáveis de ambiente no painel do Railway (as mesmas da tabela acima)
5. Railway detecta automaticamente o projeto .NET e faz o build
6. Copiar a URL gerada e atualizar `API_URL` no `index.html`
```

- [ ] **Step 2: Verificar se o README está completo e sem placeholders**

Ler o arquivo e confirmar que todas as seções estão preenchidas.

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.API/README.md
git commit -m "docs: adicionar README detalhado do backend"
```

---

## Verificação Final

- [ ] `dotnet build CaixaDiario.sln` — sem erros
- [ ] `dotnet test CaixaDiario.Tests/` — 19 testes passando
- [ ] Login via curl ou browser retorna JWT válido
- [ ] Frontend conecta na API e fluxo completo funciona (login → caixa → histórico)
- [ ] Token GitHub do Gist revogado em github.com/settings/tokens
```
