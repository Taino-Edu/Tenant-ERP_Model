# 📚 softNerd — Documentação Completa e Didática

> **Para quem é este documento?**  
> Para você que criou o projeto mas não lembra exatamente como ele funciona. Aqui você vai entender **o que é**, **como funciona**, **como rodar** e **como testar** cada parte do sistema.

---

> ⚠️ **Atualização importante (04/05/2026):** Após análise detalhada do código, foram identificadas **lacunas críticas** de implementação. Veja a Seção 0 antes de qualquer coisa.

---

## 🗺️ Índice

0. [Estado real do sistema — O que está feito e o que está faltando](#0-estado-real-do-sistema)
1. [O que é esse projeto?](#1-o-que-é-esse-projeto)
2. [Visão geral da arquitetura](#2-visão-geral-da-arquitetura)
3. [Tecnologias utilizadas](#3-tecnologias-utilizadas)
4. [Como rodar o projeto](#4-como-rodar-o-projeto)
5. [Estrutura de pastas](#5-estrutura-de-pastas)
6. [O Backend em detalhes](#6-o-backend-em-detalhes)
7. [O Frontend em detalhes](#7-o-frontend-em-detalhes)
8. [O banco de dados](#8-o-banco-de-dados)
9. [Como a autenticação funciona](#9-como-a-autenticação-funciona)
10. [Comunicação em tempo real (SignalR)](#10-comunicação-em-tempo-real-signalr)
11. [Fluxos principais do sistema](#11-fluxos-principais-do-sistema)
12. [Guia de testes manuais](#12-guia-de-testes-manuais)
13. [Pontos onde o sistema pode quebrar](#13-pontos-onde-o-sistema-pode-quebrar)
14. [Oportunidades de melhoria](#14-oportunidades-de-melhoria)
15. [Glossário](#15-glossário)

---

---

## 0. Estado real do sistema

> Esta seção é a mais importante. Mostra exatamente o que foi implementado, o que está pela metade e o que não existe ainda.

### As 3 partes do sistema (conforme definido)

O sistema foi projetado com 3 módulos distintos:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  PARTE 1 — Venda Avulsa                                                     │
│  Venda direta de produtos SEM login do cliente.                             │
│  Ex: cliente chega no balcão, pede uma carta, admin registra e cobra.       │
│  Status: ❌ NÃO IMPLEMENTADO — não existe nenhum código para isso           │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  PARTE 2 — Mesas (QR Code + Comanda)                                        │
│  Cliente escaneia QR da mesa → faz login rápido (CPF+WhatsApp) →            │
│  adiciona itens → Maikon fecha a conta.                                     │
│  Status: 🟡 INCOMPLETO — falta o AuthController (veja abaixo)              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  PARTE 3 — Campeonatos TCG                                                  │
│  Jogadores compram ingresso antecipado → sentam nas mesas →                 │
│  jogam e consomem → cada um tem sua comanda → Maikon fecha.                 │
│  Status: 🟡 INCOMPLETO — mesma dependência do AuthController               │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 🚨 O problema mais crítico: AuthController está faltando

Ao analisar o código, foi encontrado que **não existe o arquivo `AuthController.cs`**. Isso é um bloqueio grave porque sem ele, os endpoints de login não existem na API.

**O que existe (está implementado):**

| Arquivo | Status | O que faz |
|---|---|---|
| `AuthService.cs` | ✅ Completo | Toda a lógica de login, tokens, quick-login |
| `AuthDtos.cs` | ✅ Completo | Formatos de entrada/saída (LoginRequest, QuickLoginRequest...) |
| `IAuthService.cs` | ✅ Completo | Contrato da interface |
| `Program.cs` | ✅ Registrado | `AddScoped<IAuthService, AuthService>()` está configurado |

**O que está faltando:**

| Arquivo | Status | Impacto |
|---|---|---|
| `AuthController.cs` | ❌ AUSENTE | Sem ele, NENHUM login funciona — 404 em tudo |

**O arquivo que precisa ser criado** expõe as rotas:
```
POST /api/auth/login         → Login do admin (email + senha)
POST /api/auth/quick-login   → Login do cliente (CPF + WhatsApp + mesa)
POST /api/auth/refresh       → Renovar token expirado
POST /api/auth/logout        → Fazer logout
```

---

### 🔍 Segundo problema: QuickLogin não cria a Comanda

Olhando o `AuthService.cs`, o `QuickLoginAsync()` cria/recupera o usuário mas **não abre a comanda automaticamente**. O `AuthResponse` retorna: `accessToken`, `refreshToken`, `expiresAt`, `role`, `userName`, `userId` — mas **não retorna `comandaId`**.

O fluxo previsto no guia de testes menciona um `comandaId` na resposta do quick-login, mas o código ainda não faz isso. A abertura da comanda precisaria acontecer dentro do `QuickLoginAsync` ou logo após.

---

### 📊 Resumo de implementação (honesto)

| Módulo | Implementado |
|---|---|
| Modelos do banco (Users, Products, Comandas...) | ✅ 100% |
| Configuração do banco (AppDbContext) | ✅ 100% |
| AuthService (lógica de autenticação) | ✅ 90% |
| ProductController (CRUD de estoque) | ✅ 100% |
| ComandaController (gestão de pedidos) | ✅ 95% |
| ChampionshipController (campeonatos) | ✅ 90% |
| TcgController (busca de cartas) | ✅ 80% |
| **AuthController (endpoints de login)** | **❌ 0% — arquivo não existe** |
| Frontend — Dashboard admin | ✅ 95% |
| Frontend — Página da mesa (QR Code) | ✅ 85% |
| Frontend — Estoque admin | ✅ 90% |
| Frontend — Campeonatos admin | ✅ 85% |
| **Venda Avulsa (sem login)** | **❌ 0% — não existe em nenhuma camada** |
| Integração Quick-Login → abre Comanda | 🟡 50% — falta criar a comanda automaticamente |

---

## 1. O que é esse projeto?

O **softNerd** é um sistema completo para gerenciar uma **loja de card games** (como Pokémon, Magic: The Gathering, Yu-Gi-Oh). Pensa assim:

> Imagine uma lojinha que também tem mesas para jogar. O cliente chega, senta numa mesa, escaneia um QR Code, faz o pedido pelo celular (bebidas, salgadinhos, acessórios) e o dono vê tudo em tempo real no painel. Quando o cliente vai embora, o dono fecha a conta. O sistema ainda gerencia campeonatos e o estoque da loja.

### O que o sistema faz:

- **Gestão de comandas** — O cliente escaneia o QR Code da mesa, faz login rápido (CPF + WhatsApp) e adiciona itens no seu pedido. O admin vê em tempo real.
- **Estoque (produtos)** — O admin cadastra produtos (bebidas, salgadinhos, cartas, acessórios) com preço e quantidade.
- **Campeonatos** — O admin cria torneios, os jogadores se inscrevem. Pokémon, Magic, Yu-Gi-Oh...
- **Busca de cartas TCG** — Integração com a API externa TCGPlayer para buscar cartas e preços.
- **Dashboard ao vivo** — O admin vê todas as mesas abertas em tempo real, sem precisar recarregar a página.

---

## 2. Visão geral da arquitetura

```
┌─────────────────────────────────────────────────────────────────┐
│                        USUÁRIO FINAL                            │
│                                                                 │
│  🧑 Admin (dono da loja)          🛒 Cliente (jogador)         │
│  Acessa: localhost:3000/admin     Acessa: localhost:3000/mesa/3 │
└──────────────────┬──────────────────────────┬───────────────────┘
                   │                          │
                   ▼                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                   FRONTEND — Next.js (porta 3000)               │
│                                                                 │
│  • Páginas React com TypeScript                                 │
│  • Consome a API via Axios (HTTP)                               │
│  • Recebe atualizações em tempo real via SignalR (WebSocket)    │
└──────────────────────────┬──────────────────────────────────────┘
                           │ HTTP + WebSocket
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                   BACKEND — ASP.NET Core 8 (porta 5000)         │
│                                                                 │
│  • API REST → responde às requisições do frontend               │
│  • Hub SignalR → empurra atualizações em tempo real             │
│  • Autenticação JWT → valida tokens                             │
│  • Lógica de negócio → regras da loja                           │
└────────┬──────────────────────────────────┬─────────────────────┘
         │                                  │
         ▼                                  ▼
┌─────────────────────┐       ┌─────────────────────────────────┐
│  PostgreSQL (5432)  │       │       MongoDB (27017)           │
│                     │       │                                 │
│  Dados principais:  │       │  Cache de cartas TCG:           │
│  • Usuários         │       │  • Cartas buscadas na API       │
│  • Produtos         │       │  • Evita repetir chamadas       │
│  • Comandas         │       │    externas desnecessárias      │
│  • Campeonatos      │       └─────────────────────────────────┘
└─────────────────────┘
```

> 💡 **Analogia:** Pensa no backend como um garçom que anota os pedidos, o frontend como o cardápio digital na mão do cliente, o PostgreSQL como o caderninho de pedidos e o MongoDB como um bloco de rascunho para informações temporárias.

---

## 3. Tecnologias utilizadas

### Backend (C# / .NET)

| Tecnologia | O que faz no projeto |
|---|---|
| **ASP.NET Core 8** | Framework principal da API. Processa requisições HTTP. |
| **Entity Framework Core** | Traduz código C# para queries SQL. Você escreve em C#, ele faz o SQL. |
| **PostgreSQL** | Banco de dados relacional. Armazena tudo de forma permanente. |
| **MongoDB** | Banco de dados de documentos. Guarda cache de cartas TCG. |
| **SignalR** | Permite comunicação em tempo real via WebSocket (push de dados). |
| **JWT (JSON Web Token)** | Sistema de autenticação sem estado. O token é uma "pulseira de acesso". |
| **BCrypt** | Criptografia de senhas. A senha nunca é salva em texto puro. |
| **Swagger** | Documentação automática da API. Acesse em `localhost:5000`. |

### Frontend (TypeScript / React)

| Tecnologia | O que faz no projeto |
|---|---|
| **Next.js 14** | Framework React com roteamento de páginas automático. |
| **TypeScript** | JavaScript com tipos. Evita bugs antes de executar. |
| **Tailwind CSS** | Classes CSS para estilização rápida. |
| **Axios** | Biblioteca para fazer chamadas HTTP à API. |
| **@microsoft/signalr** | Conecta o frontend ao hub em tempo real. |
| **react-hot-toast** | Notificações de sucesso/erro na tela. |
| **qrcode** | Gera os QR Codes das mesas. |
| **Lucide React** | Biblioteca de ícones. |

### Infraestrutura

| Tecnologia | O que faz |
|---|---|
| **Docker** | Empacota cada serviço (API, frontend, bancos) em containers isolados. |
| **Docker Compose** | Orquestra todos os containers juntos com um único comando. |

---

## 4. Como rodar o projeto

### Pré-requisitos

- **Docker Desktop** instalado e rodando (obrigatório!)
- Windows 10/11

### Opção 1 — Script automático (recomendado)

Abra o PowerShell na pasta `softNerd` e rode:

```powershell
.\start.ps1
```

Esse script vai:
1. Verificar se o Docker está rodando
2. Construir as imagens do Docker
3. Subir PostgreSQL, MongoDB, a API e o frontend
4. Aplicar as migrações do banco automaticamente
5. Criar o usuário admin (Maikon) automaticamente

> ⏱️ **Primeira vez:** pode demorar 3-5 minutos (precisa baixar as imagens)  
> ⏱️ **Vezes seguintes:** ~30 segundos

### Opção 2 — Batch file

Clique duas vezes no arquivo `INICIAR-TUDO.bat`.

### Verificando se está tudo rodando

Depois de subir, acesse:

| Endereço | O que é |
|---|---|
| `http://localhost:3000` | Frontend (tela principal) |
| `http://localhost:5000` | API + Swagger (documentação interativa) |
| `http://localhost:5432` | PostgreSQL (use pgAdmin ou DBeaver) |
| `http://localhost:27017` | MongoDB |
| `http://localhost:5050` | pgAdmin (visualizar banco) |

### Credenciais padrão

**Admin da API:**
- Email: `admin@cardgamestore.com.br`
- Senha: `SenhaForte@123`

**PostgreSQL:**
- Usuário: `cardgame_user`
- Senha: `CardGame@2025`
- Banco: `cardgamestore`

**pgAdmin:**
- Email: `admin@cardgame.com`
- Senha: `admin`

### Para parar tudo

```powershell
docker-compose down
```

Para parar E apagar os dados dos bancos:
```powershell
docker-compose down -v
```

---

## 5. Estrutura de pastas

```
softNerd/
│
├── CardGameStore/              ← API Backend (C#)
│   ├── Controllers/            ← Endpoints da API (onde chegam as requisições)
│   ├── Services/               ← Lógica de negócio
│   │   ├── Interfaces/         ← "Contratos" dos serviços
│   │   └── Implementations/    ← Código real dos serviços
│   ├── Models/                 ← Estrutura dos dados
│   │   ├── PostgreSQL/         ← Entidades do banco relacional
│   │   └── MongoDB/            ← Modelos do cache
│   ├── DTOs/                   ← Objetos de transferência (o que entra/sai da API)
│   ├── Data/
│   │   └── AppDbContext.cs     ← Configuração do banco de dados
│   ├── Hubs/
│   │   └── ComandaHub.cs       ← Hub do SignalR (tempo real)
│   └── Program.cs              ← Configuração central (ponto de entrada)
│
├── frontend/                   ← Frontend Next.js (TypeScript)
│   ├── app/                    ← Páginas (App Router do Next.js)
│   │   ├── admin/              ← Área administrativa
│   │   │   ├── dashboard/      ← Dashboard em tempo real
│   │   │   ├── estoque/        ← Gestão de produtos
│   │   │   ├── cartas/         ← Busca de cartas TCG
│   │   │   ├── campeonatos/    ← Gestão de torneios
│   │   │   └── qrcodes/        ← Geração de QR Codes
│   │   ├── login/              ← Tela de login (admin)
│   │   ├── cliente/            ← Visão do cliente
│   │   └── mesa/[mesa]/        ← Página da mesa (ex: /mesa/3)
│   ├── lib/
│   │   ├── api.ts              ← Funções para chamar a API
│   │   └── auth.ts             ← Funções de autenticação
│   └── components/             ← Componentes reutilizáveis
│
├── docker-compose.yml          ← Orquestra todos os serviços
├── start.ps1                   ← Script de inicialização
└── INICIAR-TUDO.bat            ← Alternativa Windows
```

> 💡 **Dica de navegação:** Quando quiser entender uma funcionalidade, siga a cadeia:  
> `Controller` → chama → `Service` → acessa → `DbContext` → salva no → `Banco`

---

## 6. O Backend em detalhes

### Como o .NET organiza o código

O backend segue um padrão chamado **Clean Architecture** (Arquitetura Limpa). A ideia é separar responsabilidades:

```
Requisição HTTP
      ↓
  Controller        ← Recebe e valida os dados da requisição
      ↓
   Service          ← Aplica as regras de negócio
      ↓
  DbContext         ← Acessa o banco de dados
      ↓
   Banco            ← Salva/lê os dados
```

### O Program.cs — O coração da configuração

O arquivo `Program.cs` é onde **tudo é configurado** antes da aplicação iniciar. Pensa nele como a "planta baixa" da API. Ele configura:

1. **Qual banco usar** — SQLite (desenvolvimento local) ou PostgreSQL (Docker/produção)
2. **MongoDB** — Opcionalmente, para cache de cartas
3. **Autenticação JWT** — Regras de validação dos tokens
4. **Autorização RBAC** — Políticas de acesso (Admin vs Cliente)
5. **SignalR** — Hub de tempo real
6. **CORS** — Quais origens podem chamar a API
7. **Swagger** — Documentação automática

### Os Controllers (endpoints)

Cada controller é responsável por uma área do sistema:

#### `ComandaController` — Pedidos/contas
```
GET    /api/comanda/dashboard    → Lista todas as comandas abertas (Admin)
GET    /api/comanda/my           → Minha comanda ativa (Cliente)
POST   /api/comanda/{id}/items   → Adicionar item à comanda
DELETE /api/comanda/{id}/items/{itemId} → Remover item
PUT    /api/comanda/{id}/close   → Fechar/pagar a conta (Admin)
PUT    /api/comanda/{id}/cancel  → Cancelar sem cobrança (Admin)
```

#### `ProductController` — Estoque
```
GET    /api/product              → Listar produtos ativos
GET    /api/product/{id}         → Buscar produto por ID
GET    /api/product/low-stock    → Alertas de estoque baixo (Admin)
POST   /api/product              → Criar produto (Admin)
PUT    /api/product/{id}         → Atualizar produto (Admin)
DELETE /api/product/{id}         → Desativar produto (Admin)
PATCH  /api/product/{id}/stock   → Ajustar estoque (Admin)
```

#### `ChampionshipController` — Campeonatos
```
GET    /api/championship         → Listar campeonatos
GET    /api/championship/{id}    → Detalhes de um campeonato
POST   /api/championship         → Criar campeonato (Admin)
POST   /api/championship/{id}/register → Inscrever-se (Cliente)
PUT    /api/championship/{id}    → Atualizar campeonato (Admin)
DELETE /api/championship/{id}    → Deletar campeonato (Admin)
```

#### `TcgController` — Cartas TCG
```
GET    /api/tcg/search           → Buscar cartas na API externa
GET    /api/tcg/card/{id}        → Detalhes de carta (cache MongoDB)
GET    /api/tcg/prices/{cardId}  → Preços atuais da carta
```

### Os Services (lógica de negócio)

Os serviços contêm **as regras do negócio**. Exemplos do que eles fazem:

- `AuthService` — Valida login, gera tokens JWT, lida com refresh token
- `ComandaService` — Verifica se a comanda existe, se está aberta, atualiza o total, notifica via SignalR
- `ProductService` — Valida se tem estoque antes de adicionar à comanda
- `ChampionshipService` — Verifica prazo de inscrição, número máximo de participantes

### Injeção de Dependência

Você vai notar que os serviços são "injetados" via construtor:

```csharp
// No Controller
public ComandaController(IComandaService comandaService, IHubContext<ComandaHub> hub)
{
    _comandaService = comandaService;
    _hub = hub;
}
```

Isso é **Dependency Injection (DI)** — em vez de criar objetos manualmente, você declara o que precisa e o framework entrega. É mais fácil de testar e manter.

---

## 7. O Frontend em detalhes

### Como o Next.js organiza as páginas

O Next.js usa **App Router** — cada pasta dentro de `app/` vira uma rota. Exemplos:

| Pasta | URL |
|---|---|
| `app/page.tsx` | `localhost:3000/` |
| `app/login/page.tsx` | `localhost:3000/login` |
| `app/admin/dashboard/page.tsx` | `localhost:3000/admin/dashboard` |
| `app/mesa/[mesa]/page.tsx` | `localhost:3000/mesa/3` (dinâmico!) |

O `[mesa]` entre colchetes é um parâmetro dinâmico — o número da mesa vem da URL.

### O arquivo `lib/api.ts` — Como o frontend fala com a API

Todas as chamadas HTTP são centralizadas aqui. Usa o Axios como cliente HTTP:

```typescript
// Exemplo simplificado de como funciona
const api = axios.create({ baseURL: 'http://localhost:5000' })

// Interceptor: adiciona o token automaticamente em toda requisição
api.interceptors.request.use(config => {
  const token = getToken() // pega do cookie
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})
```

### O Dashboard em tempo real

O arquivo `app/admin/dashboard/page.tsx` é o mais complexo. Ele:

1. **Carrega as comandas** via HTTP ao abrir a página
2. **Conecta ao SignalR** para receber atualizações em tempo real
3. **Escuta eventos** `ComandaUpdated` e `ComandaClosed`
4. Quando recebe um evento, **atualiza a tela** sem recarregar a página
5. Mostra **métricas** (total de mesas, faturamento aberto)

```
Admin abre o dashboard
        ↓
Busca comandas via GET /api/comanda/dashboard
        ↓
Conecta ao WebSocket /hubs/comanda
        ↓
[Aguarda eventos em tempo real]
        ↓
Cliente adiciona item na mesa 3
        ↓
API dispara evento "ComandaUpdated" via SignalR
        ↓
Dashboard recebe → atualiza a tela → mostra toast
```

### O fluxo do cliente (página da mesa)

```
Cliente chega na mesa 3
        ↓
Escaneia QR Code → abre localhost:3000/mesa/3
        ↓
Informa nome, CPF e WhatsApp
        ↓
POST /api/auth/quick-login → API cria conta + comanda + retorna token
        ↓
Cliente vê o cardápio de produtos
        ↓
Adiciona itens → POST /api/comanda/{id}/items
        ↓
Admin vê em tempo real no dashboard
```

---

## 8. O banco de dados

### Estrutura das tabelas (PostgreSQL)

```
┌──────────────┐     ┌──────────────────┐     ┌──────────────────────────┐
│    users     │     │     comandas      │     │      comanda_items        │
├──────────────┤     ├──────────────────┤     ├──────────────────────────┤
│ id (PK)      │◄────│ user_id (FK)     │◄────│ comanda_id (FK)          │
│ name         │     │ id (PK)          │     │ id (PK)                  │
│ email        │     │ table_identifier │     │ product_id (FK, opcional)│
│ password_hash│     │ status           │     │ item_name                │
│ cpf          │     │ total_in_cents   │     │ unit_price_in_cents      │
│ whatsapp     │     │ opened_at        │     │ quantity                 │
│ role         │     │ closed_at        │     └──────────────────────────┘
│ is_active    │     │ championship_id  │
└──────────────┘     └──────────────────┘
                                │
                                │
┌──────────────┐     ┌──────────────────────────┐
│  products    │     │      championships        │
├──────────────┤     ├──────────────────────────┤
│ id (PK)      │     │ id (PK)                  │
│ name         │     │ name                     │
│ category     │     │ game                     │
│ price_in_cents│    │ start_date               │
│ stock_qty    │     │ max_participants          │
│ is_active    │     │ entry_fee_in_cents        │
└──────────────┘     │ status                   │
                     └──────────────────────────┘
```

### Por que preços são em centavos?

Você vai notar `price_in_cents`, `total_in_cents`, etc. Isso é uma **prática recomendada** para evitar problemas com ponto flutuante. `R$ 9,99` é armazenado como `999`. Evita bugs clássicos como `0.1 + 0.2 = 0.30000000000000004`.

### Status da Comanda

```
Aberta → EmAndamento → Fechada
  ↓
Cancelada
```

- **Aberta** — Cliente fez login, comanda criada, ainda sem itens
- **EmAndamento** — Cliente adicionou pelo menos um item
- **Fechada** — Admin fechou e cobrou
- **Cancelada** — Admin cancelou sem cobrar

### O seed do banco (dados iniciais)

No `AppDbContext.cs`, existe uma configuração de **seed** que cria o usuário admin "Maikon" automaticamente quando o banco é criado pela primeira vez:

```csharp
Email: "admin@cardgamestore.com.br"
Senha: "SenhaForte@123" (armazenada com BCrypt)
```

---

## 9. Como a autenticação funciona

### Login do Admin

```
1. Admin envia: { email, senha }
      ↓
2. API verifica se o email existe no banco
      ↓
3. API verifica se o hash BCrypt da senha bate
      ↓
4. API gera dois tokens:
   - Access Token (JWT) — válido por 60 minutos
   - Refresh Token — válido por 30 dias
      ↓
5. Frontend salva os tokens (cookies)
      ↓
6. Toda requisição seguinte envia: Authorization: Bearer {token}
```

### Login rápido do Cliente

```
1. Cliente envia: { nome, cpf, whatsapp, mesa }
      ↓
2. API verifica se CPF já existe:
   - Sim → usa a conta existente
   - Não → cria nova conta de Cliente
      ↓
3. API cria uma nova Comanda para essa mesa
      ↓
4. API gera um Access Token para o cliente
      ↓
5. Cliente usa o token para adicionar itens na comanda
```

### O que é um JWT?

JWT (JSON Web Token) é um token que parece isso:
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6Ik1haWtvbiIsInJvbGUiOiJBZG1pbiIsImV4cCI6MTcwMDAwMDAwMH0.SIGNATURE
```

Ele tem 3 partes separadas por `.`:
1. **Header** — Algoritmo usado (HS256)
2. **Payload** — Dados do usuário (id, nome, role, expiração) — **não é criptografado, só codificado em Base64**
3. **Signature** — Assinatura que garante que o token não foi adulterado

> ⚠️ **Importante:** O payload do JWT pode ser lido por qualquer um! Nunca coloque dados sensíveis como senha nele.

---

## 10. Comunicação em tempo real (SignalR)

### O que é SignalR?

SignalR é uma biblioteca que permite comunicação **bidirecional** entre servidor e cliente usando WebSocket. Em vez do cliente ficar perguntando "tem novidade?" a cada segundo (polling), o servidor **avisa** o cliente quando há algo novo.

### Como funciona no projeto

```
ComandaHub.cs (servidor)          Dashboard (cliente)
       │                                 │
       │  1. Cliente conecta             │
       │◄────────────────────────────────┤
       │                                 │
       │  2. API adiciona item           │
       │     → chama _hub.Clients.All    │
       │        .SendAsync("ComandaUpdated", dados)
       │                                 │
       │  3. Servidor envia evento       │
       ├────────────────────────────────►│
       │                                 │
       │  4. Frontend atualiza a tela    │
       │     sem recarregar a página     │
```

### Autenticação no SignalR

Como WebSocket não suporta headers HTTP da mesma forma, o token JWT é enviado via **query string**:

```
ws://localhost:5000/hubs/comanda?access_token=eyJhbGc...
```

A API extrai esse token no `OnMessageReceived`:
```csharp
var accessToken = context.Request.Query["access_token"];
if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
    context.Token = accessToken;
```

---

## 11. Fluxos principais do sistema

### Fluxo completo de uma venda

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Admin cadastra produtos no estoque                       │
│    POST /api/product { nome, preço, quantidade }            │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. Admin gera QR Codes das mesas                            │
│    Acessa: localhost:3000/admin/qrcodes                     │
│    Cada QR aponta para: localhost:3000/mesa/{numero}        │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. Cliente escaneia o QR da Mesa 3                          │
│    Abre: localhost:3000/mesa/3                              │
│    Informa nome, CPF, WhatsApp                              │
│    → API cria conta + comanda + retorna token               │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. Cliente adiciona itens                                   │
│    POST /api/comanda/{id}/items { produtoId, quantidade }   │
│    → API atualiza total da comanda                          │
│    → API dispara evento SignalR "ComandaUpdated"            │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│ 5. Dashboard do Admin recebe o evento em tempo real         │
│    → Card da comanda pisca / notificação toast aparece      │
│    → Admin vê: Mesa 3 - R$ 25,00 - 3 itens                 │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│ 6. Admin fecha a comanda                                    │
│    PUT /api/comanda/{id}/close                              │
│    → Status muda para "Fechada"                             │
│    → Dashboard remove o card da mesa                        │
└─────────────────────────────────────────────────────────────┘
```

---

## 12. Guia de testes manuais

### Pré-requisito: projeto rodando

```powershell
.\start.ps1
# Aguardar tudo subir (~30s após a primeira vez)
```

### Teste 1 — Login como Admin

1. Acesse `http://localhost:5000` (Swagger)
2. Clique em `POST /api/auth/login`
3. Clique em "Try it out"
4. Preencha:
```json
{
  "email": "admin@cardgamestore.com.br",
  "password": "SenhaForte@123"
}
```
5. ✅ Esperado: Status 200, retorna `accessToken` e `refreshToken`
6. ❌ Se falhar: banco pode não ter sido criado com o seed

### Teste 2 — Autenticar no Swagger

1. Copie o `accessToken` do Teste 1
2. Clique no botão "Authorize 🔓" no topo do Swagger
3. Cole: `Bearer {seu_token_aqui}`
4. Clique "Authorize"
5. ✅ Agora as rotas protegidas estão liberadas

### Teste 3 — Criar um produto

1. No Swagger: `POST /api/product`
2. Preencha:
```json
{
  "name": "Refrigerante Lata",
  "description": "Coca-Cola 350ml",
  "category": "Bebida",
  "priceInCents": 500,
  "stockQuantity": 50,
  "minimumStock": 10
}
```
3. ✅ Esperado: Status 201, produto criado com ID

### Teste 4 — Simular cliente na mesa

1. No Swagger: `POST /api/auth/quick-login`
2. Preencha:
```json
{
  "name": "João Gamer",
  "cpf": "12345678901",
  "whatsApp": "11999999999",
  "tableIdentifier": "Mesa-3"
}
```
3. ✅ Esperado: Status 200, retorna token + `comandaId`
4. Guarde o `comandaId` para o próximo teste

### Teste 5 — Adicionar item à comanda

1. Use o token do cliente (Teste 4) ou troque o Authorize pelo token do cliente
2. No Swagger: `POST /api/comanda/{id}/items`
3. Substitua `{id}` pelo `comandaId` do Teste 4
4. Preencha:
```json
{
  "productId": "{id-do-produto-criado-no-teste-3}",
  "quantity": 2
}
```
5. ✅ Esperado: Status 200, item adicionado

### Teste 6 — Ver o dashboard em tempo real

1. Abra `http://localhost:3000/login`
2. Faça login como admin
3. Navegue até `http://localhost:3000/admin/dashboard`
4. ✅ Esperado: Ver a comanda da "Mesa-3" com o item adicionado
5. Faça outro Teste 5 e observe o card atualizar automaticamente

### Teste 7 — Fechar a comanda

1. No dashboard, clique em "Fechar" na comanda da Mesa-3
2. ✅ Esperado: Card desaparece do dashboard
3. No Swagger, use `GET /api/comanda/dashboard` → comanda não aparece mais

### Teste 8 — Verificar estoque baixo

1. Crie um produto com `stockQuantity: 3` e `minimumStock: 5`
2. `GET /api/product/low-stock`
3. ✅ Esperado: O produto aparece na lista de alerta

---

## 13. Pontos onde o sistema pode quebrar

> Esta seção é um mapa honesto das fragilidades identificadas no código. Não é crítica — é orientação para você melhorar o sistema!

### 🔴 Crítico — Pode causar perda de dados ou falhas graves

**1. Sem validação de CPF**  
O campo CPF aceita qualquer string de 11 caracteres. Um usuário pode se cadastrar com `00000000000` ou `12345678901` (CPF inválido). Isso pode causar colisões ou registros inválidos.

**2. Secret Key JWT hardcoded nos comentários**  
O arquivo `docker-compose.yml` contém a chave secreta JWT em texto plano: `CardGameStore_SecretKey_2025_MinLength32Chars!`. Se esse arquivo for para um repositório público, qualquer pessoa pode forjar tokens de admin.

**3. Sem controle de estoque na adição à comanda**  
Ao adicionar um produto à comanda, o sistema não desconta o estoque em tempo real. Dois clientes podem adicionar o último refrigerante ao mesmo tempo e ambos terão na comanda, mas o estoque vai ficar negativo.

**4. Nenhum mecanismo de retry no SignalR**  
Se a conexão SignalR cair, o dashboard para de receber atualizações. O código tem `onreconnected` mas não há tentativas automáticas configuradas com backoff exponencial.

### 🟡 Moderado — Pode causar comportamento inesperado

**5. CORS muito permissivo para produção**  
O CORS permite `localhost:3000`, `localhost:5173` e `localhost:5000`. Em produção real, esses domínios localhost não fazem sentido — qualquer chamada de um domínio externo seria bloqueada mas o localhost ficaria aberto.

**6. SQLite em desenvolvimento sem migrations**  
O desenvolvimento usa `EnsureCreated()` em vez de migrations. Isso significa que se você mudar o schema do banco, em vez de migrar, o SQLite pode ficar desatualizado sem aviso claro.

**7. MongoDB sem tratamento de erro nos serviços**  
O try/catch para MongoDB está apenas no `Program.cs`. Se o MongoDB conectar mas falhar durante uma query, a exceção não tem tratamento nos serviços.

**8. Refresh Token sem revogação**  
Uma vez emitido, o refresh token de 30 dias não pode ser revogado (não há tabela de tokens revogados). Se um token for comprometido, ele fica válido por 30 dias.

### 🟢 Baixo — Inconveniências ou melhorias de UX

**9. Sem paginação nos endpoints de listagem**  
`GET /api/product` retorna todos os produtos de uma vez. Com 10.000 produtos, isso vai ser lento.

**10. O dashboard re-busca TUDO a cada evento SignalR**  
Quando uma comanda é atualizada, o dashboard faz `fetchComandas()` que busca TODAS as comandas novamente. Com 50 mesas abertas e atualizações frequentes, isso cria muitas requisições desnecessárias.

**11. Sem loading state no login do cliente**  
A página da mesa não mostra feedback visual enquanto a requisição de login está em andamento.

**12. Preços em centavos sem validação mínima**  
Um produto pode ser criado com `priceInCents: 0` ou negativo sem erro.

---

## 14. Oportunidades de melhoria

### Segurança

- Implementar validação de CPF (algoritmo oficial dos dígitos verificadores)
- Mover secrets para variáveis de ambiente reais (`.env` fora do git)
- Adicionar rate limiting nos endpoints de login
- Implementar revogação de refresh tokens
- Adicionar validação de WhatsApp (formato brasileiro)

### Performance

- Adicionar paginação nos endpoints de listagem (`?page=1&limit=20`)
- No evento SignalR, enviar apenas a comanda modificada em vez de buscar todas
- Adicionar cache em memória para o cardápio (produtos raramente mudam)

### Funcionalidades

- Controle real de estoque ao adicionar à comanda (reserva temporária)
- Histórico de comandas fechadas com filtros por data
- Relatório de vendas diárias/mensais
- Sistema de notificação (WhatsApp/email) quando a comanda é fechada
- Múltiplos admins com níveis de acesso diferentes (garçom vs gerente)
- Sistema de fila de pedidos para a cozinha/bar

### Qualidade de código

- Adicionar testes unitários nos Services
- Adicionar testes de integração nos Controllers
- Configurar CI/CD (GitHub Actions) para rodar os testes automaticamente
- Adicionar logging estruturado (Serilog) para facilitar debug em produção

---

## 15. Glossário

| Termo | Definição no contexto do projeto |
|---|---|
| **Comanda** | Conta aberta de um cliente numa mesa. Equivale a um "pedido". |
| **Mesa** | Identificador da mesa física (ex: "Mesa-3"). Vem do QR Code. |
| **Admin** | Dono/gerente da loja. Acessa o painel de gestão. |
| **Cliente** | Jogador que está na mesa e faz pedidos. |
| **JWT** | Token de acesso que prova quem você é para a API. |
| **SignalR** | Tecnologia de comunicação em tempo real (WebSocket). |
| **Seed** | Dados iniciais inseridos no banco quando criado pela primeira vez. |
| **Migration** | Script que altera a estrutura do banco sem perder os dados. |
| **DTO** | Objeto que define o formato dos dados trafegados pela API. |
| **RBAC** | Controle de acesso baseado em papel (Admin vs Cliente). |
| **BCrypt** | Algoritmo de hash para senhas (irreversível e seguro). |
| **CORS** | Política que define quais origens podem chamar a API. |
| **Docker Compose** | Ferramenta que sobe todos os serviços do projeto de uma vez. |
| **Container** | Ambiente isolado onde cada serviço roda sem interferir nos outros. |
| **TCG** | Trading Card Game — jogos de cartas colecionáveis. |

---

*Documentação gerada em 04/05/2026 — Projeto softNerd*  
*Versão da API: CardGameStore v1 (.NET 8) | Frontend: Next.js 14*
