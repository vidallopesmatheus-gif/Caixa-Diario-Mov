# Sprint 0 — Fundação de Dados: Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar tabelas `contas_recorrentes` e `audit_logs`, estender models JSONB com `Categoria`/`TipoCusto`/`RecorrenciaId`, criar `AuditService`, `RecorrenciaService`, `CategoriasController` e seus testes.

**Architecture:** Modelos JSONB (`ItemFinanceiro`, `ContaProvisionada`) ganham campos opcionais sem migration de coluna. `ContaRecorrente` e `AuditLog` são entidades EF próprias com suas tabelas. Services seguem padrão `IInterface`/`Implementation` registrados como `Scoped`. Testes usam Mock<IInterface> (padrão do projeto) exceto `AuditService` que usa InMemory EF.

**Tech Stack:** .NET 10, EF Core + Npgsql, xUnit, Moq, Microsoft.EntityFrameworkCore.InMemory (adicionado ao test project)

---

## Mapa de Arquivos

| Ação | Arquivo |
|------|---------|
| Modify | `CaixaDiario.API/Models/ItemFinanceiro.cs` |
| Modify | `CaixaDiario.API/Models/ContaProvisionada.cs` |
| Modify | `CaixaDiario.API/Models/Usuario.cs` |
| Modify | `CaixaDiario.API/Enums/CodigoRetorno.cs` |
| Create | `CaixaDiario.API/Models/ContaRecorrente.cs` |
| Create | `CaixaDiario.API/Models/AuditLog.cs` |
| Modify | `CaixaDiario.API/Data/AppDbContext.cs` |
| Create | `CaixaDiario.API/Repositories/Interfaces/IContaRecorrenteRepository.cs` |
| Create | `CaixaDiario.API/Repositories/ContaRecorrenteRepository.cs` |
| Create | `CaixaDiario.API/Services/IAuditService.cs` |
| Create | `CaixaDiario.API/Services/AuditService.cs` |
| Create | `CaixaDiario.API/Services/IRecorrenciaService.cs` |
| Create | `CaixaDiario.API/Services/RecorrenciaService.cs` |
| Create | `CaixaDiario.API/Controllers/CategoriasController.cs` |
| Modify | `CaixaDiario.API/Program.cs` |
| Create | `CaixaDiario.Tests/Services/AuditServiceTests.cs` |
| Create | `CaixaDiario.Tests/Services/RecorrenciaServiceTests.cs` |

---

## Task 1: Atualizar models existentes + adicionar ContaRecorrente e AuditLog

**Files:**
- Modify: `CaixaDiario.API/Models/ItemFinanceiro.cs`
- Modify: `CaixaDiario.API/Models/ContaProvisionada.cs`
- Modify: `CaixaDiario.API/Models/Usuario.cs`
- Modify: `CaixaDiario.API/Enums/CodigoRetorno.cs`
- Create: `CaixaDiario.API/Models/ContaRecorrente.cs`
- Create: `CaixaDiario.API/Models/AuditLog.cs`

- [ ] **Step 1: Atualizar `ItemFinanceiro.cs`**

```csharp
namespace CaixaDiario.API.Models;

public class ItemFinanceiro
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public string? TipoCusto { get; set; } // "Receita" | "CustoFixo" | "CustoVariavel"
}
```

- [ ] **Step 2: Atualizar `ContaProvisionada.cs`**

```csharp
namespace CaixaDiario.API.Models;

public class ContaProvisionada
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public bool Pago { get; set; } = false;
    public string? Categoria { get; set; }
    public Guid? RecorrenciaId { get; set; }
}
```

- [ ] **Step 3: Criar `ContaRecorrente.cs`**

```csharp
namespace CaixaDiario.API.Models;

public class ContaRecorrente
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public string Tipo { get; set; } = string.Empty; // "Receber" | "Pagar"
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
}
```

- [ ] **Step 4: Criar `AuditLog.cs`**

```csharp
namespace CaixaDiario.API.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Entidade { get; set; } = string.Empty;
    // "RegistroDiario" | "ContaRecorrente" | "MetaAnual"
    public string AcaoTipo { get; set; } = string.Empty;
    // "Criacao" | "Edicao" | "Exclusao"
    public string EntidadeId { get; set; } = string.Empty;
    public string? DadosAntes { get; set; }
    public string? DadosDepois { get; set; }
    public DateTime OcorridoEm { get; set; }
}
```

- [ ] **Step 5: Adicionar navigation property em `Usuario.cs`**

No arquivo `CaixaDiario.API/Models/Usuario.cs`, adicionar após `public List<MetaAnual> MetasAnuais`:

```csharp
public ICollection<ContaRecorrente> ContasRecorrentes { get; set; } = new List<ContaRecorrente>();
```

- [ ] **Step 6: Adicionar valores ao `CodigoRetorno.cs`**

Adicionar antes de `ERRO_INTERNO`:
```csharp
CONTA_RECORRENTE_NAO_ENCONTRADA,
```

- [ ] **Step 7: Build para verificar compilação**

```
dotnet build CaixaDiario.API
```
Expected: Build succeeded, 0 errors.

---

## Task 2: Atualizar AppDbContext com novas entidades

**Files:**
- Modify: `CaixaDiario.API/Data/AppDbContext.cs`

- [ ] **Step 1: Adicionar DbSets**

Após `public DbSet<MetaAnual> MetasAnuais`:
```csharp
public DbSet<ContaRecorrente> ContasRecorrentes { get; set; }
public DbSet<AuditLog> AuditLogs { get; set; }
```

- [ ] **Step 2: Adicionar mapeamento de ContaRecorrente no `OnModelCreating`**

Após o bloco `modelBuilder.Entity<MetaAnual>`:
```csharp
modelBuilder.Entity<ContaRecorrente>(entity =>
{
    entity.ToTable("contas_recorrentes");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
    entity.Property(e => e.Descricao).HasColumnName("descricao").IsRequired();
    entity.Property(e => e.Valor).HasColumnName("valor").HasColumnType("decimal(18,2)");
    entity.Property(e => e.Categoria).HasColumnName("categoria");
    entity.Property(e => e.Tipo).HasColumnName("tipo").IsRequired();
    entity.Property(e => e.DataInicio).HasColumnName("data_inicio");
    entity.Property(e => e.DataFim).HasColumnName("data_fim");
    entity.Property(e => e.Ativo).HasColumnName("ativo").HasDefaultValue(true);
    entity.Property(e => e.CriadoEm).HasColumnName("criado_em");
    entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
    entity.HasOne(e => e.Cliente)
        .WithMany(u => u.ContasRecorrentes)
        .HasForeignKey(e => e.ClienteId);
    entity.HasIndex(e => new { e.ClienteId, e.Ativo });
});
```

- [ ] **Step 3: Adicionar mapeamento de AuditLog no `OnModelCreating`**

```csharp
modelBuilder.Entity<AuditLog>(entity =>
{
    entity.ToTable("audit_logs");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
    entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
    entity.Property(e => e.Entidade).HasColumnName("entidade").IsRequired();
    entity.Property(e => e.AcaoTipo).HasColumnName("acao_tipo").IsRequired();
    entity.Property(e => e.EntidadeId).HasColumnName("entidade_id").IsRequired();
    entity.Property(e => e.DadosAntes).HasColumnName("dados_antes");
    entity.Property(e => e.DadosDepois).HasColumnName("dados_depois");
    entity.Property(e => e.OcorridoEm).HasColumnName("ocorrido_em");
    entity.HasIndex(e => new { e.ClienteId, e.OcorridoEm });
    entity.HasIndex(e => new { e.Entidade, e.AcaoTipo });
});
```

- [ ] **Step 4: Build**

```
dotnet build CaixaDiario.API
```
Expected: Build succeeded.

---

## Task 3: EF Core Migration

**Files:**
- Create: migration gerada automaticamente em `CaixaDiario.API/Migrations/`

- [ ] **Step 1: Gerar migration**

```
cd CaixaDiario.API && dotnet ef migrations add AdicionarRecorrenciaEAuditoria
```
Expected: "Build succeeded." + "Done."

- [ ] **Step 2: Verificar migration gerada**

Abrir o arquivo de migration gerado e confirmar que contém:
- `CREATE TABLE contas_recorrentes` com colunas: id, cliente_id, descricao, valor, categoria, tipo, data_inicio, data_fim, ativo, criado_em, atualizado_em
- `CREATE TABLE audit_logs` com colunas: id, cliente_id, usuario_id, entidade, acao_tipo, entidade_id, dados_antes, dados_depois, ocorrido_em
- Índices em `(cliente_id, ativo)` e `(cliente_id, ocorrido_em)` e `(entidade, acao_tipo)`

- [ ] **Step 3: Aplicar migration ao banco**

```
dotnet ef database update
```
Expected: "Done."

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.API/Models/ CaixaDiario.API/Data/ CaixaDiario.API/Migrations/ CaixaDiario.API/Enums/
git commit -m "feat(sprint0): models ContaRecorrente + AuditLog + migration"
```

---

## Task 4: IAuditService + AuditService

**Files:**
- Create: `CaixaDiario.API/Services/IAuditService.cs`
- Create: `CaixaDiario.API/Services/AuditService.cs`

- [ ] **Step 1: Criar `IAuditService.cs`**

```csharp
namespace CaixaDiario.API.Services;

public interface IAuditService
{
    Task LogAsync(
        Guid clienteId,
        Guid usuarioId,
        string entidade,
        string acaoTipo,
        string entidadeId,
        string? dadosAntes,
        string? dadosDepois);
}
```

- [ ] **Step 2: Criar `AuditService.cs`**

```csharp
using CaixaDiario.API.Data;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;

    public AuditService(AppDbContext context) => _context = context;

    public async Task LogAsync(
        Guid clienteId,
        Guid usuarioId,
        string entidade,
        string acaoTipo,
        string entidadeId,
        string? dadosAntes,
        string? dadosDepois)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            UsuarioId = usuarioId,
            Entidade = entidade,
            AcaoTipo = acaoTipo,
            EntidadeId = entidadeId,
            DadosAntes = dadosAntes,
            DadosDepois = dadosDepois,
            OcorridoEm = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Build**

```
dotnet build CaixaDiario.API
```
Expected: Build succeeded.

---

## Task 5: IContaRecorrenteRepository + ContaRecorrenteRepository

**Files:**
- Create: `CaixaDiario.API/Repositories/Interfaces/IContaRecorrenteRepository.cs`
- Create: `CaixaDiario.API/Repositories/ContaRecorrenteRepository.cs`

- [ ] **Step 1: Criar `IContaRecorrenteRepository.cs`**

```csharp
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IContaRecorrenteRepository
{
    Task<List<ContaRecorrente>> ListarAtivasPorClienteAsync(Guid clienteId);
    Task<ContaRecorrente?> ObterPorIdAsync(Guid clienteId, Guid id);
    Task<ContaRecorrente> AdicionarAsync(ContaRecorrente conta);
    Task<ContaRecorrente> AtualizarAsync(ContaRecorrente conta);
}
```

- [ ] **Step 2: Criar `ContaRecorrenteRepository.cs`**

```csharp
using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class ContaRecorrenteRepository : IContaRecorrenteRepository
{
    private readonly AppDbContext _context;

    public ContaRecorrenteRepository(AppDbContext context) => _context = context;

    public async Task<List<ContaRecorrente>> ListarAtivasPorClienteAsync(Guid clienteId) =>
        await _context.ContasRecorrentes
            .Where(c => c.ClienteId == clienteId && c.Ativo)
            .OrderBy(c => c.CriadoEm)
            .ToListAsync();

    public async Task<ContaRecorrente?> ObterPorIdAsync(Guid clienteId, Guid id) =>
        await _context.ContasRecorrentes
            .FirstOrDefaultAsync(c => c.ClienteId == clienteId && c.Id == id);

    public async Task<ContaRecorrente> AdicionarAsync(ContaRecorrente conta)
    {
        _context.ContasRecorrentes.Add(conta);
        await _context.SaveChangesAsync();
        return conta;
    }

    public async Task<ContaRecorrente> AtualizarAsync(ContaRecorrente conta)
    {
        _context.ContasRecorrentes.Update(conta);
        await _context.SaveChangesAsync();
        return conta;
    }
}
```

---

## Task 6: IRecorrenciaService + RecorrenciaService

**Files:**
- Create: `CaixaDiario.API/Services/IRecorrenciaService.cs`
- Create: `CaixaDiario.API/Services/RecorrenciaService.cs`

- [ ] **Step 1: Criar `IRecorrenciaService.cs`**

```csharp
namespace CaixaDiario.API.Services;

public interface IRecorrenciaService
{
    Task MaterializarMesAtualAsync(Guid clienteId);
}
```

- [ ] **Step 2: Criar `RecorrenciaService.cs`**

```csharp
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class RecorrenciaService : IRecorrenciaService
{
    private readonly IContaRecorrenteRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;

    public RecorrenciaService(IContaRecorrenteRepository contaRepo, IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
    }

    public async Task MaterializarMesAtualAsync(Guid clienteId)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var ativas = await _contaRepo.ListarAtivasPorClienteAsync(clienteId);
        if (ativas.Count == 0) return;

        var registrosDoMes = await _registroRepo.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia);

        var materializados = new HashSet<Guid>(
            registrosDoMes.SelectMany(r =>
                r.ContasReceber.Concat(r.ContasPagar)
                    .Where(c => c.RecorrenciaId.HasValue)
                    .Select(c => c.RecorrenciaId!.Value)));

        var pendentes = ativas.Where(c =>
            !materializados.Contains(c.Id) &&
            c.DataInicio <= ultimoDia &&
            (c.DataFim == null || c.DataFim >= primeiroDia)).ToList();

        if (pendentes.Count == 0) return;

        var registroHoje = registrosDoMes.FirstOrDefault(r => r.Data == hoje);
        if (registroHoje == null)
        {
            var todos = await _registroRepo.ListarPorClienteAsync(clienteId);
            var saldoAnterior = todos.FirstOrDefault(r => r.Data < hoje)?.SaldoFinal ?? 0;
            registroHoje = new RegistroDiario
            {
                Id = Guid.NewGuid(),
                ClienteId = clienteId,
                Data = hoje,
                Inicio = saldoAnterior,
                SaldoFinal = saldoAnterior,
                CriadoEm = DateTime.UtcNow,
                SalvoEm = DateTime.UtcNow,
            };
            await _registroRepo.AdicionarAsync(registroHoje);
        }

        foreach (var conta in pendentes)
        {
            var nova = new ContaProvisionada
            {
                Descricao = conta.Descricao,
                Valor = conta.Valor,
                DataVencimento = hoje,
                Pago = false,
                Categoria = conta.Categoria,
                RecorrenciaId = conta.Id,
            };

            if (conta.Tipo == "Receber")
                registroHoje.ContasReceber.Add(nova);
            else
                registroHoje.ContasPagar.Add(nova);
        }

        await _registroRepo.AtualizarAsync(registroHoje);
    }
}
```

---

## Task 7: CategoriasController

**Files:**
- Create: `CaixaDiario.API/Controllers/CategoriasController.cs`

- [ ] **Step 1: Criar `CategoriasController.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/categorias")]
public class CategoriasController : ControllerBase
{
    private static readonly object _categorias = new
    {
        entradas = new[]
        {
            new { nome = "Vendas", tipoCusto = "Receita" },
            new { nome = "Serviços Prestados", tipoCusto = "Receita" },
            new { nome = "Outras Receitas", tipoCusto = "Receita" },
        },
        saidas = new[]
        {
            new { nome = "Aluguel", tipoCusto = "CustoFixo" },
            new { nome = "Salários/Folha", tipoCusto = "CustoFixo" },
            new { nome = "Energia/Água/Internet", tipoCusto = "CustoFixo" },
            new { nome = "Manutenção", tipoCusto = "CustoFixo" },
            new { nome = "Seguros", tipoCusto = "CustoFixo" },
            new { nome = "Insumos/Mercadoria", tipoCusto = "CustoVariavel" },
            new { nome = "Embalagens", tipoCusto = "CustoVariavel" },
            new { nome = "Comissões", tipoCusto = "CustoVariavel" },
            new { nome = "Marketing", tipoCusto = "CustoVariavel" },
            new { nome = "Outros", tipoCusto = "CustoVariavel" },
        },
    };

    [HttpGet]
    public IActionResult Listar() => Ok(_categorias);
}
```

---

## Task 8: Registrar serviços no DI (Program.cs)

**Files:**
- Modify: `CaixaDiario.API/Program.cs`

- [ ] **Step 1: Adicionar registros após os existentes**

Após `builder.Services.AddScoped<IMetaService, MetaService>();`, adicionar:

```csharp
builder.Services.AddScoped<IContaRecorrenteRepository, ContaRecorrenteRepository>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IRecorrenciaService, RecorrenciaService>();
```

- [ ] **Step 2: Build**

```
dotnet build CaixaDiario.API
```
Expected: Build succeeded.

---

## Task 9: Testes — AuditService + RecorrenciaService

**Files:**
- Modify: `CaixaDiario.Tests/CaixaDiario.Tests.csproj` (add InMemory package)
- Create: `CaixaDiario.Tests/Services/AuditServiceTests.cs`
- Create: `CaixaDiario.Tests/Services/RecorrenciaServiceTests.cs`

- [ ] **Step 1: Adicionar InMemory EF ao projeto de testes**

```
cd CaixaDiario.Tests && dotnet add package Microsoft.EntityFrameworkCore.InMemory
```
Expected: Package added successfully.

- [ ] **Step 2: Escrever testes para AuditService**

`CaixaDiario.Tests/Services/AuditServiceTests.cs`:

```csharp
using CaixaDiario.API.Data;
using CaixaDiario.API.Services;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.Tests.Services;

public class AuditServiceTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task LogAsync_GravaRegistro_ComTodosOsCampos()
    {
        var ctx = CriarContexto();
        var sut = new AuditService(ctx);
        var clienteId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        await sut.LogAsync(clienteId, usuarioId, "RegistroDiario", "Criacao", "id-123", null, "{\"saldo\":100}");

        var log = ctx.AuditLogs.Single();
        Assert.Equal(clienteId, log.ClienteId);
        Assert.Equal(usuarioId, log.UsuarioId);
        Assert.Equal("RegistroDiario", log.Entidade);
        Assert.Equal("Criacao", log.AcaoTipo);
        Assert.Equal("id-123", log.EntidadeId);
        Assert.Null(log.DadosAntes);
        Assert.Equal("{\"saldo\":100}", log.DadosDepois);
    }

    [Fact]
    public async Task LogAsync_MultiplosChamadas_GravaMultiplosRegistros()
    {
        var ctx = CriarContexto();
        var sut = new AuditService(ctx);
        var clienteId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        await sut.LogAsync(clienteId, usuarioId, "RegistroDiario", "Criacao", "id-1", null, "{}");
        await sut.LogAsync(clienteId, usuarioId, "RegistroDiario", "Edicao", "id-1", "{}", "{\"saldo\":200}");

        Assert.Equal(2, ctx.AuditLogs.Count());
    }
}
```

- [ ] **Step 3: Executar testes para ver falhar (classes não existem ainda)**

Wait — os testes foram escritos APÓS criar `AuditService`. Se os testes compilam, execute para confirmar que passam:

```
cd CaixaDiario.Tests && dotnet test --filter "AuditServiceTests" -v minimal
```
Expected: 2 passed.

- [ ] **Step 4: Escrever testes para RecorrenciaService**

`CaixaDiario.Tests/Services/RecorrenciaServiceTests.cs`:

```csharp
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class RecorrenciaServiceTests
{
    private readonly Mock<IContaRecorrenteRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly RecorrenciaService _sut;

    public RecorrenciaServiceTests() =>
        _sut = new RecorrenciaService(_contaRepoMock.Object, _registroRepoMock.Object);

    private static ContaRecorrente CriarContaRecorrente(string tipo = "Pagar", DateOnly? dataFim = null) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        Descricao = "Aluguel",
        Valor = 1000m,
        Tipo = tipo,
        DataInicio = new DateOnly(2026, 1, 1),
        DataFim = dataFim,
        Ativo = true,
        CriadoEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task MaterializarMesAtual_SemContasAtivas_NaoAcessaRegistros()
    {
        var clienteId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente>());

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.ListarPorPeriodoAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task MaterializarMesAtual_ContaJaMaterializada_NaoCriaDuplicata()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarContaRecorrente();
        conta.ClienteId = clienteId;

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Aluguel", Valor = 1000m, RecorrenciaId = conta.Id }
            },
            ContasReceber = new(),
        };

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario> { registroExistente });

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task MaterializarMesAtual_ContaPendente_CriaContaNoRegistroExistente()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarContaRecorrente(tipo: "Pagar");
        conta.ClienteId = clienteId;

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var registroHoje = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            ContasPagar = new List<ContaProvisionada>(),
            ContasReceber = new List<ContaProvisionada>(),
        };

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario> { registroHoje });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .ReturnsAsync((RegistroDiario r) => r);

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(rd =>
            rd.ContasPagar.Count == 1 &&
            rd.ContasPagar[0].RecorrenciaId == conta.Id)), Times.Once);
    }

    [Fact]
    public async Task MaterializarMesAtual_SemRegistroHoje_CriaRegistroVazioEThenAdicionaConta()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarContaRecorrente(tipo: "Receber");
        conta.ClienteId = clienteId;

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .ReturnsAsync((RegistroDiario r) => r);

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Once);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(rd =>
            rd.ContasReceber.Count == 1 &&
            rd.ContasReceber[0].RecorrenciaId == conta.Id)), Times.Once);
    }

    [Fact]
    public async Task MaterializarMesAtual_ContaForaDoIntervalo_NaoMaterializa()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarContaRecorrente();
        conta.ClienteId = clienteId;
        // DataFim no mês passado
        conta.DataFim = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-1);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        _contaRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { conta });
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia))
            .ReturnsAsync(new List<RegistroDiario>());

        await _sut.MaterializarMesAtualAsync(clienteId);

        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }
}
```

- [ ] **Step 5: Executar todos os testes do Sprint 0**

```
dotnet test CaixaDiario.Tests --filter "AuditServiceTests|RecorrenciaServiceTests" -v minimal
```
Expected: 7 passed.

- [ ] **Step 6: Executar todos os testes para garantir que nenhum quebrou**

```
dotnet test CaixaDiario.Tests -v minimal
```
Expected: All passed.

- [ ] **Step 7: Commit Sprint 0**

```bash
git add CaixaDiario.API/Services/ CaixaDiario.API/Repositories/ CaixaDiario.API/Controllers/CategoriasController.cs CaixaDiario.API/Program.cs CaixaDiario.Tests/Services/AuditServiceTests.cs CaixaDiario.Tests/Services/RecorrenciaServiceTests.cs CaixaDiario.Tests/CaixaDiario.Tests.csproj
git commit -m "feat(sprint0): AuditService, RecorrenciaService, CategoriasController + testes"
```
