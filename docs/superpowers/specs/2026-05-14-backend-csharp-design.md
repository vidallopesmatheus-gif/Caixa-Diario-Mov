# Design: Backend C# — Caixa Diário

**Data:** 2026-05-14
**Status:** Aprovado

## Contexto

O projeto atual é um único `index.html` com autenticação hardcoded e dados armazenados em um GitHub Gist público. Isso expõe credenciais no código-fonte e torna o sistema inseguro para uso em produção. O objetivo é criar um backend ASP.NET Core C# com banco PostgreSQL (Supabase) para substituir o Gist, resolver os problemas de segurança e permitir publicação para 3 clientes.

---

## Arquitetura

```
Browser (index.html)
        ↓ HTTPS
ASP.NET Core API (C#)   ← autentica, valida, controla acesso
        ↓
Supabase (PostgreSQL)   ← dados dos usuários e registros de caixa
```

- **Frontend:** HTML puro mantido. Funções do Gist substituídas por chamadas à API REST.
- **Backend:** ASP.NET Core Web API (.NET 8), autenticação JWT, Entity Framework Core + Npgsql.
- **Banco:** PostgreSQL hospedado no Supabase (plano gratuito).
- **Hospedagem backend:** Railway (recomendado) ou equivalente.
- **Acesso:** URL única compartilhada. Cada cliente faz login com usuário e senha próprios.

---

## Banco de Dados

### Tabela `usuarios`

| Coluna | Tipo | Descrição |
|---|---|---|
| id | UUID | Chave primária |
| nome_usuario | VARCHAR | Login único |
| senha_hash | VARCHAR | Senha com bcrypt |
| nome | VARCHAR | Nome completo |
| loja | VARCHAR | Loja/empresa |
| perfil | VARCHAR | `admin` ou `cliente` |
| ativo | BOOLEAN | Se o usuário está ativo |
| criado_em | TIMESTAMP | Data de criação |
| atualizado_em | TIMESTAMP | Data da última atualização |
| usuario_atualizacao | VARCHAR | Quem fez a última atualização |

### Tabela `registros_diarios`

| Coluna | Tipo | Descrição |
|---|---|---|
| id | UUID | Chave primária |
| cliente_id | UUID | FK → usuarios |
| data | DATE | Data do registro |
| inicio | DECIMAL | Saldo inicial |
| entrada | DECIMAL | Dinheiro que entrou |
| saidas | JSONB | Lista `[{descricao, valor}]` |
| contas_receber | JSONB | Provisionamento a receber |
| contas_pagar | JSONB | Provisionamento a pagar |
| saldo_final | DECIMAL | Saldo calculado/confirmado |
| excluido | BOOLEAN | Soft delete |
| motivo_exclusao | VARCHAR | Motivo informado ao excluir |
| criado_em | TIMESTAMP | Data de criação |
| salvo_em | TIMESTAMP | Última atualização |
| atualizado_em | TIMESTAMP | Data da última atualização |
| usuario_atualizacao | VARCHAR | Quem fez a última atualização |

---

## Endpoints da API

### Autenticação

| Método | Rota | Descrição | Acesso |
|---|---|---|---|
| POST | `/api/auth/login` | Login, retorna JWT | Público |

**Retornos — `/api/auth/login`**

| Status | Código | Situação |
|---|---|---|
| 200 | `SUCESSO` | Login válido, retorna token JWT + dados do usuário |
| 400 | `DADOS_INVALIDOS` | Body inválido ou campos em branco |
| 401 | `CREDENCIAIS_INVALIDAS` | Usuário ou senha incorretos |
| 403 | `USUARIO_INATIVO` | Usuário inativo (`ativo = false`) |
| 500 | `ERRO_INTERNO` | Exceção não tratada |

---

### Usuários

| Método | Rota | Descrição | Acesso |
|---|---|---|---|
| GET | `/api/usuarios` | Lista clientes ativos | Admin |
| GET | `/api/usuarios/{id}` | Busca um usuário | Admin |
| POST | `/api/usuarios` | Cria novo cliente | Admin |
| PUT | `/api/usuarios/{id}` | Atualiza cliente | Admin |
| DELETE | `/api/usuarios/{id}` | Desativa cliente (`ativo=false`) | Admin |

**Retornos — Usuários**

| Status | Código | Situação |
|---|---|---|
| 200 | `SUCESSO` | Sucesso (GET, PUT, DELETE) |
| 201 | `SUCESSO` | Cliente criado (POST) |
| 400 | `DADOS_INVALIDOS` | Campos obrigatórios ausentes ou inválidos |
| 400 | `SENHA_MUITO_CURTA` | Senha com menos de 4 caracteres |
| 401 | `TOKEN_INVALIDO` | JWT ausente, expirado ou malformado |
| 403 | `SEM_PERMISSAO` | Perfil sem acesso ao recurso |
| 404 | `USUARIO_NAO_ENCONTRADO` | ID não existe |
| 409 | `NOME_USUARIO_DUPLICADO` | `nome_usuario` já existe |
| 500 | `ERRO_INTERNO` | Exceção não tratada |

---

### Registros Diários

| Método | Rota | Descrição | Acesso |
|---|---|---|---|
| GET | `/api/registros/{clienteId}` | Lista registros não excluídos | Admin + próprio cliente |
| GET | `/api/registros/{clienteId}/{data}` | Busca registro de um dia | Admin + próprio cliente |
| POST | `/api/registros` | Cria ou atualiza registro do dia | Cliente |
| DELETE | `/api/registros/{clienteId}/{data}` | Soft delete com motivo obrigatório | Admin + próprio cliente |

**Retornos — Registros Diários**

| Status | Código | Situação |
|---|---|---|
| 200 | `SUCESSO` | Sucesso (GET, POST atualização, DELETE) |
| 201 | `SUCESSO` | Registro criado (POST novo) |
| 400 | `DADOS_INVALIDOS` | Campos obrigatórios ausentes ou inválidos |
| 400 | `DATA_FUTURA` | Tentativa de registrar data futura |
| 400 | `MOTIVO_OBRIGATORIO` | Delete sem motivo informado |
| 401 | `TOKEN_INVALIDO` | JWT ausente, expirado ou malformado |
| 403 | `ACESSO_NEGADO` | Cliente acessando registro de outro cliente |
| 404 | `REGISTRO_NAO_ENCONTRADO` | Registro não existe ou já excluído |
| 500 | `ERRO_INTERNO` | Exceção não tratada |

---

### Padrão de resposta de erro

Todos os erros retornam o mesmo formato:

```json
{
  "status": 400,
  "codigo": "NOME_USUARIO_DUPLICADO",
  "mensagem": "Nome de usuário já existe.",
  "campo": "nome_usuario"
}
```

O campo `campo` é opcional — aparece apenas em erros de validação de campo específico.

---

## Estrutura do Projeto C#

```
CaixaDiario.API/
├── Controllers/
│   ├── AuthController.cs
│   ├── UsuariosController.cs
│   └── RegistrosController.cs
├── Data/
│   ├── AppDbContext.cs
│   └── Migrations/
├── DTOs/
│   ├── Auth/
│   │   ├── LoginRequestDto.cs
│   │   └── LoginResponseDto.cs
│   ├── Usuarios/
│   │   ├── UsuarioDto.cs
│   │   ├── CriarUsuarioDto.cs
│   │   └── AtualizarUsuarioDto.cs
│   └── Registros/
│       ├── RegistroDto.cs
│       ├── CriarRegistroDto.cs
│       └── ExcluirRegistroDto.cs
├── Models/
│   ├── Usuario.cs
│   └── RegistroDiario.cs
├── Enums/
│   └── CodigoRetorno.cs
├── Services/
│   ├── AuthService.cs
│   ├── TokenService.cs
│   ├── UsuarioService.cs
│   └── RegistroService.cs
├── Middleware/
│   └── ErrorHandlingMiddleware.cs
├── Responses/
│   ├── ApiResponse.cs
│   └── ErroResponse.cs
├── Program.cs
└── appsettings.json
```

| Camada | Responsabilidade |
|---|---|
| `Controllers` | Recebe requisição, valida autenticação, chama Service, retorna resposta |
| `Services` | Regras de negócio (validações, cálculos, acesso ao banco) |
| `Models` | Entidades do banco (mapeadas pelo Entity Framework) |
| `DTOs` | Dados que trafegam entre frontend e API |
| `Enums` | `CodigoRetorno` com todos os códigos de erro |
| `Responses` | Formato padrão de resposta (`ApiResponse`, `ErroResponse`) |
| `Middleware` | Captura exceções não tratadas e retorna `ERRO_INTERNO` |
| `Data` | Contexto do Entity Framework e migrations |

---

## Integração Frontend → API

**Header de autenticação em todas as requisições autenticadas:**
```http
Authorization: Bearer <token>
```

**Token JWT contém:** `id`, `nome_usuario`, `perfil` — suficiente para o frontend controlar o que exibe.

**Configuração no frontend** — uma linha substitui toda a configuração do Gist:
```js
const API_URL = 'https://seu-backend.railway.app';
```

**Funções reescritas no `index.html`:**

| Função atual | O que muda |
|---|---|
| `loadGistCfg()` / `gistLoad()` | Removidas |
| `gistSave()` | Substituída por `POST /api/registros` |
| `doLogin()` | Passa a chamar `POST /api/auth/login` |
| `saveClient()` | Passa a chamar `POST /api/usuarios` |
| `confirmDelClient()` | Passa a chamar `DELETE /api/usuarios/{id}` |
| `confirmDelRecord()` | Passa a chamar `DELETE /api/registros/{clienteId}/{data}` |

O restante do frontend (UI, cálculos, renderização) permanece intacto.
