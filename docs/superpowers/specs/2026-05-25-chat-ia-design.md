# Design: Chat com IA — Caixa Diário

**Data:** 2026-05-25  
**Status:** Aprovado

---

## Resumo

Adicionar um assistente de IA com chat ao Caixa Diário, acessível via botão flutuante em todas as páginas autenticadas. O assistente responde apenas perguntas relacionadas ao app (registros, contas, metas, dashboard, exportação). Perguntas fora do escopo são bloqueadas antes de chegar à API da IA.

---

## Decisões de design

| Decisão | Escolha | Motivo |
|---|---|---|
| Provedor de IA | Claude Haiku 3.5 (Anthropic) | Menor custo, suficiente para Q&A |
| Guard-rails | Pré-filtro + System Prompt | Defesa em profundidade, economia de tokens |
| Histórico | Stateless (em memória) | Zero custo de banco, simplicidade para v1 |
| UI | Botão flutuante + painel deslizante | Disponível em todas as páginas, não intrusivo |
| Acesso a dados | Nenhum (v1) | Simplicidade; v2 pode injetar contexto no system prompt |

---

## Arquitetura

```
Frontend (React)                Backend (.NET 10)               Anthropic API
─────────────────               ──────────────────              ─────────────
ChatButton (flutuante)
  └─ abre ChatPanel
       └─ useChat hook
            └─ POST /api/chat ──► ChatController
               { message,               └─ ChatService
                 history[] }                  ├─ TopicGuard.IsOffTopic()
                                              │    ├─ [off-topic] → resposta padrão (sem chamar API)
                                              │    └─ [ok] ──────────────────────────────────►
                                              │                                  Claude Haiku 3.5
                                              │                                  (system prompt +
                                              │                                   histórico + msg)
                                              └─ retorna ChatResponseDto  ◄──────────────────
```

---

## Backend

### Novos arquivos

```
CaixaDiario.API/
  Controllers/
    ChatController.cs          POST /api/chat — requer JWT
  DTOs/Chat/
    ChatRequestDto.cs          { Message: string, History: ChatMessageDto[] }
    ChatResponseDto.cs         { Reply: string, WasBlocked: bool }
    ChatMessageDto.cs          { Role: string, Content: string }
  Services/
    IChatService.cs
    ChatService.cs             Orquestra TopicGuard + chamada Anthropic
    TopicGuard.cs              Pré-filtro stateless por regex
```

### `ChatController`

- `[Authorize]` — somente usuários autenticados
- `POST /api/chat` recebe `ChatRequestDto`, retorna `ChatResponseDto`
- Sem estado próprio; delega para `ChatService`

### `ChatService`

1. Chama `TopicGuard.IsOffTopic(message)`
2. Se bloqueado: retorna `{ Reply: "<msg padrão>", WasBlocked: true }` sem chamar a API
3. Se ok: monta request para Anthropic com system prompt + histórico (últimas 10 msgs) + mensagem atual
4. Retorna resposta da IA

### `TopicGuard`

Método estático `IsOffTopic(string message) → bool`.

Padrões bloqueados (regex, case-insensitive):

```
receita(s)? de|bolo|culinária|cozinha
previsão do tempo|clima em|temperatura em
futebol|basquete|vôlei|esporte(s)?
notícia(s)|política|eleição|presidente
como instalar|tutorial de|programar em
```

Retorna `true` se qualquer padrão bater na mensagem.

### System Prompt (enviado em toda requisição à Claude)

```
Você é o assistente do Caixa Diário, um aplicativo de controle
financeiro pessoal. Você responde APENAS perguntas relacionadas a:
- Como usar o app (registros diários, entradas, saídas)
- Contas provisionadas
- Metas anuais
- Dashboard e gráficos
- Exportação de dados
- Dúvidas sobre finanças pessoais básicas no contexto do app

Se o usuário perguntar qualquer coisa fora desse escopo, responda:
"Só posso ajudar com o Caixa Diário. Tem alguma dúvida sobre
registros, metas ou contas?"

Seja direto e conciso. Responda em português.
```

### Configuração

- `ANTHROPIC_API_KEY` no `.env` (já carregado via DotNetEnv)
- `appsettings.json`: adicionar `"Anthropic": { "ApiKey": "", "Model": "claude-haiku-4-5-20251001" }`
- `ChatService` registrado como `AddScoped` no `Program.cs`
- Pacote NuGet: `Anthropic.SDK` (cliente oficial C#)

---

## Frontend

### Novos arquivos

```
frontend/src/
  components/Chat/
    ChatButton.tsx             Botão flutuante (canto inferior direito)
    ChatPanel.tsx              Painel deslizante com histórico + input
  hooks/
    useChat.ts                 Estado em memória, chama chatApi
  services/
    chatApi.ts                 POST /api/chat com Bearer token
```

### Integração

`Layout.tsx` recebe `<ChatButton />` e `<ChatPanel />` uma única vez — disponível em todas as páginas autenticadas sem alterar nenhuma página individualmente.

### `useChat` hook

```typescript
// Estado
messages: { role: 'user' | 'assistant'; content: string }[]
isLoading: boolean

// Ação
sendMessage(text: string): Promise<void>
  // Adiciona msg do usuário ao estado
  // Chama chatApi com as últimas 10 mensagens como histórico
  // Adiciona resposta ao estado
  // Zera ao fechar o painel (prop onClose reseta o hook)
```

### Layout do ChatPanel

```
┌─────────────────────────────┐
│  Assistente Caixa Diário  ✕ │  ← header
├─────────────────────────────┤
│                             │
│  [mensagens scrolláveis]    │
│                             │
├─────────────────────────────┤
│ [Digite sua pergunta...] ►  │  ← input + botão enviar
└─────────────────────────────┘
```

- Largura: 360px, altura: 500px (fixo, canto inferior direito)
- Abre com animação slide-in da direita
- Input desabilitado enquanto `isLoading = true`
- Scroll automático para última mensagem

---

## Guard-rails: camadas de segurança

| Camada | Onde | O que faz |
|---|---|---|
| 1 — Autenticação JWT | `ChatController` | Somente usuários logados acessam o chat |
| 2 — Pré-filtro `TopicGuard` | `ChatService` | Bloqueia tópicos off-topic antes da API — zero token gasto |
| 3 — System Prompt | Anthropic API | Claude recusa perguntas fora do escopo mesmo que passem pelo filtro |

---

## Caminho para v2 (acesso a dados reais)

Quando implementar, basta adicionar em `ChatService` antes da chamada à API:

```csharp
var contexto = await _registroService.GetResumoAtual(userId);
systemPrompt += $"\n\nContexto do usuário:\n- Saldo: {contexto.Saldo}\n- Meta: {contexto.Meta}";
```

Nenhuma alteração de contrato necessária — o `ChatController` e o frontend permanecem inalterados.

---

## Custo estimado

- Modelo: Claude Haiku 3.5 — ~$0,08/MTok input, ~$0,25/MTok output
- Conversa típica (10 msgs curtas): ~2.000 tokens ≈ **$0,001**
- Com 100 usuários fazendo 5 conversas/mês: ~$0,50/mês

---

## Arquivos modificados (existentes)

| Arquivo | Alteração |
|---|---|
| `CaixaDiario.API/Program.cs` | Registrar `IChatService`, `TopicGuard` |
| `CaixaDiario.API/appsettings.json` | Adicionar seção `Anthropic` |
| `frontend/src/components/Layout/Layout.tsx` | Adicionar `<ChatButton>` e `<ChatPanel>` |
