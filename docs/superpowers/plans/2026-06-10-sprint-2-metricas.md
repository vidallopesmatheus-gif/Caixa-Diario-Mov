# Sprint 2 — Fase 2: Gestão Financeira (Métricas)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **Prerequisite:** Sprint 1 completo (categorias persistidas no JSONB, contas recorrentes funcionando).

**Goal:** Implementar MetricasService com EBITDA, Prime Cost e Ponto de Equilíbrio; endpoints de evolução mensal e fluxo projetado; cards de métricas no Dashboard; gráfico de fluxo projetado; ClientGraficoPage com evolução 3/6/12 meses.

**Architecture:** `MetricasService` é uma classe de cálculo puro — recebe listas de `RegistroDiario` e `ContaRecorrente`, retorna DTOs. O `MetricasController` carrega os dados via repositórios e delega cálculos ao service. Isso torna o service 100% testável sem mock de DB. Frontend usa `useMetricas` hook separado para não poluir `ClientDashboardPage`.

**Tech Stack:** .NET 10, xUnit, React 19, TypeScript, Recharts (já instalado)

---

## Mapa de Arquivos

| Ação | Arquivo |
|------|---------|
| Create | `CaixaDiario.API/DTOs/Metricas/MetricasPeriodoDto.cs` |
| Create | `CaixaDiario.API/DTOs/Metricas/SemaforoDto.cs` |
| Create | `CaixaDiario.API/DTOs/Metricas/EvolucaoMensalDto.cs` |
| Create | `CaixaDiario.API/DTOs/Metricas/FluxoProjetadoDto.cs` |
| Create | `CaixaDiario.API/Services/IMetricasService.cs` |
| Create | `CaixaDiario.API/Services/MetricasService.cs` |
| Create | `CaixaDiario.API/Controllers/MetricasController.cs` |
| Modify | `CaixaDiario.API/Program.cs` |
| Create | `CaixaDiario.Tests/Services/MetricasServiceTests.cs` |
| Create | `frontend/src/api/metricas.ts` |
| Create | `frontend/src/hooks/useMetricas.ts` |
| Modify | `frontend/src/pages/client/ClientDashboardPage.tsx` |
| Modify | `frontend/src/pages/client/ClientGraficoPage.tsx` |

---

## Task 1: DTOs de Métricas

**Files:**
- Create: `CaixaDiario.API/DTOs/Metricas/MetricasPeriodoDto.cs`
- Create: `CaixaDiario.API/DTOs/Metricas/EvolucaoMensalDto.cs`
- Create: `CaixaDiario.API/DTOs/Metricas/FluxoProjetadoDto.cs`

- [ ] **Step 1: Criar pasta e `MetricasPeriodoDto.cs`**

```csharp
namespace CaixaDiario.API.DTOs.Metricas;

public class MetricasPeriodoDto
{
    public EbitdaDto? Ebitda { get; set; }
    public PrimeCostDto? PrimeCost { get; set; }
    public PontoDeEquilibrioDto? PontoDeEquilibrio { get; set; }
    public decimal SaldoProjetado { get; set; }
    // Sprint 3 additions (nullable = not yet implemented)
    public ValuationDto? Valuation { get; set; }
    public RunwayDto? Runway { get; set; }
    public LiquidezDto? Liquidez { get; set; }
}

public class EbitdaDto
{
    public decimal Valor { get; set; }
    public decimal? Percentual { get; set; }
    public string Semaforo { get; set; } = "cinza"; // "verde" | "amarelo" | "vermelho" | "cinza"
}

public class PrimeCostDto
{
    public decimal? Percentual { get; set; }
    public string Semaforo { get; set; } = "cinza";
}

public class PontoDeEquilibrioDto
{
    public decimal Valor { get; set; }
    public decimal Receita { get; set; }
    public string Semaforo { get; set; } = "cinza";
}

public class ValuationDto
{
    public decimal Valor { get; set; }
    public string Semaforo { get; set; } = "cinza";
}

public class RunwayDto
{
    public decimal Meses { get; set; }
    public string Semaforo { get; set; } = "cinza";
}

public class LiquidezDto
{
    public decimal? Indice { get; set; }
    public bool AltaLiquidez { get; set; }
    public string Semaforo { get; set; } = "cinza";
}
```

- [ ] **Step 2: Criar `EvolucaoMensalDto.cs`**

```csharp
namespace CaixaDiario.API.DTOs.Metricas;

public class EvolucaoMensalDto
{
    public string Mes { get; set; } = string.Empty; // "2026-01"
    public decimal Receita { get; set; }
    public decimal Custos { get; set; }
    public decimal Lucro { get; set; }
    public decimal Saldo { get; set; }
}
```

- [ ] **Step 3: Criar `FluxoProjetadoDto.cs`**

```csharp
namespace CaixaDiario.API.DTOs.Metricas;

public class FluxoProjetadoDto
{
    public decimal SaldoAtual { get; set; }
    public List<FluxoDiaDto> Dias { get; set; } = new();
}

public class FluxoDiaDto
{
    public DateOnly Data { get; set; }
    public decimal SaldoProjetado { get; set; }
}
```

---

## Task 2: IMetricasService + MetricasService

**Files:**
- Create: `CaixaDiario.API/Services/IMetricasService.cs`
- Create: `CaixaDiario.API/Services/MetricasService.cs`

- [ ] **Step 1: Criar `IMetricasService.cs`**

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

- [ ] **Step 2: Escrever os testes primeiro**

`CaixaDiario.Tests/Services/MetricasServiceTests.cs`:

```csharp
using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class MetricasServiceTests
{
    private readonly MetricasService _sut = new();

    private static RegistroDiario CriarRegistro(DateOnly data, List<ItemFinanceiro> entradas, List<ItemFinanceiro> saidas, decimal saldoFinal = 0) =>
        new() { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = data, Entradas = entradas, Saidas = saidas, SaldoFinal = saldoFinal };

    private static ItemFinanceiro Item(string desc, decimal valor, string? categoria = null, string? tipoCusto = null) =>
        new() { Descricao = desc, Valor = valor, Categoria = categoria, TipoCusto = tipoCusto };

    // ---- EBITDA ----

    [Fact]
    public void CalcularPeriodo_SemCategorias_EbitdaNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m) },
            new() { Item("Aluguel", 300m) });

        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });

        Assert.Null(resultado.Ebitda);
    }

    [Fact]
    public void CalcularPeriodo_ComCategorias_CalculaEbitdaCorreto()
    {
        // EBITDA = Receita - CustoFixo(exceto Manutenção) - CustoVariavel
        // Receita = 1000, CustoFixo(Aluguel) = 300, CustoVariavel(Insumos) = 200
        // EBITDA = 1000 - 300 - 200 = 500 (50%)
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 300m, "Aluguel", "CustoFixo"),
                Item("Insumos", 200m, "Insumos/Mercadoria", "CustoVariavel"),
            });

        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });

        Assert.NotNull(resultado.Ebitda);
        Assert.Equal(500m, resultado.Ebitda!.Valor);
        Assert.Equal(0.5m, resultado.Ebitda.Percentual);
        Assert.Equal("verde", resultado.Ebitda.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_ManutencaoNaoEntraNoEbitda()
    {
        // Manutenção é proxy de depreciação — somada de volta ao EBITDA
        // Receita = 1000, Manutenção = 200 → não subtrai
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Manutenção", 200m, "Manutenção", "CustoFixo") });

        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });

        Assert.NotNull(resultado.Ebitda);
        Assert.Equal(1000m, resultado.Ebitda!.Valor);
    }

    // ---- Prime Cost ----

    [Fact]
    public void CalcularPeriodo_SemSalariosEInsumos_PrimeCostNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") });

        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });

        Assert.Null(resultado.PrimeCost);
    }

    [Fact]
    public void CalcularPeriodo_ComSalariosEInsumos_CalculaPrimeCostCorreto()
    {
        // PrimeCost = (Salários + Insumos) / Receita = (400+300)/1000 = 70%
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Salários", 400m, "Salários/Folha", "CustoFixo"),
                Item("Insumos", 300m, "Insumos/Mercadoria", "CustoVariavel"),
            });

        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });

        Assert.NotNull(resultado.PrimeCost);
        Assert.Equal(0.7m, resultado.PrimeCost!.Percentual);
        Assert.Equal("amarelo", resultado.PrimeCost.Semaforo);
    }

    // ---- Ponto de Equilíbrio ----

    [Fact]
    public void CalcularPeriodo_SemCategoria_PontoDeEquilibrioNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m) },
            new() { Item("Aluguel", 300m) });

        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });

        Assert.Null(resultado.PontoDeEquilibrio);
    }

    [Fact]
    public void CalcularPeriodo_ComCategorias_CalculaPontoDeEquilibrioCorreto()
    {
        // Receita=1000, CustoFixo=300, CustoVariavel=200
        // MC% = (1000-200)/1000 = 0.8
        // PE = 300 / 0.8 = 375
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 300m, "Aluguel", "CustoFixo"),
                Item("Insumos", 200m, "Insumos/Mercadoria", "CustoVariavel"),
            });

        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });

        Assert.NotNull(resultado.PontoDeEquilibrio);
        Assert.Equal(375m, resultado.PontoDeEquilibrio!.Valor);
        Assert.Equal("verde", resultado.PontoDeEquilibrio.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_ReceitaZero_NaoDividePorZero()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1), new(), new());
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Null(resultado.Ebitda);
        Assert.Null(resultado.PrimeCost);
        Assert.Null(resultado.PontoDeEquilibrio);
    }

    // ---- Evolução ----

    [Fact]
    public void CalcularEvolucao_RetornaMesesCorretos()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje.AddDays(-5),
                new() { Item("Venda", 1000m, "Vendas", "Receita") },
                new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") },
                saldoFinal: 5000m),
        };

        var resultado = _sut.CalcularEvolucao(registros, 3);

        Assert.Equal(3, resultado.Count);
        var mesAtual = resultado.Last();
        Assert.Equal(1000m, mesAtual.Receita);
        Assert.Equal(300m, mesAtual.Custos);
        Assert.Equal(700m, mesAtual.Lucro);
    }

    // ---- Fluxo Projetado ----

    [Fact]
    public void CalcularFluxoProjetado_SemContasFuturas_SaldoConstante()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = CriarRegistro(hoje, new(), new(), saldoFinal: 1000m);

        var resultado = _sut.CalcularFluxoProjetado(new() { registro }, new(), 3);

        Assert.Equal(1000m, resultado.SaldoAtual);
        Assert.Equal(3, resultado.Dias.Count);
        Assert.All(resultado.Dias, d => Assert.Equal(1000m, d.SaldoProjetado));
    }
}
```

- [ ] **Step 3: Executar para ver falhar**

```
dotnet test CaixaDiario.Tests --filter "MetricasServiceTests" -v minimal
```
Expected: FAIL — MetricasService não existe.

- [ ] **Step 4: Criar `MetricasService.cs`**

```csharp
using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class MetricasService : IMetricasService
{
    public MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo)
    {
        var entradas = registrosDoPeriodo.SelectMany(r => r.Entradas).ToList();
        var saidas = registrosDoPeriodo.SelectMany(r => r.Saidas).ToList();

        var receita = entradas.Where(e => e.TipoCusto == "Receita").Sum(e => e.Valor);
        var custosFixos = saidas.Where(s => s.TipoCusto == "CustoFixo" && s.Categoria != "Manutenção").Sum(s => s.Valor);
        var custosVariaveis = saidas.Where(s => s.TipoCusto == "CustoVariavel").Sum(s => s.Valor);

        var temCategoria = entradas.Any(e => e.Categoria != null) || saidas.Any(s => s.Categoria != null);

        var dto = new MetricasPeriodoDto();

        if (temCategoria && receita > 0)
        {
            var ebitdaValor = receita - custosFixos - custosVariaveis;
            var ebitdaPerc = ebitdaValor / receita;
            dto.Ebitda = new EbitdaDto
            {
                Valor = ebitdaValor,
                Percentual = ebitdaPerc,
                Semaforo = ebitdaPerc >= 0.15m ? "verde" : ebitdaPerc >= 0.05m ? "amarelo" : "vermelho",
            };

            var salarios = saidas.Where(s => s.Categoria == "Salários/Folha").Sum(s => s.Valor);
            var insumos = saidas.Where(s => s.Categoria == "Insumos/Mercadoria").Sum(s => s.Valor);
            if (salarios > 0 || insumos > 0)
            {
                var primeCostPerc = (salarios + insumos) / receita;
                dto.PrimeCost = new PrimeCostDto
                {
                    Percentual = primeCostPerc,
                    Semaforo = primeCostPerc < 0.6m ? "verde" : primeCostPerc <= 0.75m ? "amarelo" : "vermelho",
                };
            }

            if (custosFixos > 0 || custosVariaveis > 0)
            {
                var mc = (receita - custosVariaveis) / receita;
                var pe = mc > 0 ? custosFixos / mc : 0;
                dto.PontoDeEquilibrio = new PontoDeEquilibrioDto
                {
                    Valor = pe,
                    Receita = receita,
                    Semaforo = receita >= pe * 1.2m ? "verde" : receita >= pe ? "amarelo" : "vermelho",
                };
            }
        }

        var saldoAtual = todosRegistros.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;
        var totalReceber = todosRegistros.SelectMany(r => r.ContasReceber).Where(c => !c.Pago).Sum(c => c.Valor);
        var totalPagar = todosRegistros.SelectMany(r => r.ContasPagar).Where(c => !c.Pago).Sum(c => c.Valor);
        dto.SaldoProjetado = saldoAtual + totalReceber - totalPagar;

        return dto;
    }

    public List<EvolucaoMensalDto> CalcularEvolucao(List<RegistroDiario> registros, int meses)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var resultado = new List<EvolucaoMensalDto>();

        for (int i = meses - 1; i >= 0; i--)
        {
            var ref_ = hoje.AddMonths(-i);
            var prefixo = $"{ref_.Year}-{ref_.Month:D2}";
            var doMes = registros.Where(r => r.Data.ToString("yyyy-MM").StartsWith(prefixo)).ToList();

            var receita = doMes.SelectMany(r => r.Entradas).Sum(e => e.Valor);
            var custos = doMes.SelectMany(r => r.Saidas).Sum(s => s.Valor);
            var saldo = doMes.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;

            resultado.Add(new EvolucaoMensalDto
            {
                Mes = prefixo,
                Receita = receita,
                Custos = custos,
                Lucro = receita - custos,
                Saldo = saldo,
            });
        }

        return resultado;
    }

    public FluxoProjetadoDto CalcularFluxoProjetado(List<RegistroDiario> registros, List<ContaRecorrente> recorrentes, int dias)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var saldoAtual = registros.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;

        var fluxoDias = new List<FluxoDiaDto>();
        var saldoCorrendo = saldoAtual;

        for (int d = 1; d <= dias; d++)
        {
            var dia = hoje.AddDays(d);

            var entradas = registros.SelectMany(r => r.ContasReceber)
                .Where(c => !c.Pago && c.DataVencimento == dia).Sum(c => c.Valor);

            var saidas = registros.SelectMany(r => r.ContasPagar)
                .Where(c => !c.Pago && c.DataVencimento == dia).Sum(c => c.Valor);

            var entradasRec = recorrentes.Where(r => r.Tipo == "Receber" && r.Ativo &&
                r.DataInicio <= dia && (r.DataFim == null || r.DataFim >= dia) &&
                r.DataInicio.Day == dia.Day).Sum(r => r.Valor);

            var saidasRec = recorrentes.Where(r => r.Tipo == "Pagar" && r.Ativo &&
                r.DataInicio <= dia && (r.DataFim == null || r.DataFim >= dia) &&
                r.DataInicio.Day == dia.Day).Sum(r => r.Valor);

            saldoCorrendo += entradas + entradasRec - saidas - saidasRec;

            fluxoDias.Add(new FluxoDiaDto { Data = dia, SaldoProjetado = saldoCorrendo });
        }

        return new FluxoProjetadoDto { SaldoAtual = saldoAtual, Dias = fluxoDias };
    }
}
```

- [ ] **Step 5: Executar testes**

```
dotnet test CaixaDiario.Tests --filter "MetricasServiceTests" -v minimal
```
Expected: All passed.

---

## Task 3: MetricasController + DI

**Files:**
- Create: `CaixaDiario.API/Controllers/MetricasController.cs`
- Modify: `CaixaDiario.API/Program.cs`

- [ ] **Step 1: Criar `MetricasController.cs`**

```csharp
using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/metricas")]
[Authorize]
public class MetricasController : ControllerBase
{
    private readonly IMetricasService _metricasService;
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaRecorrenteRepository _contaRecorrenteRepo;

    public MetricasController(IMetricasService metricasService, IRegistroRepository registroRepo, IContaRecorrenteRepository contaRecorrenteRepo)
    {
        _metricasService = metricasService;
        _registroRepo = registroRepo;
        _contaRecorrenteRepo = contaRecorrenteRepo;
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    private void VerificarAcesso(Guid clienteId)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> ObterMetricas(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
    {
        VerificarAcesso(clienteId);
        var todos = await _registroRepo.ListarPorClienteAsync(clienteId);
        var doPeriodo = todos.Where(r => r.Data >= de && r.Data <= ate).ToList();
        var resultado = _metricasService.CalcularPeriodo(todos, doPeriodo);
        return Ok(new ApiResponse<MetricasPeriodoDto> { Dados = resultado });
    }

    [HttpGet("{clienteId:guid}/evolucao")]
    public async Task<IActionResult> ObterEvolucao(Guid clienteId, [FromQuery] int meses = 12)
    {
        VerificarAcesso(clienteId);
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var resultado = _metricasService.CalcularEvolucao(registros, meses);
        return Ok(new ApiResponse<List<EvolucaoMensalDto>> { Dados = resultado });
    }

    [HttpGet("{clienteId:guid}/fluxo-projetado")]
    public async Task<IActionResult> ObterFluxoProjetado(Guid clienteId, [FromQuery] int dias = 90)
    {
        VerificarAcesso(clienteId);
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _contaRecorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var resultado = _metricasService.CalcularFluxoProjetado(registros, recorrentes, dias);
        return Ok(new ApiResponse<FluxoProjetadoDto> { Dados = resultado });
    }
}
```

- [ ] **Step 2: Registrar no DI (Program.cs)**

Após `builder.Services.AddScoped<IContaRecorrenteService, ContaRecorrenteService>();`, adicionar:
```csharp
builder.Services.AddScoped<IMetricasService, MetricasService>();
```

- [ ] **Step 3: Build + todos os testes**

```
dotnet build CaixaDiario.API && dotnet test CaixaDiario.Tests -v minimal
```
Expected: Build succeeded + All tests passed.

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.API/DTOs/Metricas/ CaixaDiario.API/Services/IMetricasService.cs CaixaDiario.API/Services/MetricasService.cs CaixaDiario.API/Controllers/MetricasController.cs CaixaDiario.API/Program.cs CaixaDiario.Tests/Services/MetricasServiceTests.cs
git commit -m "feat(sprint2): MetricasService, MetricasController, testes EBITDA/PrimeCost/PE"
```

---

## Task 4: Frontend — api/metricas.ts + useMetricas hook

**Files:**
- Create: `frontend/src/api/metricas.ts`
- Create: `frontend/src/hooks/useMetricas.ts`

- [ ] **Step 1: Criar `api/metricas.ts`**

```typescript
import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface EbitdaMetrica {
  valor: number
  percentual?: number
  semaforo: string
}

export interface PrimeCostMetrica {
  percentual?: number
  semaforo: string
}

export interface PontoDeEquilibrioMetrica {
  valor: number
  receita: number
  semaforo: string
}

export interface ValuationMetrica {
  valor: number
  semaforo: string
}

export interface RunwayMetrica {
  meses: number
  semaforo: string
}

export interface LiquidezMetrica {
  indice?: number
  altaLiquidez: boolean
  semaforo: string
}

export interface MetricasPeriodo {
  ebitda?: EbitdaMetrica
  primeCost?: PrimeCostMetrica
  pontoDeEquilibrio?: PontoDeEquilibrioMetrica
  saldoProjetado: number
  valuation?: ValuationMetrica
  runway?: RunwayMetrica
  liquidez?: LiquidezMetrica
}

export interface EvolucaoMensal {
  mes: string
  receita: number
  custos: number
  lucro: number
  saldo: number
}

export interface FluxoDia {
  data: string
  saldoProjetado: number
}

export interface FluxoProjetado {
  saldoAtual: number
  dias: FluxoDia[]
}

export async function obterMetricas(clienteId: string, de: string, ate: string): Promise<MetricasPeriodo> {
  const res = await apiFetch<ApiResponse<MetricasPeriodo>>(`/api/metricas/${clienteId}?de=${de}&ate=${ate}`)
  return res.dados
}

export async function obterEvolucao(clienteId: string, meses = 12): Promise<EvolucaoMensal[]> {
  const res = await apiFetch<ApiResponse<EvolucaoMensal[]>>(`/api/metricas/${clienteId}/evolucao?meses=${meses}`)
  return res.dados
}

export async function obterFluxoProjetado(clienteId: string, dias = 90): Promise<FluxoProjetado> {
  const res = await apiFetch<ApiResponse<FluxoProjetado>>(`/api/metricas/${clienteId}/fluxo-projetado?dias=${dias}`)
  return res.dados
}
```

- [ ] **Step 2: Criar `hooks/useMetricas.ts`**

```typescript
import { useState, useEffect, useCallback } from 'react'
import { obterMetricas, obterFluxoProjetado } from '../api/metricas'
import type { MetricasPeriodo, FluxoProjetado } from '../api/metricas'

export function useMetricas(clienteId: string | null, de: string, ate: string) {
  const [metricas, setMetricas] = useState<MetricasPeriodo | null>(null)
  const [fluxo, setFluxo] = useState<FluxoProjetado | null>(null)
  const [loading, setLoading] = useState(false)

  const carregar = useCallback(async () => {
    if (!clienteId || !de || !ate) return
    setLoading(true)
    try {
      const [m, f] = await Promise.all([
        obterMetricas(clienteId, de, ate),
        obterFluxoProjetado(clienteId, 30),
      ])
      setMetricas(m)
      setFluxo(f)
    } catch (e) {
      console.error(e)
    } finally {
      setLoading(false)
    }
  }, [clienteId, de, ate])

  useEffect(() => { carregar() }, [carregar])

  return { metricas, fluxo, loading }
}
```

---

## Task 5: Frontend — Dashboard com cards de métricas + gráfico de fluxo

**Files:**
- Modify: `frontend/src/pages/client/ClientDashboardPage.tsx`

- [ ] **Step 1: Adicionar imports no topo do arquivo**

```typescript
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import { useMetricas } from '../../hooks/useMetricas'
import { fmtBRL } from '../../utils/format'
```

- [ ] **Step 2: Adicionar hook de métricas no componente**

Após `const { registros, loading } = useRegistros(clienteId)`, adicionar:
```typescript
const { metricas, fluxo } = useMetricas(clienteId, de, ate)
```

- [ ] **Step 3: Adicionar cards de métricas e gráfico de fluxo no JSX**

Após o bloco `</div>` do saldo projetado existente (antes do bloco de metas), adicionar:

```tsx
{metricas && (
  <div className="stats-grid" style={{ marginTop: 16 }}>
    {metricas.ebitda && (
      <StatCard
        label={`📊 EBITDA ${metricas.ebitda.semaforo === 'verde' ? '🟢' : metricas.ebitda.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
        value={`${fmtBRL(metricas.ebitda.valor)} (${((metricas.ebitda.percentual ?? 0) * 100).toFixed(1)}%)`}
        className={metricas.ebitda.semaforo === 'verde' ? 'val-green' : metricas.ebitda.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
      />
    )}
    {metricas.primeCost?.percentual != null && (
      <StatCard
        label={`🍽️ Prime Cost ${metricas.primeCost.semaforo === 'verde' ? '🟢' : metricas.primeCost.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
        value={`${((metricas.primeCost.percentual) * 100).toFixed(1)}%`}
        className={metricas.primeCost.semaforo === 'verde' ? 'val-green' : metricas.primeCost.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
      />
    )}
    {metricas.pontoDeEquilibrio && (
      <StatCard
        label={`⚖️ Ponto de Equilíbrio ${metricas.pontoDeEquilibrio.semaforo === 'verde' ? '🟢' : metricas.pontoDeEquilibrio.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
        value={fmtBRL(metricas.pontoDeEquilibrio.valor)}
        className={metricas.pontoDeEquilibrio.semaforo === 'verde' ? 'val-green' : metricas.pontoDeEquilibrio.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
      />
    )}
  </div>
)}

{fluxo && fluxo.dias.length > 0 && (
  <div className="meta-card">
    <h3>📈 Fluxo de Caixa Projetado (30 dias)</h3>
    <div style={{ height: 200 }}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={fluxo.dias.map(d => ({ dia: d.data.slice(5), saldo: d.saldoProjetado }))}>
          <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
          <XAxis dataKey="dia" stroke="var(--tx3)" tick={{ fontSize: 11 }} interval={4} />
          <YAxis stroke="var(--tx3)" tick={{ fontSize: 11 }} tickFormatter={v => `R$${(v/1000).toFixed(0)}k`} />
          <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
          <Line type="monotone" dataKey="saldo" stroke="#0a84ff" strokeWidth={2} dot={false} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  </div>
)}
```

- [ ] **Step 4: TypeScript check**

```
cd frontend && npx tsc --noEmit
```
Expected: No errors.

---

## Task 6: Frontend — ClientGraficoPage refatorada (evolução 3/6/12 meses)

**Files:**
- Modify: `frontend/src/pages/client/ClientGraficoPage.tsx`

- [ ] **Step 1: Substituir `ClientGraficoPage.tsx` inteiro**

```tsx
import { useState, useEffect } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL } from '../../utils/format'
import { obterEvolucao } from '../../api/metricas'
import type { EvolucaoMensal } from '../../api/metricas'
import {
  BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, Legend,
} from 'recharts'

interface Props { clienteIdOverride?: string }

const OPCOES_MESES = [3, 6, 12] as const

export default function ClientGraficoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const [meses, setMeses] = useState<3 | 6 | 12>(6)
  const [evolucao, setEvolucao] = useState<EvolucaoMensal[]>([])
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    setLoading(true)
    obterEvolucao(clienteId, meses)
      .then(setEvolucao)
      .catch(console.error)
      .finally(() => setLoading(false))
  }, [clienteId, meses])

  const data = evolucao.map(e => ({
    mes: e.mes.slice(2), // "26-06"
    receita: e.receita,
    custos: e.custos,
    lucro: e.lucro,
    saldo: e.saldo,
  }))

  const totalReceita = evolucao.reduce((s, e) => s + e.receita, 0)
  const totalCustos = evolucao.reduce((s, e) => s + e.custos, 0)
  const totalLucro = evolucao.reduce((s, e) => s + e.lucro, 0)
  const saldoAtual = evolucao[evolucao.length - 1]?.saldo ?? 0

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        {OPCOES_MESES.map(m => (
          <button key={m} onClick={() => setMeses(m)}
            style={{ padding: '6px 14px', borderRadius: 8, border: '1px solid var(--bd)', cursor: 'pointer',
              background: meses === m ? '#0a84ff' : 'var(--bg-card)', color: meses === m ? '#fff' : 'var(--tx1)', fontSize: 13 }}>
            {m} meses
          </button>
        ))}
      </div>

      <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14, color: '#888' }}>📊 Receita vs. Custos (barras)</h3>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 14, padding: 20, marginBottom: 24, height: 280 }}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
            <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 12 }} />
            <YAxis stroke="var(--tx3)" tick={{ fontSize: 12 }} tickFormatter={v => `R$${(v/1000).toFixed(0)}k`} />
            <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
            <Legend />
            <Bar dataKey="receita" name="Receita" fill="#34c759" radius={[4,4,0,0]} />
            <Bar dataKey="custos" name="Custos" fill="#ff3b30" radius={[4,4,0,0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14, color: '#888' }}>📈 Lucro Operacional (linha)</h3>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 14, padding: 20, marginBottom: 24, height: 200 }}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
            <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 12 }} />
            <YAxis stroke="var(--tx3)" tick={{ fontSize: 12 }} tickFormatter={v => `R$${(v/1000).toFixed(0)}k`} />
            <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
            <Line type="monotone" dataKey="lucro" name="Lucro Op." stroke="#ffd60a" strokeWidth={2} dot={{ fill: '#ffd60a' }} />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <div className="stats-grid">
        <StatCard label="📈 Receita Total" value={fmtBRL(totalReceita)} className="val-green" />
        <StatCard label="💸 Custos Totais" value={fmtBRL(totalCustos)} className="val-red" />
        <StatCard label="📊 Lucro Total" value={fmtBRL(totalLucro)} className={totalLucro >= 0 ? 'val-green' : 'val-red'} />
        <StatCard label="💰 Saldo Atual" value={fmtBRL(saldoAtual)} className="val-blue" />
      </div>
    </>
  )
}
```

- [ ] **Step 2: TypeScript check + testes**

```
cd frontend && npx tsc --noEmit && npm test -- --run
```
Expected: No TS errors, all tests pass.

- [ ] **Step 3: Commit Sprint 2**

```bash
git add frontend/src/api/metricas.ts frontend/src/hooks/useMetricas.ts frontend/src/pages/client/ClientDashboardPage.tsx frontend/src/pages/client/ClientGraficoPage.tsx
git commit -m "feat(sprint2): cards EBITDA/PrimeCost/PE, fluxo projetado, gráfico evolução"
```
