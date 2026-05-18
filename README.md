# softNerd — Sistema de Gestão para Lojas de Card Games

> Plataforma completa para gerenciamento de lojas especializadas em card games (Pokémon, Magic: The Gathering, Yu-Gi-Oh! e outros), com painel administrativo moderno, assistente IA, conformidade LGPD e autenticação segura via HttpOnly Cookies.

---

## Sumário

- [Visão Geral](#visão-geral)
- [Funcionalidades](#funcionalidades)
- [Stack Tecnológico](#stack-tecnológico)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Instalação e Execução](#instalação-e-execução)
- [Testes](#testes)
- [API Reference](#api-reference)
- [Conformidade LGPD](#conformidade-lgpd)
- [Segurança](#segurança)
- [Licença](#licença)

---

## Visão Geral

O softNerd é um sistema de gestão desenvolvido especificamente para o nicho de lojas de card games. Centraliza controle de estoque, vendas, crediário, anúncios, precificação automática via APIs de terceiros e comunicação em tempo real entre atendentes e administradores.

A plataforma é composta por uma API REST em ASP.NET Core, um frontend Next.js com App Router e um banco de dados PostgreSQL, totalmente containerizável via Docker.

---

## Funcionalidades

### Gestão de Produtos e Estoque
- Cadastro de cards com integração às APIs oficiais: **Pokémon TCG**, **Magic: The Gathering (Scryfall)** e **Yu-Gi-Oh! (ygoprodeck.com)**
- Busca automática de imagens, nomes e metadados dos cards via APIs externas
- Controle de estoque com alertas de quantidade mínima
- Upload de imagens direto no sistema (drag-and-drop no painel admin, JPEG/PNG/WebP, máx 5 MB)

### Vendas e Crediário
- Registro de vendas com múltiplos métodos de pagamento
- Painel de crediário (`/admin/crediario`) com controle de parcelas, vencimentos e inadimplência
- Histórico de transações por cliente

### Painel Administrativo
- Dashboard com métricas em tempo real via **SignalR** (SSE + Long Polling)
- Gestão de anúncios e promoções
- Gerenciamento de usuários e permissões
- **Assistente IA conversacional** (Google Gemini 2.0 Flash) — chat flutuante para perguntas sobre o negócio em linguagem natural, disponível no painel admin
- Gerenciamento de solicitações LGPD pelo painel dedicado (`/admin/lgpd`)

### Autenticação e Segurança
- JWT armazenado em **HttpOnly Cookies** (accessToken + refreshToken) — sem localStorage, sem js-cookie
- Cookies com flags `HttpOnly` e `Secure` para proteção contra XSS
- Refresh token automático com rotação
- BCrypt para hash de senhas
- `UseForwardedHeaders` para leitura correta de `X-Forwarded-For` em ambientes com proxy reverso

### Conformidade LGPD
- Página pública `/lgpd` para exercício de direitos (acesso, correção, exclusão, portabilidade)
- Página `/privacidade` (Política de Privacidade) e `/termos` (Termos de Uso)
- Banner de cookies com aceite granular (`CookieBanner`)
- Formulário de solicitação com validação de CPF pelo **Módulo 11**
- Audit log automático com IP anonimizado via **SHA-256**
- Painel admin `/admin/lgpd` para gestão e resposta de solicitações

### Experiência do Usuário
- **Tema claro/escuro** (dark/light mode) com `ThemeToggle` disponível em todas as páginas
- Interface responsiva com Tailwind CSS e Radix UI
- PWA com auto-hide da barra de navegação
- Notificações em tempo real via SignalR

---

## Stack Tecnológico

### Backend

| Tecnologia | Versão | Finalidade |
|---|---|---|
| ASP.NET Core | 8.0 | API REST principal |
| Entity Framework Core | 8.x | ORM e migrações |
| PostgreSQL | 16 | Banco de dados relacional |
| SignalR | 8.x | Comunicação em tempo real (SSE + Long Polling) |
| JWT (HttpOnly Cookies) | — | Autenticação stateless segura |
| BCrypt.Net | — | Hash de senhas |
| Google Gemini 2.0 Flash | — | Assistente IA (HTTP direto, sem SDK) |
| xUnit + Moq + FluentAssertions | — | Testes unitários e de integração |
| coverlet | — | Cobertura de código |

### Frontend

| Tecnologia | Versão | Finalidade |
|---|---|---|
| Next.js (App Router) | 14.x | Framework React SSR/SSG |
| TypeScript | 5.x | Tipagem estática |
| Tailwind CSS | 3.x | Estilização utilitária |
| Radix UI | — | Componentes acessíveis |
| SignalR Client | — | Tempo real com `withCredentials` (cross-origin cookie compatível) |
| next-themes | — | Dark/light mode |

### Infraestrutura

| Tecnologia | Finalidade |
|---|---|
| Docker + Docker Compose | Containerização |
| Nginx | Proxy reverso |
| GitHub Actions | CI/CD |

---

## Estrutura do Projeto

```
softNerd/
├── backend/
│   ├── softNerd.API/              # Controllers, Middleware, Program.cs
│   ├── softNerd.Application/      # Services, DTOs, Interfaces
│   ├── softNerd.Domain/           # Entidades, enums
│   ├── softNerd.Infrastructure/   # Repositórios, EF, Migrations
│   └── softNerd.Tests/            # Testes unitários (~85 testes)
│       ├── AuditServiceTests.cs   # 6 testes
│       ├── LgpdServiceTests.cs    # 8 testes
│       └── AnnouncementServiceTests.cs
├── frontend/
│   └── app/
│       ├── (public)/
│       │   ├── privacidade/       # Política de Privacidade
│       │   ├── termos/            # Termos de Uso
│       │   └── lgpd/              # Exercício de direitos LGPD
│       └── admin/
│           ├── lgpd/              # Painel LGPD admin
│           ├── crediario/         # Painel de crediário
│           └── ...
├── frontend/components/
│   ├── admin/
│   │   ├── AiChatWidget.tsx       # Chat flutuante com Gemini
│   │   └── ImageUpload.tsx        # Upload drag-and-drop
│   ├── CookieBanner.tsx           # Banner de cookies LGPD
│   └── ThemeToggle.tsx            # Alternador dark/light
└── docker-compose.yml
```

---

## Instalação e Execução

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (recomendado)
- PostgreSQL 16 (ou via Docker)

### Via Docker Compose (recomendado)

```bash
git clone https://github.com/seu-usuario/softNerd.git
cd softNerd
cp .env.example .env
# Edite .env com suas variáveis (ver seção de variáveis abaixo)
docker-compose up --build
```

A aplicação estará disponível em:
- Frontend: `http://localhost:3000`
- API: `http://localhost:5000`

### Manual

**Backend:**
```bash
cd backend/softNerd.API
dotnet restore
dotnet ef database update
dotnet run
```

**Frontend:**
```bash
cd frontend
npm install
npm run dev
```

### Variáveis de Ambiente

```env
# Banco de dados
DATABASE_URL=Host=localhost;Database=softnerd;Username=postgres;Password=sua_senha

# JWT
JWT_SECRET=sua_chave_secreta_longa
JWT_ISSUER=softNerd
JWT_AUDIENCE=softNerd-client
ACCESS_TOKEN_EXPIRES_MINUTES=15
REFRESH_TOKEN_EXPIRES_DAYS=7

# Cookies
COOKIE_DOMAIN=localhost
COOKIE_SECURE=false  # true em produção (HTTPS)

# IA
GEMINI_API_KEY=sua_chave_gemini

# APIs externas (opcionais)
POKEMON_TCG_API_KEY=sua_chave_pokemon
```

---

## Testes

O projeto possui aproximadamente **85 testes** cobrindo serviços, regras de negócio e conformidade LGPD.

```bash
cd backend
dotnet test

# Com cobertura de código (coverlet)
dotnet test --collect:"XPlat Code Coverage"
```

### Cobertura por módulo

| Módulo | Testes |
|---|---|
| AuditService | 6 |
| LgpdService | 8 |
| AnnouncementService | ✓ |
| Demais serviços | ~71 |
| **Total** | **~85** |

---

## API Reference

### Autenticação

| Método | Endpoint | Descrição |
|---|---|---|
| `POST` | `/api/auth/login` | Login — define cookies HttpOnly |
| `POST` | `/api/auth/refresh` | Renova accessToken via refreshToken cookie |
| `POST` | `/api/auth/logout` | Invalida tokens e limpa cookies |

> Todos os endpoints protegidos leem o JWT do cookie `accessToken` automaticamente. Não é necessário enviar o header `Authorization: Bearer`.

### Produtos e Estoque

| Método | Endpoint | Descrição |
|---|---|---|
| `GET` | `/api/products` | Lista produtos com paginação e filtros |
| `POST` | `/api/products` | Cria produto |
| `PUT` | `/api/products/{id}` | Atualiza produto |
| `DELETE` | `/api/products/{id}` | Remove produto |

### Upload de Imagens

| Método | Endpoint | Descrição |
|---|---|---|
| `POST` | `/api/upload/image` | Upload de imagem (JPEG/PNG/WebP, máx 5 MB) |

### APIs de Cards

| Método | Endpoint | Descrição |
|---|---|---|
| `GET` | `/api/cards/pokemon/search?q={nome}` | Busca cards Pokémon |
| `GET` | `/api/cards/mtg/search?q={nome}` | Busca cards MTG (Scryfall) |
| `GET` | `/api/cards/yugioh/search?q={nome}` | Busca cards Yu-Gi-Oh! (ygoprodeck) |

### LGPD

| Método | Endpoint | Descrição |
|---|---|---|
| `POST` | `/api/lgpd/solicitacao` | Registra solicitação de titular (valida CPF Módulo 11) |
| `GET` | `/api/lgpd/solicitacoes` | Lista solicitações (admin) |
| `PUT` | `/api/lgpd/solicitacoes/{id}` | Atualiza status da solicitação (admin) |

### Assistente IA

| Método | Endpoint | Descrição |
|---|---|---|
| `POST` | `/api/ai/chat` | Envia mensagem ao assistente Gemini 2.0 Flash |

---

## Conformidade LGPD

O softNerd implementa os requisitos da **Lei Geral de Proteção de Dados (Lei 13.709/2018)**:

- **Base legal documentada** para cada finalidade de tratamento
- **Exercício de direitos** dos titulares via formulário público em `/lgpd` com validação de CPF (Módulo 11)
- **Audit log imutável** de todas as operações sensíveis, com IP anonimizado (SHA-256)
- **Minimização de dados** — coleta apenas o necessário para cada operação
- **Banner de cookies** com aceite granular e registro de consentimento
- **Painel administrativo** (`/admin/lgpd`) para gestão das solicitações com prazos legais (15 dias)
- **Política de Privacidade** e **Termos de Uso** em páginas públicas acessíveis

---

## Segurança

| Medida | Implementação |
|---|---|
| Autenticação | JWT em HttpOnly Cookies (não acessível via JS) |
| Senhas | BCrypt com salt aleatório |
| CSRF | SameSite=Strict nos cookies de sessão |
| XSS | HttpOnly Cookies + Content Security Policy |
| SQL Injection | Entity Framework Core (queries parametrizadas) |
| IP de clientes | `UseForwardedHeaders` + `X-Forwarded-For` |
| Dados pessoais em logs | IP anonimizado via SHA-256 |
| HTTPS | Forçado em produção (`COOKIE_SECURE=true`) |

---

## Licença

Este software é proprietário e confidencial. Todos os direitos reservados.

Consulte o arquivo [LICENSE](./LICENSE) para os termos completos de uso.

---

*softNerd — Gestão inteligente para lojas de card games. Atualizado em 15/05/2026.*
