# Caixa Diário — Backend API

Sistema de controle de caixa diário para pequenos negócios. API REST construída com ASP.NET Core C# e banco de dados PostgreSQL (Supabase).

---

## Visão Geral

O backend expõe uma API REST que autentica usuários, gerencia clientes e persiste registros diários de caixa. Substitui a solução anterior baseada em GitHub Gist, resolvendo problemas de segurança e permitindo publicação para múltiplos clientes.

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10)
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

Salvar como `appsettings.Development.json` na raiz de `CaixaDiario.API/` (este arquivo está no `.gitignore`).

**3. Rodar as migrations:**
```bash
dotnet ef database update
```

**4. Inserir o usuário admin** (no SQL Editor do Supabase):

Primeiro, gere o hash bcrypt da senha desejada criando um projeto .NET temporário:
```csharp
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("admin123"));
```

Depois execute no Supabase:
```sql
INSERT INTO usuarios (id, nome_usuario, senha_hash, nome, perfil, ativo, criado_em, usuario_atualizacao)
VALUES (gen_random_uuid(), 'admin', '<hash-bcrypt-gerado>', 'Administrador', 'admin', true, NOW(), 'sistema');
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

Cada camada conhece apenas a imediatamente abaixo. Os Services nunca acessam o DbContext diretamente — isso passa pelo Repository, o que permite testar os Services com mock do Repository (sem banco real).

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
├── Migrations/        # Migrations do EF Core
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

---

## Padrão de Resposta

**Sucesso:**
```json
{ "codigo": "SUCESSO", "dados": { ... } }
```

**Erro:**
```json
{ "status": 400, "codigo": "DADOS_INVALIDOS", "mensagem": "Campos obrigatórios ausentes.", "campo": "nomeUsuario" }
```

---

## Testes

```bash
dotnet test CaixaDiario.Tests/
```

Testes unitários cobrem os Services (AuthService, UsuarioService, RegistroService) com mock dos Repositories via Moq. Esperado: 20 testes passando.

---

## Deploy (Railway)

1. Criar conta em [railway.app](https://railway.app)
2. Criar novo projeto → "Deploy from GitHub repo"
3. Selecionar o repositório e a pasta `CaixaDiario.API`
4. Configurar as variáveis de ambiente no painel do Railway (as mesmas da tabela acima)
5. Railway detecta automaticamente o projeto .NET e faz o build
6. Copiar a URL gerada e atualizar `API_URL` no `index.html`
