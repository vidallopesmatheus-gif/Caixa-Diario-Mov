# Sprint 3 — Fase 3: Estratégica (Valuation, Auditoria, Exportações)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **Prerequisite:** Sprint 2 completo (MetricasService funcionando, EBITDA/PrimeCost/PE disponíveis).

**Goal:** Adicionar Valuation/Runway/Liquidez ao MetricasService; endpoint + UI de auditoria; expandir exportações com Excel enriquecido, PDF (QuestPDF) e CSV; atualizar ClientExportacaoPage com 3 botões.

**Architecture:** Valuation/Runway/Liquidez são adicionados ao `MetricasPeriodoDto` já existente (campos nullable — ausentes quando dados históricos insuficientes). `AuditoriaController` usa `AppDbContext` direto via novo `IAuditRepository`. Exportações expandem o `ExportController` existente com 3 rotas novas, mantendo a rota original para compatibilidade.

**Tech Stack:** .NET 10, QuestPDF (novo pacote), xUnit, Moq, React 19, TypeScript

---

## Mapa de Arquivos

| Ação | Arquivo |
|------|---------|
| Modify | `CaixaDiario.API/Services/MetricasService.cs` |
| Modify | `CaixaDiario.API/Services/IMetricasService.cs` |
| Modify | `CaixaDiario.API/Tests/Services/MetricasServiceTests.cs` |
| Create | `CaixaDiario.API/Repositories/Interfaces/IAuditRepository.cs` |
| Create | `CaixaDiario.API/Repositories/AuditRepository.cs` |
| Create | `CaixaDiario.API/DTOs/Auditoria/AuditLogDto.cs` |
| Create | `CaixaDiario.API/DTOs/Auditoria/AuditLogFiltroDto.cs` |
| Create | `CaixaDiario.API/Controllers/AuditoriaController.cs` |
| Modify | `CaixaDiario.API/Controllers/ExportController.cs` |
| Modify | `CaixaDiario.API/CaixaDiario.API.csproj` |
| Modify | `CaixaDiario.API/Program.cs` |
| Create | `frontend/src/api/auditoria.ts` |
| Create | `frontend/src/pages/admin/AdminAuditoriaPage.tsx` |
| Modify | `frontend/src/pages/client/ClientExportacaoPage.tsx` |
| Modify | `frontend/src/App.tsx` |

---

## Task 1: Valuation, Runway e Liquidez no MetricasService

**Files:**
- Modify: `CaixaDiario.API/Services/IMetricasService.cs`
- Modify: `CaixaDiario.API/Services/MetricasService.cs`
- Modify: `CaixaDiario.Tests/Services/MetricasServiceTests.cs`

- [ ] **Step 1: Atualizar `IMetricasService.cs`**

Adicionar um overload que passa todos os registros para cálculo histórico. A assinatura `CalcularPeriodo` já recebe `todosRegistros` — apenas adicionar documentação interna. Não requer mudança de interface.

Adicionar ao interface um método auxiliar para as métricas estratégicas:

```csharp
using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IMetricasService
{
    MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo);
    List<EvolucaoMensalDto> CalcularEvolucao(List<RegistroDiario> registros, int meses);
    FluxoProjetadoDto CalcularFluxoProjetado(List<RegistroDiario> registros, List<ContaRecorrente> recorrentes, int dias);
}
```

A interface não muda — `CalcularPeriodo` já inclui `todosRegistros` que serão usados para Valuation/Runway/Liquidez.

- [ ] **Step 2: Escrever testes de Valuation/Runway/Liquidez primeiro**

Adicionar ao final de `CaixaDiario.Tests/Services/MetricasServiceTests.cs`:

```csharp
// ---- Valuation ----

[Fact]
public void CalcularPeriodo_TresMesesDeDados_CalculaValuation()
{
    // Lucro médio 3 meses = 1000, Lucro Anual = 12000, Valuation = 36000
    var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
    var registros = new List<RegistroDiario>();
    for (int i = 2; i >= 0; i--)
    {
        var data = hoje.AddMonths(-i).AddDays(-5);
        registros.Add(CriarRegistro(data,
            new() { Item("Venda", 2000m, "Vendas", "Receita") },
            new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") }));
    }

    var resultado = _sut.CalcularPeriodo(registros, registros);

    Assert.NotNull(resultado.Valuation);
    Assert.True(resultado.Valuation!.Valor > 0);
}

[Fact]
public void CalcularPeriodo_SaldoZero_RunwayRetornaZero()
{
    var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
    var registro = CriarRegistro(hoje,
        new() { Item("Venda", 500m, "Vendas", "Receita") },
        new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") },
        saldoFinal: 0m);

    var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

    Assert.NotNull(resultado.Runway);
    Assert.Equal(0m, resultado.Runway!.Meses);
}

[Fact]
public void CalcularPeriodo_SemContasPagarProximos30Dias_LiquidezAltaLiquidez()
{
    var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
    var registro = CriarRegistro(hoje, new(), new(), saldoFinal: 5000m);

    var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

    Assert.NotNull(resultado.Liquidez);
    Assert.True(resultado.Liquidez!.AltaLiquidez);
}

[Fact]
public void CalcularPeriodo_ComContasPagarProximos30Dias_CalculaLiquidez()
{
    var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
    var amanha = hoje.AddDays(1);
    var registro = new RegistroDiario
    {
        Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
        Entradas = new(), Saidas = new(), SaldoFinal = 3000m,
        ContasReceber = new(),
        ContasPagar = new() { new() { Descricao = "Aluguel", Valor = 1000m, DataVencimento = amanha, Pago = false } },
    };

    var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

    // Liquidez = 3000 / 1000 = 3.0 → verde
    Assert.NotNull(resultado.Liquidez);
    Assert.Equal(3.0m, resultado.Liquidez!.Indice);
    Assert.Equal("verde", resultado.Liquidez.Semaforo);
    Assert.False(resultado.Liquidez.AltaLiquidez);
}
```

- [ ] **Step 3: Executar para ver novos testes falharem**

```
dotnet test CaixaDiario.Tests --filter "MetricasServiceTests" -v minimal
```
Expected: 4 novos testes FAIL.

- [ ] **Step 4: Adicionar Valuation/Runway/Liquidez ao `MetricasService.CalcularPeriodo`**

No final do método `CalcularPeriodo`, antes do `return dto;`, adicionar:

```csharp
// Valuation
var ultimos3Meses = Enumerable.Range(0, 3)
    .Select(i => DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-i))
    .Select(m => todosRegistros.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month).ToList())
    .ToList();

if (ultimos3Meses.Any(m => m.Count > 0))
{
    var lucrosMensais = ultimos3Meses.Select(m =>
        m.SelectMany(r => r.Entradas).Sum(e => e.Valor) -
        m.SelectMany(r => r.Saidas).Sum(s => s.Valor)).ToList();
    var lucroMedioMensal = lucrosMensais.Average();
    var valuationValor = lucroMedioMensal * 12 * 3;
    // Tendência: comparar mês atual vs mês anterior
    string valuationSemaforo = "cinza";
    if (ultimos3Meses[0].Count > 0 && ultimos3Meses[1].Count > 0)
    {
        var lucroAtual = lucrosMensais[0];
        var lucroAnterior = lucrosMensais[1];
        valuationSemaforo = lucroAnterior == 0 ? "cinza"
            : (lucroAtual - lucroAnterior) / Math.Abs(lucroAnterior) > 0.05m ? "verde"
            : (lucroAtual - lucroAnterior) / Math.Abs(lucroAnterior) < -0.05m ? "vermelho"
            : "amarelo";
    }
    dto.Valuation = new ValuationDto { Valor = valuationValor, Semaforo = valuationSemaforo };
}

// Runway
var saldoAtualRunway = todosRegistros.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;
var burnMedioMensal = ultimos3Meses
    .Where(m => m.Count > 0)
    .Select(m => m.SelectMany(r => r.Saidas).Sum(s => s.Valor))
    .DefaultIfEmpty(0)
    .Average();

dto.Runway = new RunwayDto
{
    Meses = burnMedioMensal > 0 ? Math.Round(saldoAtualRunway / burnMedioMensal, 1) : 0,
    Semaforo = burnMedioMensal == 0 ? "cinza"
        : saldoAtualRunway / burnMedioMensal > 6 ? "verde"
        : saldoAtualRunway / burnMedioMensal >= 3 ? "amarelo"
        : "vermelho",
};

// Liquidez
var hoje30 = DateOnly.FromDateTime(DateTime.UtcNow);
var em30dias = hoje30.AddDays(30);
var contasPagarProximas = todosRegistros
    .SelectMany(r => r.ContasPagar)
    .Where(c => !c.Pago && c.DataVencimento.HasValue &&
                c.DataVencimento.Value >= hoje30 && c.DataVencimento.Value <= em30dias)
    .Sum(c => c.Valor);

if (contasPagarProximas == 0)
{
    dto.Liquidez = new LiquidezDto { AltaLiquidez = true, Semaforo = "verde" };
}
else
{
    var indice = Math.Round(saldoAtualRunway / contasPagarProximas, 2);
    dto.Liquidez = new LiquidezDto
    {
        Indice = indice,
        AltaLiquidez = false,
        Semaforo = indice >= 1.5m ? "verde" : indice >= 1.0m ? "amarelo" : "vermelho",
    };
}
```

- [ ] **Step 5: Executar todos os testes de métricas**

```
dotnet test CaixaDiario.Tests --filter "MetricasServiceTests" -v minimal
```
Expected: All passed.

- [ ] **Step 6: Commit**

```bash
git add CaixaDiario.API/Services/MetricasService.cs CaixaDiario.Tests/Services/MetricasServiceTests.cs
git commit -m "feat(sprint3): Valuation, Runway, Liquidez no MetricasService + testes"
```

---

## Task 2: Frontend — Cards Valuation/Runway/Liquidez no Dashboard

**Files:**
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1: Adicionar cards estratégicos no JSX**

No bloco de métricas existente (`{metricas && ( <div className="stats-grid" ...>`), adicionar após os cards existentes:

```tsx
{metricas.valuation && (
  <StatCard
    label={`💎 Valuation ${metricas.valuation.semaforo === 'verde' ? '🟢' : metricas.valuation.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
    value={fmtBRL(metricas.valuation.valor)}
    className={metricas.valuation.semaforo === 'verde' ? 'val-green' : 'val-blue'}
  />
)}
{metricas.runway && (
  <StatCard
    label={`⏳ Runway ${metricas.runway.semaforo === 'verde' ? '🟢' : metricas.runway.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
    value={`${metricas.runway.meses.toFixed(1)} meses`}
    className={metricas.runway.semaforo === 'verde' ? 'val-green' : metricas.runway.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
  />
)}
{metricas.liquidez && (
  <StatCard
    label={`💧 Liquidez ${metricas.liquidez.semaforo === 'verde' ? '🟢' : metricas.liquidez.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
    value={metricas.liquidez.altaLiquidez ? 'Alta liquidez' : `${metricas.liquidez.indice?.toFixed(2)}×`}
    className={metricas.liquidez.semaforo === 'verde' ? 'val-green' : metricas.liquidez.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
  />
)}
```

- [ ] **Step 2: TypeScript check**

```
cd frontend && npx tsc --noEmit
```
Expected: No errors.

---

## Task 3: IAuditRepository + AuditRepository + AuditoriaController

**Files:**
- Create: `CaixaDiario.API/Repositories/Interfaces/IAuditRepository.cs`
- Create: `CaixaDiario.API/Repositories/AuditRepository.cs`
- Create: `CaixaDiario.API/DTOs/Auditoria/AuditLogDto.cs`
- Create: `CaixaDiario.API/Controllers/AuditoriaController.cs`
- Modify: `CaixaDiario.API/Program.cs`

- [ ] **Step 1: Criar `IAuditRepository.cs`**

```csharp
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IAuditRepository
{
    Task<(List<AuditLog> items, int total)> ListarPaginadoAsync(
        Guid clienteId,
        DateTime? de,
        DateTime? ate,
        string? entidade,
        string? acaoTipo,
        int pagina,
        int tamanhoPagina);
}
```

- [ ] **Step 2: Criar `AuditRepository.cs`**

```csharp
using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;

    public AuditRepository(AppDbContext context) => _context = context;

    public async Task<(List<AuditLog> items, int total)> ListarPaginadoAsync(
        Guid clienteId,
        DateTime? de,
        DateTime? ate,
        string? entidade,
        string? acaoTipo,
        int pagina,
        int tamanhoPagina)
    {
        var query = _context.AuditLogs
            .Where(l => l.ClienteId == clienteId)
            .AsQueryable();

        if (de.HasValue) query = query.Where(l => l.OcorridoEm >= de.Value);
        if (ate.HasValue) query = query.Where(l => l.OcorridoEm <= ate.Value);
        if (!string.IsNullOrWhiteSpace(entidade)) query = query.Where(l => l.Entidade == entidade);
        if (!string.IsNullOrWhiteSpace(acaoTipo)) query = query.Where(l => l.AcaoTipo == acaoTipo);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.OcorridoEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return (items, total);
    }
}
```

- [ ] **Step 3: Criar `AuditLogDto.cs`**

```csharp
namespace CaixaDiario.API.DTOs.Auditoria;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Entidade { get; set; } = string.Empty;
    public string AcaoTipo { get; set; } = string.Empty;
    public string EntidadeId { get; set; } = string.Empty;
    public string? DadosAntes { get; set; }
    public string? DadosDepois { get; set; }
    public DateTime OcorridoEm { get; set; }
}

public class AuditLogPaginadoDto
{
    public List<AuditLogDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
}
```

- [ ] **Step 4: Criar `AuditoriaController.cs`**

```csharp
using CaixaDiario.API.DTOs.Auditoria;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/auditoria")]
[Authorize]
public class AuditoriaController : ControllerBase
{
    private readonly IAuditRepository _repo;

    public AuditoriaController(IAuditRepository repo) => _repo = repo;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(
        Guid clienteId,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] string? entidade,
        [FromQuery] string? acao,
        [FromQuery] int pagina = 1)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        const int tamanhoPagina = 50;
        var (items, total) = await _repo.ListarPaginadoAsync(clienteId, de, ate, entidade, acao, pagina, tamanhoPagina);

        var resultado = new AuditLogPaginadoDto
        {
            Items = items.Select(l => new AuditLogDto
            {
                Id = l.Id, ClienteId = l.ClienteId, UsuarioId = l.UsuarioId,
                Entidade = l.Entidade, AcaoTipo = l.AcaoTipo, EntidadeId = l.EntidadeId,
                DadosAntes = l.DadosAntes, DadosDepois = l.DadosDepois, OcorridoEm = l.OcorridoEm,
            }).ToList(),
            Total = total, Pagina = pagina, TamanhoPagina = tamanhoPagina,
        };

        return Ok(new ApiResponse<AuditLogPaginadoDto> { Dados = resultado });
    }
}
```

- [ ] **Step 5: Registrar no DI (Program.cs)**

Após `builder.Services.AddScoped<IMetricasService, MetricasService>();`, adicionar:
```csharp
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
```

- [ ] **Step 6: Build**

```
dotnet build CaixaDiario.API
```
Expected: Build succeeded.

---

## Task 4: ExportController expandido com /xlsx, /pdf, /csv

**Files:**
- Modify: `CaixaDiario.API/CaixaDiario.API.csproj`
- Modify: `CaixaDiario.API/Controllers/ExportController.cs`

- [ ] **Step 1: Adicionar QuestPDF**

```
cd CaixaDiario.API && dotnet add package QuestPDF
```
Expected: Package added successfully.

- [ ] **Step 2: Substituir `ExportController.cs` completo**

```csharp
using ClosedXML.Excel;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IRegistroRepository _registroRepository;

    public ExportController(IRegistroRepository registroRepository) => _registroRepository = registroRepository;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    private async Task<List<CaixaDiario.API.Models.RegistroDiario>> CarregarEValidar(Guid clienteId, DateOnly de, DateOnly ate)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (ate < de)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Data final deve ser maior ou igual à inicial.");

        return await _registroRepository.ListarPorPeriodoAsync(clienteId, de, ate);
    }

    // Rota original mantida para compatibilidade
    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Exportar(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
        => await ExportarXlsx(clienteId, de, ate);

    [HttpGet("{clienteId:guid}/xlsx")]
    public async Task<IActionResult> ExportarXlsx(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
    {
        var registros = await CarregarEValidar(clienteId, de, ate);

        using var workbook = new XLWorkbook();

        // Aba 1: Resumo Diário
        var ws1 = workbook.Worksheets.Add("Resumo Diário");
        ws1.Cell(1, 1).Value = "Data"; ws1.Cell(1, 2).Value = "Total Entradas (R$)";
        ws1.Cell(1, 3).Value = "Total Saídas (R$)"; ws1.Cell(1, 4).Value = "Lucro Operacional (R$)";
        ws1.Cell(1, 5).Value = "Saldo Final (R$)";
        var h1 = ws1.Row(1); h1.Style.Font.Bold = true;
        h1.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C2C2E"); h1.Style.Font.FontColor = XLColor.White;

        int row = 2;
        foreach (var r in registros)
        {
            var te = r.Entradas.Sum(e => e.Valor); var ts = r.Saidas.Sum(s => s.Valor);
            ws1.Cell(row, 1).Value = r.Data.ToString("dd/MM/yyyy");
            ws1.Cell(row, 2).Value = (double)te; ws1.Cell(row, 3).Value = (double)ts;
            ws1.Cell(row, 4).Value = (double)(te - ts); ws1.Cell(row, 5).Value = (double)r.SaldoFinal;
            row++;
        }
        ws1.Columns().AdjustToContents();

        // Aba 2: Por Categoria
        var ws2 = workbook.Worksheets.Add("Por Categoria");
        ws2.Cell(1, 1).Value = "Data"; ws2.Cell(1, 2).Value = "Tipo";
        ws2.Cell(1, 3).Value = "Categoria"; ws2.Cell(1, 4).Value = "Descrição"; ws2.Cell(1, 5).Value = "Valor (R$)";
        var h2 = ws2.Row(1); h2.Style.Font.Bold = true;
        h2.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C2C2E"); h2.Style.Font.FontColor = XLColor.White;

        int row2 = 2;
        foreach (var r in registros)
        {
            foreach (var e in r.Entradas)
            {
                ws2.Cell(row2, 1).Value = r.Data.ToString("dd/MM/yyyy"); ws2.Cell(row2, 2).Value = "Entrada";
                ws2.Cell(row2, 3).Value = e.Categoria ?? ""; ws2.Cell(row2, 4).Value = e.Descricao;
                ws2.Cell(row2, 5).Value = (double)e.Valor; row2++;
            }
            foreach (var s in r.Saidas)
            {
                ws2.Cell(row2, 1).Value = r.Data.ToString("dd/MM/yyyy"); ws2.Cell(row2, 2).Value = "Saída";
                ws2.Cell(row2, 3).Value = s.Categoria ?? ""; ws2.Cell(row2, 4).Value = s.Descricao;
                ws2.Cell(row2, 5).Value = (double)s.Valor; row2++;
            }
        }
        ws2.Columns().AdjustToContents();

        // Aba 3: Métricas resumidas
        var ws3 = workbook.Worksheets.Add("Métricas");
        ws3.Cell(1, 1).Value = "Métrica"; ws3.Cell(1, 2).Value = "Valor";
        ws3.Row(1).Style.Font.Bold = true;
        var totalEnt = registros.Sum(r => r.Entradas.Sum(e => e.Valor));
        var totalSai = registros.Sum(r => r.Saidas.Sum(s => s.Valor));
        ws3.Cell(2, 1).Value = "Total Entradas"; ws3.Cell(2, 2).Value = (double)totalEnt;
        ws3.Cell(3, 1).Value = "Total Saídas"; ws3.Cell(3, 2).Value = (double)totalSai;
        ws3.Cell(4, 1).Value = "Lucro Operacional"; ws3.Cell(4, 2).Value = (double)(totalEnt - totalSai);
        ws3.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"relatorio_{de:yyyy-MM-dd}_a_{ate:yyyy-MM-dd}.xlsx");
    }

    [HttpGet("{clienteId:guid}/csv")]
    public async Task<IActionResult> ExportarCsv(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
    {
        var registros = await CarregarEValidar(clienteId, de, ate);

        var sb = new StringBuilder();
        sb.AppendLine("data,tipo,categoria,tipoCusto,descricao,valor");

        foreach (var r in registros)
        {
            foreach (var e in r.Entradas)
                sb.AppendLine($"{r.Data:yyyy-MM-dd},entrada,{e.Categoria ?? ""},{ e.TipoCusto ?? ""},\"{e.Descricao.Replace("\"", "\"\"")}\",{e.Valor:F2}");
            foreach (var s in r.Saidas)
                sb.AppendLine($"{r.Data:yyyy-MM-dd},saida,{s.Categoria ?? ""},{s.TipoCusto ?? ""},\"{s.Descricao.Replace("\"", "\"\"")}\",{s.Valor:F2}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"relatorio_{de:yyyy-MM-dd}_a_{ate:yyyy-MM-dd}.csv");
    }

    [HttpGet("{clienteId:guid}/pdf")]
    public async Task<IActionResult> ExportarPdf(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
    {
        var registros = await CarregarEValidar(clienteId, de, ate);

        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Relatório Financeiro — {de:dd/MM/yyyy} a {ate:dd/MM/yyyy}")
                    .SemiBold().FontSize(14).FontColor(Colors.Grey.Darken3);

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    // Tabela resumo diário
                    col.Item().Text("Resumo Diário").Bold().FontSize(11);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70); c.RelativeColumn(); c.RelativeColumn();
                            c.RelativeColumn(); c.RelativeColumn();
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Data").Bold();
                            h.Cell().Text("Entradas").Bold();
                            h.Cell().Text("Saídas").Bold();
                            h.Cell().Text("Lucro Op.").Bold();
                            h.Cell().Text("Saldo Final").Bold();
                        });

                        foreach (var r in registros)
                        {
                            var te = r.Entradas.Sum(e => e.Valor);
                            var ts = r.Saidas.Sum(s => s.Valor);
                            table.Cell().Text(r.Data.ToString("dd/MM/yyyy"));
                            table.Cell().Text($"R$ {te:N2}");
                            table.Cell().Text($"R$ {ts:N2}");
                            table.Cell().Text($"R$ {te - ts:N2}");
                            table.Cell().Text($"R$ {r.SaldoFinal:N2}");
                        }
                    });

                    // Totais
                    var totalE = registros.Sum(r => r.Entradas.Sum(e => e.Valor));
                    var totalS = registros.Sum(r => r.Saidas.Sum(s => s.Valor));
                    col.Item().Text($"Total Entradas: R$ {totalE:N2} | Total Saídas: R$ {totalS:N2} | Lucro: R$ {totalE - totalS:N2}").Bold();
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages();
                });
            });
        });

        var pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", $"relatorio_{de:yyyy-MM-dd}_a_{ate:yyyy-MM-dd}.pdf");
    }
}
```

- [ ] **Step 3: Build**

```
dotnet build CaixaDiario.API
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.API/ CaixaDiario.Tests/Services/MetricasServiceTests.cs
git commit -m "feat(sprint3): auditoria, exportações PDF/CSV/XLSX enriquecido"
```

---

## Task 5: Frontend — api/auditoria.ts + AdminAuditoriaPage

**Files:**
- Create: `frontend/src/api/auditoria.ts`
- Create: `frontend/src/pages/admin/AdminAuditoriaPage.tsx`
- Modify: `frontend/src/App.tsx`

- [ ] **Step 1: Criar `api/auditoria.ts`**

```typescript
import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface AuditLog {
  id: string
  clienteId: string
  usuarioId: string
  entidade: string
  acaoTipo: string
  entidadeId: string
  dadosAntes?: string
  dadosDepois?: string
  ocorridoEm: string
}

export interface AuditLogPaginado {
  items: AuditLog[]
  total: number
  pagina: number
  tamanhoPagina: number
}

export async function listarAuditoria(
  clienteId: string,
  params: { de?: string; ate?: string; entidade?: string; acao?: string; pagina?: number } = {}
): Promise<AuditLogPaginado> {
  const qs = new URLSearchParams()
  if (params.de) qs.set('de', params.de)
  if (params.ate) qs.set('ate', params.ate)
  if (params.entidade) qs.set('entidade', params.entidade)
  if (params.acao) qs.set('acao', params.acao)
  if (params.pagina) qs.set('pagina', String(params.pagina))

  const res = await apiFetch<ApiResponse<AuditLogPaginado>>(`/api/auditoria/${clienteId}?${qs.toString()}`)
  return res.dados
}
```

- [ ] **Step 2: Criar `AdminAuditoriaPage.tsx`**

```tsx
import { useState, useEffect } from 'react'
import { listarAuditoria } from '../../api/auditoria'
import type { AuditLog } from '../../api/auditoria'
import { fmtDate } from '../../utils/format'

interface Props { clienteId: string }

export default function AdminAuditoriaPage({ clienteId }: Props) {
  const [logs, setLogs] = useState<AuditLog[]>([])
  const [total, setTotal] = useState(0)
  const [pagina, setPagina] = useState(1)
  const [entidade, setEntidade] = useState('')
  const [acao, setAcao] = useState('')
  const [expanded, setExpanded] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    setLoading(true)
    listarAuditoria(clienteId, { entidade: entidade || undefined, acao: acao || undefined, pagina })
      .then(res => { setLogs(res.items); setTotal(res.total) })
      .catch(console.error)
      .finally(() => setLoading(false))
  }, [clienteId, entidade, acao, pagina])

  const totalPaginas = Math.ceil(total / 50)

  return (
    <div style={{ padding: 16 }}>
      <h3 style={{ marginBottom: 16 }}>📋 Histórico de Alterações</h3>

      <div style={{ display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
        <select value={entidade} onChange={e => { setEntidade(e.target.value); setPagina(1) }}
          style={{ padding: '6px 10px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)' }}>
          <option value="">Todas as entidades</option>
          <option value="RegistroDiario">Registro Diário</option>
          <option value="ContaRecorrente">Conta Recorrente</option>
          <option value="MetaAnual">Meta Anual</option>
        </select>
        <select value={acao} onChange={e => { setAcao(e.target.value); setPagina(1) }}
          style={{ padding: '6px 10px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)' }}>
          <option value="">Todas as ações</option>
          <option value="Criacao">Criação</option>
          <option value="Edicao">Edição</option>
          <option value="Exclusao">Exclusão</option>
        </select>
      </div>

      {loading && <p style={{ color: 'var(--tx3)' }}>Carregando...</p>}

      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr style={{ borderBottom: '1px solid var(--bd)' }}>
            <th style={{ padding: '8px 4px', textAlign: 'left', color: 'var(--tx3)' }}>Data/Hora</th>
            <th style={{ padding: '8px 4px', textAlign: 'left', color: 'var(--tx3)' }}>Entidade</th>
            <th style={{ padding: '8px 4px', textAlign: 'left', color: 'var(--tx3)' }}>Ação</th>
            <th style={{ padding: '8px 4px', textAlign: 'left', color: 'var(--tx3)' }}>Detalhes</th>
          </tr>
        </thead>
        <tbody>
          {logs.map(log => (
            <>
              <tr key={log.id} style={{ borderBottom: '1px solid var(--bd)' }}>
                <td style={{ padding: '8px 4px' }}>{new Date(log.ocorridoEm).toLocaleString('pt-BR')}</td>
                <td style={{ padding: '8px 4px' }}>{log.entidade}</td>
                <td style={{ padding: '8px 4px' }}>
                  <span style={{
                    padding: '2px 8px', borderRadius: 4, fontSize: 11, fontWeight: 600,
                    background: log.acaoTipo === 'Criacao' ? '#34c75920' : log.acaoTipo === 'Exclusao' ? '#ff3b3020' : '#0a84ff20',
                    color: log.acaoTipo === 'Criacao' ? '#34c759' : log.acaoTipo === 'Exclusao' ? '#ff3b30' : '#0a84ff',
                  }}>
                    {log.acaoTipo}
                  </span>
                </td>
                <td style={{ padding: '8px 4px' }}>
                  {(log.dadosAntes || log.dadosDepois) && (
                    <button onClick={() => setExpanded(expanded === log.id ? null : log.id)}
                      style={{ fontSize: 11, background: 'none', border: '1px solid var(--bd)', borderRadius: 4, padding: '2px 8px', cursor: 'pointer', color: 'var(--tx3)' }}>
                      {expanded === log.id ? 'Fechar' : 'Ver diff'}
                    </button>
                  )}
                </td>
              </tr>
              {expanded === log.id && (
                <tr key={`${log.id}-detail`}>
                  <td colSpan={4} style={{ padding: '8px 4px', background: 'var(--bg-card)' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                      {log.dadosAntes && (
                        <div>
                          <div style={{ fontSize: 11, color: 'var(--tx3)', marginBottom: 4 }}>Antes:</div>
                          <pre style={{ fontSize: 11, overflow: 'auto', maxHeight: 200, padding: 8, background: '#ff3b3010', borderRadius: 4 }}>
                            {JSON.stringify(JSON.parse(log.dadosAntes), null, 2)}
                          </pre>
                        </div>
                      )}
                      {log.dadosDepois && (
                        <div>
                          <div style={{ fontSize: 11, color: 'var(--tx3)', marginBottom: 4 }}>Depois:</div>
                          <pre style={{ fontSize: 11, overflow: 'auto', maxHeight: 200, padding: 8, background: '#34c75910', borderRadius: 4 }}>
                            {JSON.stringify(JSON.parse(log.dadosDepois), null, 2)}
                          </pre>
                        </div>
                      )}
                    </div>
                  </td>
                </tr>
              )}
            </>
          ))}
        </tbody>
      </table>

      {totalPaginas > 1 && (
        <div style={{ display: 'flex', gap: 8, marginTop: 16, justifyContent: 'center' }}>
          <button onClick={() => setPagina(p => Math.max(1, p - 1))} disabled={pagina === 1}
            style={{ padding: '6px 14px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)', cursor: 'pointer' }}>
            ← Anterior
          </button>
          <span style={{ padding: '6px 14px', color: 'var(--tx3)' }}>{pagina} / {totalPaginas}</span>
          <button onClick={() => setPagina(p => Math.min(totalPaginas, p + 1))} disabled={pagina === totalPaginas}
            style={{ padding: '6px 14px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)', cursor: 'pointer' }}>
            Próxima →
          </button>
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 3: Adicionar rota de auditoria em `App.tsx`**

Localizar onde as rotas de admin são definidas e adicionar a aba/rota de auditoria. O padrão exato depende de como `AdminClientsPage` usa tabs — abrir o arquivo e seguir o mesmo padrão.

Em `frontend/src/App.tsx`, localizar onde `AdminClientsPage` é renderizado. A `AdminAuditoriaPage` deve ser exibida como uma aba dentro do painel de cliente admin, passando o `clienteId`. Adicionar import e uso:

```tsx
import AdminAuditoriaPage from './pages/admin/AdminAuditoriaPage'
```

Na aba de cliente admin (onde há tabs de Dashboard, Caixa, etc.), adicionar uma aba "Auditoria" que renderiza `<AdminAuditoriaPage clienteId={clienteId} />`.

> **Nota:** O padrão exato de tabs está em `AdminClientsPage.tsx` — leia esse arquivo antes de editar para seguir o padrão de navegação existente.

---

## Task 6: Frontend — ClientExportacaoPage com 3 botões

**Files:**
- Modify: `frontend/src/pages/client/ClientExportacaoPage.tsx`

- [ ] **Step 1: Substituir `ClientExportacaoPage.tsx` inteiro**

```tsx
import { useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { apiFetch } from '../../api/client'
import './ClientExportacao.css'

interface Props { clienteIdOverride?: string }

export default function ClientExportacaoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null

  const hoje = new Date()
  const primeiroDia = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}-01`
  const ultimoDia = hoje.toISOString().slice(0, 10)

  const [de, setDe] = useState(primeiroDia)
  const [ate, setAte] = useState(ultimoDia)
  const [loadingXlsx, setLoadingXlsx] = useState(false)
  const [loadingPdf, setLoadingPdf] = useState(false)
  const [loadingCsv, setLoadingCsv] = useState(false)
  const [erro, setErro] = useState('')

  async function baixar(formato: 'xlsx' | 'pdf' | 'csv') {
    if (!clienteId) return
    setErro('')
    const setLoading = formato === 'xlsx' ? setLoadingXlsx : formato === 'pdf' ? setLoadingPdf : setLoadingCsv

    setLoading(true)
    try {
      const token = localStorage.getItem('token')
      const res = await fetch(`/api/export/${clienteId}/${formato}?de=${de}&ate=${ate}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      })
      if (!res.ok) throw new Error(`Erro ${res.status}`)

      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `relatorio_${de}_a_${ate}.${formato}`
      a.click()
      URL.revokeObjectURL(url)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao exportar')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div style={{ maxWidth: 480, margin: '0 auto' }}>
      <h3 style={{ marginBottom: 20 }}>📥 Exportar Relatório</h3>

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 14, padding: 20, marginBottom: 20 }}>
        <h4 style={{ marginBottom: 12, color: 'var(--tx3)', fontSize: 13, fontWeight: 600 }}>Período</h4>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <label style={{ fontSize: 13 }}>De <input type="date" value={de} onChange={e => setDe(e.target.value)} /></label>
          <label style={{ fontSize: 13 }}>Até <input type="date" value={ate} onChange={e => setAte(e.target.value)} /></label>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <button onClick={() => baixar('xlsx')} disabled={loadingXlsx}
          style={{ padding: '14px 20px', borderRadius: 12, border: 'none', background: '#34c759', color: '#fff', fontSize: 15, fontWeight: 600, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
          {loadingXlsx ? '⏳ Gerando...' : '📥 Baixar Excel (.xlsx)'}
        </button>

        <button onClick={() => baixar('pdf')} disabled={loadingPdf}
          style={{ padding: '14px 20px', borderRadius: 12, border: 'none', background: '#ff3b30', color: '#fff', fontSize: 15, fontWeight: 600, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
          {loadingPdf ? '⏳ Gerando...' : '📄 Baixar PDF'}
        </button>

        <button onClick={() => baixar('csv')} disabled={loadingCsv}
          style={{ padding: '14px 20px', borderRadius: 12, border: 'none', background: '#0a84ff', color: '#fff', fontSize: 15, fontWeight: 600, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
          {loadingCsv ? '⏳ Gerando...' : '📋 Baixar CSV'}
        </button>
      </div>

      {erro && <p style={{ marginTop: 12, color: '#ff3b30', fontSize: 13 }}>{erro}</p>}
    </div>
  )
}
```

- [ ] **Step 2: TypeScript check + todos os testes**

```
cd frontend && npx tsc --noEmit && npm test -- --run
```
Expected: No errors, all tests pass.

```
dotnet test CaixaDiario.Tests -v minimal
```
Expected: All tests passed.

- [ ] **Step 3: Commit Sprint 3**

```bash
git add frontend/src/ CaixaDiario.API/Repositories/Interfaces/IAuditRepository.cs CaixaDiario.API/Repositories/AuditRepository.cs CaixaDiario.API/DTOs/Auditoria/ CaixaDiario.API/Controllers/AuditoriaController.cs CaixaDiario.API/Program.cs CaixaDiario.API/CaixaDiario.API.csproj
git commit -m "feat(sprint3): auditoria UI, exportação PDF/CSV, cards Valuation/Runway/Liquidez"
```
