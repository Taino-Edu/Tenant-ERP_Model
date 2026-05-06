# softNerd — CardGameStore

Sistema de gestão para loja de card games (TCG). Gerencia comandas de mesa via QR Code, vendas no balcão, campeonatos, estoque e analytics — tudo em uma única plataforma web.

---

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core 8 (C#), Entity Framework Core 8 |
| Banco relacional | PostgreSQL 16 |
| Banco de documentos | MongoDB 7 |
| Autenticação | JWT HS256 + BCrypt + Refresh Token |
| Tempo real | SignalR (WebSockets) |
| Email | SMTP (reset de senha) |
| Frontend | Next.js 14, TypeScript, Tailwind CSS |
| Infra | Docker Compose |

---

## Como rodar

### Pré-requisitos
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) instalado e rodando

### 1. Clone o repositório
```bash
git clone https://github.com/seu-usuario/softNerd.git
cd softNerd
```

### 2. Configure as variáveis de ambiente
```bash
cp .env.example .env
```
Edite o `.env` e substitua os valores:
```env
JWT_SECRET=SuaChaveSuperSecretaAqui
POSTGRES_PASSWORD=SuaSenhaAqui
```

### 3. Suba os containers
```powershell
.\start.ps1
```

| Serviço | URL |
|---|---|
| Frontend (Next.js) | http://localhost:3000 |
| API (Swagger) | http://localhost:5000/swagger |
| pgAdmin (opcional) | http://localhost:5050 |

> Para subir o pgAdmin: `docker compose --profile tools up`

---

## Comandos úteis (Makefile)

```bash
make up        # sobe tudo
make down      # para os containers
make restart   # reinicia
make logs      # logs em tempo real
make build     # rebuild sem cache
make clean     # apaga tudo incluindo o banco (cuidado!)
make status    # status dos containers
```

---

## Funcionalidades

### Venda Avulsa (balcão)
Admin seleciona produtos, aplica desconto opcional e registra a venda sem precisar de login do cliente. Estoque decrementado no PostgreSQL; evento gravado no MongoDB.
- Rota admin: `/admin/venda-avulsa`
- Endpoint: `POST /api/venda-avulsa`

### Mesas via QR Code
Clientes escaneiam o QR Code da mesa, fazem login com CPF + WhatsApp e acessam sua comanda pessoal.
- Login automático cria conta se for a primeira visita
- Comanda fica salva no servidor (cliente pode sair e voltar)
- Só o admin fecha ou cancela a comanda
- Admin gera e imprime os QR Codes em `/admin/qrcodes` (download PNG individual, lote ZIP, impressão via `window.print`)

### Campeonatos TCG
Admin cria torneios com data, jogo, taxa e limite de vagas. Clientes se inscrevem pela landing page ou pelo painel.

### Programa de Pontos
Admin adiciona ou remove pontos pelo painel (`/admin/usuarios`). Pontos têm validade de 30 dias e podem ser aplicados como desconto na comanda — a opção só fica disponível para o cliente se o admin liberar.

### Anúncios e Landing Page
Admin gerencia banners, avisos e destaques em `/admin/anuncios`. A landing page pública exibe essas informações junto com campeonatos e produtos em destaque.

- Tipo `Banner`: imagem 1200×400px, JPEG/WebP, máx. 2 MB
- Tipo `Destaque`: imagem 800×600px, JPEG/WebP, máx. 1 MB
- Tipo `Aviso`: texto livre com título e descrição

### Analytics
Endpoints em `/api/analytics` expõem KPIs do dia, ticket médio (30 dias), curva horária de vendas, top 5 produtos, clientes ativos/inativos e insights individuais por cliente.

### Autenticação segura
- Login admin via e-mail + senha
- Reset de senha por e-mail (token 2h, sem user enumeration)
- Refresh token com revogação via logout

---

## Estrutura do projeto

```
softNerd/
├── CardGameStore/          ← API ASP.NET Core 8
│   ├── Controllers/        ← Endpoints REST
│   ├── Services/           ← Regras de negócio
│   ├── Models/             ← Entidades do banco
│   ├── DTOs/               ← Objetos de transferência
│   └── Data/               ← DbContext e seed
├── frontend/               ← Next.js 14 (sistema real)
│   ├── app/
│   │   ├── page.tsx        ← Landing page pública
│   │   ├── admin/          ← Painel administrativo (sidebar responsiva)
│   │   └── mesa/[mesa]/    ← Login e comanda via QR Code
│   ├── components/         ← Componentes reutilizáveis
│   └── lib/                ← API client, auth, SignalR
├── teste/                  ← Demo standalone com dados mockados (não sobe ao git)
│   ├── app/admin/          ← Todas as telas do admin com recharts
│   ├── app/comanda/        ← Visão mobile do cliente
│   └── app/loja/           ← Landing page pública
├── tests/unit/             ← xUnit (62 testes nos 6 serviços principais)
├── docker-compose.yml
└── start.ps1
```

---

## Testes

```powershell
cd tests/unit/CardGameStore.Tests
dotnet test
```

62 testes unitários cobrindo: Auth, Comanda, VendaAvulsa, Product, User, Championship.

---

## Variáveis de ambiente

| Variável | Descrição |
|---|---|
| `JWT_SECRET` | Chave secreta para assinar tokens JWT (mín. 32 chars) |
| `POSTGRES_USER` | Usuário do PostgreSQL |
| `POSTGRES_PASSWORD` | Senha do PostgreSQL |
| `POSTGRES_DB` | Nome do banco de dados |
| `PGADMIN_EMAIL` | E-mail para acesso ao pgAdmin |
| `PGADMIN_PASSWORD` | Senha para acesso ao pgAdmin |
| `SMTP_HOST` | Servidor SMTP para reset de senha |
| `SMTP_PORT` | Porta do servidor SMTP |
| `SMTP_USER` | Usuário SMTP |
| `SMTP_PASS` | Senha SMTP |

> **Nunca commite o arquivo `.env` no Git.** Ele já está no `.gitignore`.

---

## Autenticação

O sistema usa **JWT Bearer Tokens** com refresh automático.

| Perfil | Acesso | Login |
|---|---|---|
| Admin | Painel completo | E-mail + senha em `/login` |
| Cliente | Comanda da mesa | CPF + WhatsApp via QR Code |

Tokens de acesso expiram em **60 minutos**. O refresh token dura **30 dias**.

---

## Branches

| Branch | Descrição |
|---|---|
| `main` | Código estável — pronto para produção |
| `dev` | Desenvolvimento ativo — testar antes de mergear |

---

## Licença

Projeto privado — softNerd © 2025
