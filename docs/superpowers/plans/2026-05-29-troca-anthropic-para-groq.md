# Troca Anthropic SDK → Groq (HttpClient) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Substituir `AnthropicClientWrapper` por `GroqClientWrapper` usando `HttpClient` puro, sem novas dependências NuGet, mantendo `IAnthropicClient` intacta para futura volta ao Claude.

**Architecture:** Novo `GroqClientWrapper` implementa `IAnthropicClient` via chamada REST ao endpoint OpenAI-compatible do Groq. Apenas o registro DI e a configuração mudam; todo o restante da pilha (ChatService, TopicGuard, controller, DTOs, frontend) permanece intocado.

**Tech Stack:** .NET 10, xUnit, Moq, HttpClient nativo, Groq REST API (`https://api.groq.com/openai/v1/chat/completions`)

---

## Mapa de Arquivos

| Ação | Arquivo |
|------|---------|
| Criar | `CaixaDiario.API/Services/GroqClientWrapper.cs` |
| Criar | `CaixaDiario.Tests/Services/GroqClientWrapperTests.cs` |
| Modificar | `CaixaDiario.API/appsettings.json` |
| Modificar | `CaixaDiario.API/.env` |
| Modificar | `CaixaDiario.API/Program.cs` (linhas 62-70) |
| Modificar | `CaixaDiario.API/CaixaDiario.API.csproj` |

---

## Task 1: Atualizar configuração (.env e appsettings.json)

**Files:**
- Modify: `CaixaDiario.API/.env`
- Modify: `CaixaDiario.API/appsettings.json`

- [ ] **Step 1: Substituir chave Anthropic pela chave Groq no .env**

Abrir `CaixaDiario.API/.env` e substituir a linha:
```
Anthropic__ApiKey=sk-ant-api03-...
```
por:
```
Groq__ApiKey=GROQ_API_KEY_PLACEHOLDER
```

- [ ] **Step 2: Substituir seção Anthropic pela seção Groq no appsettings.json**

Em `CaixaDiario.API/appsettings.json`, substituir:
```json
"Anthropic": {
  "ApiKey": "",
  "Model": "claude-haiku-4-5-20251001",
  "MaxTokens": 1024
}
```
por:
```json
"Groq": {
  "ApiKey": "",
  "Model": "llama-3.3-70b-versatile",
  "MaxTokens": 1024
}
```

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.API/.env CaixaDiario.API/appsettings.json
git commit -m "config: trocar configuração Anthropic por Groq"
```

---

## Task 2: Escrever testes para GroqClientWrapper (falham antes da implementação)

**Files:**
- Create: `CaixaDiario.Tests/Services/GroqClientWrapperTests.cs`

- [ ] **Step 1: Criar o arquivo de testes**

Criar `CaixaDiario.Tests/Services/GroqClientWrapperTests.cs` com o conteúdo:

```csharp
using System.Net;
using System.Text;
using CaixaDiario.API.DTOs.Chat;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class GroqClientWrapperTests
{
    private static HttpClient BuildClient(HttpStatusCode status, string body)
    {
        var handler = new FakeHandler(status, body);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task EnviarMensagemAsync_RespostaValida_RetornaConteudoDoAssistente()
    {
        var groqResponse = """
            {
                "choices": [
                    {
                        "message": {
                            "role": "assistant",
                            "content": "Para registrar uma entrada, acesse a tela Caixa."
                        }
                    }
                ]
            }
            """;
        var sut = new GroqClientWrapper(
            BuildClient(HttpStatusCode.OK, groqResponse),
            "test-key", "llama-3.3-70b-versatile", 1024);

        var result = await sut.EnviarMensagemAsync("system prompt", [], "como usar o app?");

        Assert.Equal("Para registrar uma entrada, acesse a tela Caixa.", result);
    }

    [Fact]
    public async Task EnviarMensagemAsync_ErroHTTP_LancaHttpRequestException()
    {
        var sut = new GroqClientWrapper(
            BuildClient(HttpStatusCode.Unauthorized, """{"error":{"message":"invalid api key"}}"""),
            "bad-key", "llama-3.3-70b-versatile", 1024);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.EnviarMensagemAsync("system", [], "mensagem"));
    }

    [Fact]
    public async Task EnviarMensagemAsync_ComHistorico_EnviaSystemPrimeiroDepoisHistoricoDepoisUser()
    {
        string? capturedBody = null;
        var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"ok"}}]}""",
            body => capturedBody = body);
        var sut = new GroqClientWrapper(new HttpClient(handler), "key", "model", 1024);

        var historico = new List<ChatMessageDto>
        {
            new() { Role = "user", Content = "primeira pergunta" },
            new() { Role = "assistant", Content = "primeira resposta" }
        };

        await sut.EnviarMensagemAsync("meu system prompt", historico, "nova pergunta");

        Assert.NotNull(capturedBody);
        using var doc = System.Text.Json.JsonDocument.Parse(capturedBody);
        var msgs = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(4, msgs.Count);
        Assert.Equal("system", msgs[0].GetProperty("role").GetString());
        Assert.Equal("user",   msgs[1].GetProperty("role").GetString());
        Assert.Equal("assistant", msgs[2].GetProperty("role").GetString());
        Assert.Equal("user",   msgs[3].GetProperty("role").GetString());
        Assert.Equal("meu system prompt", msgs[0].GetProperty("content").GetString());
        Assert.Equal("nova pergunta", msgs[3].GetProperty("content").GetString());
    }

    [Fact]
    public async Task EnviarMensagemAsync_EnviaModelEMaxTokensCorretos()
    {
        string? capturedBody = null;
        var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"ok"}}]}""",
            body => capturedBody = body);
        var sut = new GroqClientWrapper(new HttpClient(handler), "key", "llama-3.3-70b-versatile", 512);

        await sut.EnviarMensagemAsync("system", [], "msg");

        using var doc = System.Text.Json.JsonDocument.Parse(capturedBody!);
        Assert.Equal("llama-3.3-70b-versatile", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal(512, doc.RootElement.GetProperty("max_tokens").GetInt32());
    }
}

internal class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

internal class CapturingHandler(HttpStatusCode status, string body, Action<string> capture) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var requestBody = await request.Content!.ReadAsStringAsync(ct);
        capture(requestBody);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
```

- [ ] **Step 2: Confirmar que os testes falham (GroqClientWrapper não existe ainda)**

```bash
cd CaixaDiario.Tests
dotnet test --filter "GroqClientWrapperTests" -v minimal
```

Esperado: erro de compilação — `GroqClientWrapper não encontrado`.

---

## Task 3: Implementar GroqClientWrapper

**Files:**
- Create: `CaixaDiario.API/Services/GroqClientWrapper.cs`

- [ ] **Step 1: Criar o arquivo de implementação**

Criar `CaixaDiario.API/Services/GroqClientWrapper.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CaixaDiario.API.DTOs.Chat;

namespace CaixaDiario.API.Services;

public class GroqClientWrapper : IAnthropicClient
{
    private const string ApiUrl = "https://api.groq.com/openai/v1/chat/completions";

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly int _maxTokens;

    public GroqClientWrapper(HttpClient http, string apiKey, string model, int maxTokens)
    {
        _http = http;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _model = model;
        _maxTokens = maxTokens;
    }

    public async Task<string> EnviarMensagemAsync(
        string systemPrompt, List<ChatMessageDto> historico, string mensagem)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        foreach (var h in historico)
            messages.Add(new { role = h.Role, content = h.Content });

        messages.Add(new { role = "user", content = mensagem });

        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            max_tokens = _maxTokens,
            messages
        });

        var response = await _http.PostAsync(ApiUrl,
            new StringContent(body, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(error);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que passam**

```bash
cd CaixaDiario.Tests
dotnet test --filter "GroqClientWrapperTests" -v minimal
```

Esperado:
```
Passed! - Failed: 0, Passed: 4, Skipped: 0
```

- [ ] **Step 3: Rodar toda a suite para garantir que nada quebrou**

```bash
dotnet test -v minimal
```

Esperado: todos os testes passando.

- [ ] **Step 4: Commit**

```bash
git add CaixaDiario.Tests/Services/GroqClientWrapperTests.cs CaixaDiario.API/Services/GroqClientWrapper.cs
git commit -m "feat: implementar GroqClientWrapper com HttpClient nativo"
```

---

## Task 4: Atualizar Program.cs (trocar registro DI)

**Files:**
- Modify: `CaixaDiario.API/Program.cs` (linhas 62-70)

- [ ] **Step 1: Substituir o bloco de registro do Anthropic pelo Groq**

Em `CaixaDiario.API/Program.cs`, localizar e substituir o bloco:

```csharp
// Chat IA
var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"]
    ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("ANTHROPIC_API_KEY não configurada.");
var anthropicModel = builder.Configuration["Anthropic:Model"] ?? "claude-haiku-4-5-20251001";
var anthropicMaxTokens = int.TryParse(builder.Configuration["Anthropic:MaxTokens"], out var mt) ? mt : 1024;
builder.Services.AddSingleton<IAnthropicClient>(
    new AnthropicClientWrapper(anthropicApiKey, anthropicModel, anthropicMaxTokens));
builder.Services.AddScoped<IChatService, ChatService>();
```

pelo bloco:

```csharp
// Chat IA
var groqApiKey = builder.Configuration["Groq:ApiKey"]
    ?? throw new InvalidOperationException("Groq:ApiKey não configurada.");
var groqModel = builder.Configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
var groqMaxTokens = int.TryParse(builder.Configuration["Groq:MaxTokens"], out var mt) ? mt : 1024;
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAnthropicClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new GroqClientWrapper(http, groqApiKey, groqModel, groqMaxTokens);
});
builder.Services.AddScoped<IChatService, ChatService>();
```

- [ ] **Step 2: Build para garantir que compila**

```bash
cd CaixaDiario.API
dotnet build
```

Esperado: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.API/Program.cs
git commit -m "feat: trocar registro DI de AnthropicClientWrapper para GroqClientWrapper"
```

---

## Task 5: Remover Anthropic.SDK do .csproj

**Files:**
- Modify: `CaixaDiario.API/CaixaDiario.API.csproj`

- [ ] **Step 1: Remover a referência ao pacote Anthropic.SDK**

Em `CaixaDiario.API/CaixaDiario.API.csproj`, remover a linha:

```xml
<PackageReference Include="Anthropic.SDK" Version="5.10.0" />
```

- [ ] **Step 2: Build e testes para confirmar que nada depende do SDK removido**

```bash
cd CaixaDiario.API && dotnet build
cd ../CaixaDiario.Tests && dotnet test -v minimal
```

Esperado: build e testes passando sem o pacote.

- [ ] **Step 3: Commit**

```bash
git add CaixaDiario.API/CaixaDiario.API.csproj
git commit -m "chore: remover dependência Anthropic.SDK (substituído por Groq via HttpClient)"
```

---

## Task 6: Smoke test de ponta a ponta

**Files:** nenhum — apenas validação manual.

- [ ] **Step 1: Iniciar a API**

```bash
cd CaixaDiario.API
dotnet run --no-launch-profile
```

Esperado: `Now listening on: http://localhost:5000`

- [ ] **Step 2: Fazer login e guardar o token**

```bash
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"nomeUsuario":"CTI","senha":"Xk8!Qm"}' \
  | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
```

- [ ] **Step 3: Chamar o endpoint de chat**

```bash
curl -s -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  --data-binary '{"Message":"Como usar o app?","History":[]}'
```

Esperado: resposta JSON com `"wasBlocked": false` e `"reply"` contendo texto em português do Llama.

- [ ] **Step 4: Testar guard-rail (mensagem fora do escopo)**

```bash
curl -s -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  --data-binary '{"Message":"me dê uma receita de bolo","History":[]}'
```

Esperado: `"wasBlocked": true` — sem chamar a API do Groq.
