# 💰 Caixa Diário - Vinicius Guedes

Sistema simples e prático para atualizar o controle de caixa diário com movimentação de dinheiro.

## 🎯 O que faz

Este sistema permite que você:

✅ **Registre dinheiro que entrou** - Anota todas as entradas de caixa  
✅ **Registre dinheiro que saiu** - Com descrição obrigatória do gasto  
✅ **Saldo automático** - O saldo do dia seguinte começa igual ao do dia anterior  
✅ **Visualize o histórico** - Todos os registros em uma tabela organizada  
✅ **Exporte dados** - Baixe um arquivo CSV para usar em Excel ou Google Sheets  

## 🛠️ Setup de Desenvolvimento

Este projeto usa um frontend React (Vite) servido pelo backend .NET. O `wwwroot/` é populado pelo build do frontend — **é necessário buildá-lo antes de rodar o backend**.

### Pré-requisitos
- .NET 8 SDK
- Node.js 18+

### Primeira vez (ou após limpar o repositório)

```bash
# 1. Build do frontend → gera CaixaDiario.API/wwwroot/
cd frontend
npm install
npm run build
cd ..

# 2. Rodar o backend (já serve o frontend buildado)
cd CaixaDiario.API
dotnet run
```

### Desenvolvimento do frontend com hot-reload

```bash
# Terminal 1: backend .NET
cd CaixaDiario.API && dotnet run

# Terminal 2: Vite dev server (proxy para o backend)
cd frontend && npm run dev
```

> **Atenção:** Se pular o `npm run build`, o backend sobe mas retorna 404 em todas as rotas do frontend. Sempre rode o build ao clonar o repositório ou após `git clean`.

### Testes

```bash
# Backend (.NET)
cd CaixaDiario.Tests && dotnet test

# Frontend (Vitest)
cd frontend && npm test
```

---

## 📱 Como usar

### 1. Abrir o formulário
Abra o arquivo `index.html` no seu navegador (Chrome, Firefox, Safari, Edge, etc)

### 2. Preencher os dados do dia
- **Data**: Selecione a data do registro
- **Dinheiro que Entrou**: Quanto entrou de dinheiro (em R$)
- **Dinheiro que Saiu**: Quanto saiu de dinheiro (em R$)
- **Descrição da Saída**: Descreva por que saiu dinheiro (obrigatório se houver saída)
- **Saldo Final**: O saldo total que ficou em caixa ao final do dia

### 3. Adicionar a movimentação
Clique em **"✅ Adicionar Movimentação"**

### 4. Visualizar histórico
A tabela mostra todos os registros com:
- Data do registro
- Total que entrou
- Total que saiu  
- Descrição do gasto
- Saldo final do dia
- Botão para deletar (se necessário)

## 💡 Dicas importantes

### Saldo automático entre dias
Para que o saldo do dia seguinte comece corretamente:
1. O último dia tem um saldo final
2. No dia seguinte, seu saldo inicial será igual ao saldo final do dia anterior
3. Basta informar quanto entrou e saiu neste novo dia
4. O saldo final será calculado automaticamente

**Fórmula**: `Saldo Novo = Saldo Anterior + Entrada - Saída`

### Exemplo prático
```
Dia 01/05:
- Entrada: R$ 100,00
- Saída: R$ 20,00 (Compras)
- Saldo Final: R$ 80,00

Dia 02/05:
- Saldo anterior: R$ 80,00 (automático)
- Entrada: R$ 50,00
- Saída: R$ 0,00
- Saldo Final: R$ 130,00 (80 + 50)
```

## 📥 Exportar dados

1. Clique no botão **"📥 Exportar para CSV"**
2. Um arquivo `caixa_vinicius_YYYY-MM-DD.csv` será baixado
3. Abra em Excel, Google Sheets ou outro programa de planilha

## 🔒 Segurança dos dados

- Os dados são salvos **localmente no seu navegador**
- Ninguém tem acesso sem sua permissão
- Se limpar o cache/histórico do navegador, os dados podem ser perdidos
- **Sempre exporte seus dados regularmente!**

## ⚠️ Importante

- **Não limpe o cache do navegador** - Pode perder os dados
- **Faça backups regulares** - Use a função de exportação
- **Uma data = Um registro** - Se tentar adicionar na mesma data, pedirá confirmação
- **Descrição obrigatória** - Se houver saída, deve descrever

## 🚀 Colocar online

Para colocar esta planilha online e acessar de qualquer lugar:

### Opção 1: GitHub Pages (Gratuito)
1. Já está em um repositório GitHub
2. Vá em **Settings > Pages**
3. Selecione `main` branch
4. Acesse via link gerado

### Opção 2: Vercel (Gratuito)
1. Conecte o repositório no [Vercel.com](https://vercel.com)
2. Deploy automático em segundos

### Opção 3: Netlify (Gratuito)
1. Faça upload em [Netlify.com](https://netlify.com)
2. Acesse de qualquer lugar

**Nota**: Se usar online, considere adicionar um banco de dados para sincronizar entre dispositivos.

## ❓ Dúvidas frequentes

**P: Perdi meus dados. Como recupero?**  
R: Se tiver feito exportação, pode reimportar. Senão, infelizmente os dados do navegador não podem ser recuperados.

**P: Posso usar em celular?**  
R: Sim! O sistema é responsivo e funciona em qualquer dispositivo.

**P: Os dados são sincronizados entre dispositivos?**  
R: Não, nesta versão. Cada dispositivo tem seus próprios dados. Para sincronizar, exporte e compartilhe o CSV.

**P: Como editar um registro?**  
R: Delete o registro antigo e adicione um novo com os dados corretos.

---

**Desenvolvido com ❤️ para facilitar o controle de caixa**
