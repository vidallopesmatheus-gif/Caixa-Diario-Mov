# Design: Troca de Anthropic SDK para Groq (HttpClient)

**Data:** 2026-05-29
**Branch:** feature/chat-ia

## Contexto

O chat IA do Caixa Diário utiliza o Claude (Anthropic) como provedor. A conta da API Anthropic não possui créditos, pois a assinatura Claude Pro não inclui acesso à API. Para viabilizar o uso imediato sem custo, a solução é trocar para o Groq, que oferece tier gratuito generoso (30 req/min, 14.400 req/dia). O usuário pretende voltar ao Claude futuramente, então a interface `IAnthropicClient` e todo o código acima dela são preservados intactos.

## Objetivo

Substituir `AnthropicClientWrapper` por `GroqClientWrapper` implementando a mesma interface `IAnthropicClient`, usando `HttpClient` puro sem dependências adicionais.

## Arquitetura

O fluxo de dados não muda:

```
ChatController → ChatService → IAnthropicClient → [GroqClientWrapper] → api.groq.com
```

Apenas o wrapper concreto troca. Toda a lógica de guard-rails (`TopicGuard`), system prompt, histórico, DTOs e controller permanecem inalterados.

## Componentes Alterados

### 1. `GroqClientWrapper.cs` (novo)
- Localização: `CaixaDiario.API/Services/GroqClientWrapper.cs`
- Implementa `IAnthropicClient`
- Usa `HttpClient` injetado via DI
- Endpoint: `POST https://api.groq.com/openai/v1/chat/completions`
- Header: `Authorization: Bearer {apiKey}`
- Corpo da requisição (formato OpenAI-compatible):
  ```json
  {
    "model": "llama-3.3-70b-versatile",
    "max_tokens": 1024,
    "messages": [
      { "role": "system", "content": "{systemPrompt}" },
      ... histórico ...,
      { "role": "user", "content": "{mensagem}" }
    ]
  }
  ```
- Resposta extraída de: `choices[0].message.content`
- Em caso de erro HTTP: lança `HttpRequestException` com a resposta do Groq

### 2. `Program.cs` (alterado)
- Remover bloco de registro do `AnthropicClientWrapper`
- Adicionar registro do `GroqClientWrapper`:
  - Ler `Groq:ApiKey`, `Groq:Model`, `Groq:MaxTokens` da configuração
  - Registrar `HttpClient` via `AddHttpClient` ou usar `IHttpClientFactory`
  - Registrar `IAnthropicClient` como singleton com `GroqClientWrapper`

### 3. `appsettings.json` (alterado)
- Remover seção `Anthropic`
- Adicionar seção `Groq`:
  ```json
  "Groq": {
    "ApiKey": "",
    "Model": "llama-3.3-70b-versatile",
    "MaxTokens": 1024
  }
  ```

### 4. `.env` (alterado)
- Remover `Anthropic__ApiKey`
- Adicionar `Groq__ApiKey={chave do console.groq.com}`

### 5. `CaixaDiario.API.csproj` (alterado)
- Remover `<PackageReference Include="Anthropic.SDK" />`

## O que NÃO muda

- `IAnthropicClient` — interface preservada para facilitar retorno ao Claude
- `AnthropicClientWrapper` — mantido no código (não deletado), apenas desregistrado no DI
- `ChatService`, `TopicGuard`, `IChatService`, `ChatController`
- Todos os DTOs de chat
- Frontend

## Modelo Groq

`llama-3.3-70b-versatile` — melhor qualidade disponível no tier gratuito. Adequado para um assistente de suporte ao uso do app.

## Tratamento de Erros

- Resposta HTTP não-sucesso: lança `HttpRequestException` com o body da resposta do Groq
- O `ErrorHandlingMiddleware` existente captura e retorna erro 500 genérico ao cliente (comportamento atual mantido)

## Passos de Implementação

1. Adicionar `Groq__ApiKey` ao `.env` com chave do console.groq.com
2. Criar `GroqClientWrapper.cs`
3. Atualizar `appsettings.json` (seção Groq)
4. Atualizar `Program.cs` (trocar registro DI)
5. Remover `Anthropic.SDK` do `.csproj`
6. Testar endpoint `/api/chat` com curl
