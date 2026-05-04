# softNerd — CardGameStore

Sistema de gestão para loja de card games (TCG). Gerencia comandas de mesa via QR Code, vendas no balcão, campeonatos e estoque — tudo em uma única plataforma web.

---

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core 8 (C#), Entity Framework Core |
| Banco de dados | PostgreSQL 16 (produção) / SQLite (dev local) |
| Cache TCG | MongoDB 7 |
| Tempo real | SignalR (WebSockets) |
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
Edite o arquivo `.env` e troque os valores pelos seus:
```env
JWT_SECRET=SuaChaveSuperSecretaAqui
POSTGRES_PASSWORD=SuaSenhaAqui
```

### 3. Suba os containers
```bash
docker compose up --build
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

### 🛒 Venda Avulsa (balcão)
O admin seleciona produtos, informa o cliente (opcional) e registra a venda sem precisar de login do cliente.
- Rota admin: `/admin/venda-avulsa`
- Endpoint: `POST /api/comanda/venda-avulsa`

### 📱 Mesas via QR Code
Clientes escaneiam o QR Code da mesa, fazem login com CPF + WhatsApp, e acessam sua comanda.
- Login automático cria conta se for a primeira visita
- Comanda fica salva no servidor (cliente pode sair e voltar)
- Só o admin fecha a comanda

### 🏆 Campeonatos TCG
Admin cria campeonatos com data, jogo, taxa de inscrição e vagas. Clientes se inscrevem via landing page ou painel.

### ⭐ Pontos
Admin adiciona pontos aos clientes pelo painel. Pontos têm validade de 30 dias e podem ser usados para abater o valor da comanda.

### 🌐 Landing Page Pública
Página inicial com campeonatos em destaque, produtos disponíveis e informações da loja — sem precisar de login.

---

## Estrutura do projeto

```
softNerd/
├── CardGameStore/          ← API ASP.NET Core 8
│   ├── Controllers/        ← Endpoints REST
│   ├── Services/           ← Regras de negócio
│   ├── Models/             ← Entidades do banco
│   ├── DTOs/               ← Objetos de transferência
│   └── Data/               ← DbContext e configurações EF
├── frontend/               ← Next.js 14
│   ├── app/                ← Páginas (App Router)
│   │   ├── page.tsx        ← Landing page pública
│   │   ├── admin/          ← Painel administrativo
│   │   ├── cliente/        ← Área do cliente
│   │   └── mesa/[mesa]/    ← Login via QR Code
│   ├── components/         ← Componentes reutilizáveis
│   └── lib/                ← API client, auth, SignalR
├── docker-compose.yml      ← Orquestração dos containers
├── Makefile                ← Comandos de conveniência
├── .env.example            ← Template de variáveis de ambiente
└── .env                    ← Variáveis reais (não vai pro Git)
```

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

> ⚠️ **Nunca commite o arquivo `.env` no Git.** Ele já está no `.gitignore`.

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
