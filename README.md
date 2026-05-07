# softNerd — Sistema de Gestão para Lojas de Card Games

> Plataforma completa para lojas de TCG: comandas via QR Code, PDV, estoque, campeonatos, programa de pontos e analytics — tudo em uma única solução web.

---

## Visão Geral

O **softNerd** é um software de gestão desenvolvido especificamente para lojas de card games (TCG). Substitui planilhas e sistemas genéricos por uma plataforma pensada para o dia a dia do negócio: desde o cliente que escaneia o QR Code da mesa até o admin que acompanha tudo em tempo real no painel.

### Para quem é

- Lojas de card games com mesas de jogos (Pokémon, MTG, Yu-Gi-Oh, etc.)
- Estabelecimentos que organizam campeonatos e eventos
- Negócios que querem fidelizar clientes com programa de pontos

---

## Funcionalidades

### Comanda de Mesa via QR Code
Cada mesa tem um QR Code fixo. O cliente escaneia, faz login com CPF e WhatsApp (sem app, sem cadastro manual) e sua comanda é aberta automaticamente. O admin acompanha todas as comandas em tempo real com atualização via WebSocket.

### Ponto de Venda (PDV)
Frente de caixa para vendas no balcão sem exigir login do cliente. Catálogo com busca por categoria, desconto rápido (0–20%), calculador de troco para dinheiro e histórico do dia.

### Programa de Pontos
Clientes acumulam pontos a cada visita. O admin controla o saldo manualmente e pode permitir que o cliente use os pontos como desconto na comanda. Pontos têm validade de 30 dias.

### Crediário
Feche comandas no crediário com vencimento automático de 30 dias. O cliente recebe notificação por email e fica bloqueado de abrir novas comandas até quitar. Admin controla tudo pelo painel.

### Campeonatos TCG
Crie torneios com data, jogo, taxa de inscrição e limite de vagas. Clientes se inscrevem pela landing page. Admin gerencia participantes, status e colocação final.

### Anúncios e Landing Page
Painel para gerenciar banners, avisos e destaques que aparecem na landing page pública da loja. Suporta imagens externas (Cloudflare R2, Imgur, etc.).

### Analytics
Dashboard com KPIs do dia: faturamento vs dia anterior, ticket médio (30 dias), curva horária de vendas, top 5 produtos e análise de clientes ativos/inativos.

### Busca de Cartas TCG
Integração com PokémonTCG.io e Scryfall (MTG) com cache local em MongoDB para consultas instantâneas de preços de mercado.

### QR Codes de Mesa
Geração, download (PNG individual ou ZIP em lote) e impressão de QR Codes para todas as mesas da loja.

### Notificações por Email
Emails automáticos para: reset de senha, boas-vindas, crediário aberto/quitado, confirmação de inscrição em campeonato e divulgação de anúncios/promoções.

---

## Stack Tecnológica

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core 8 (C#), Entity Framework Core 8 |
| Banco relacional | PostgreSQL 16 |
| Banco de documentos | MongoDB 7 |
| Autenticação | JWT HS256 + BCrypt + Refresh Token |
| Tempo real | SignalR (WebSockets) |
| Email | SMTP (Gmail, SendGrid, Resend) |
| Frontend | Next.js 14, TypeScript, Tailwind CSS |
| Infra | Docker Compose |
| Testes | xUnit, Moq, FluentAssertions (62 testes) |

---

## Como Executar

### Pré-requisito
[Docker Desktop](https://www.docker.com/products/docker-desktop/) instalado e rodando.

### 1. Clone o repositório
```bash
git clone https://github.com/Taino-Edu/softNerd.git
cd softNerd
```

### 2. Configure as variáveis de ambiente
```bash
cp .env.example .env
```
Edite o arquivo `.env` com suas configurações (JWT secret, senhas do banco, SMTP).

### 3. Suba os containers
```powershell
.\start.ps1
```

| Serviço | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API + Swagger | http://localhost:5000/swagger |
| pgAdmin (opcional) | http://localhost:5050 |

**Primeira execução:** ~3–5 min (download das imagens Docker)
**Execuções seguintes:** ~30 segundos

### Credenciais padrão (desenvolvimento)
| Recurso | Usuário | Senha |
|---|---|---|
| Admin | admin@cardgamestore.com.br | SenhaForte@123 |
| PostgreSQL | cardgame_user | CardGame@2025 |

> Troque todas as senhas antes de qualquer deploy em produção. Consulte `CHECKLIST-PRODUCAO.md`.

---

## Estrutura do Projeto

```
softNerd/
├── CardGameStore/          ← API ASP.NET Core 8
│   ├── Controllers/        ← Endpoints REST (Auth, Comanda, PDV, Analytics…)
│   ├── Services/           ← Regras de negócio
│   ├── Models/             ← Entidades PostgreSQL + documentos MongoDB
│   ├── DTOs/               ← Contratos de entrada/saída da API
│   └── Data/               ← DbContext + seed do admin
├── frontend/               ← Next.js 14
│   ├── app/
│   │   ├── page.tsx        ← Landing page pública
│   │   ├── admin/          ← Painel administrativo (sidebar responsiva)
│   │   └── mesa/[mesa]/    ← Comanda do cliente via QR Code
│   ├── components/         ← Componentes reutilizáveis
│   └── lib/                ← API client, auth, SignalR
├── tests/unit/             ← xUnit (62 testes nos serviços principais)
├── docker-compose.yml      ← Orquestração dos containers
├── .env.example            ← Template de variáveis de ambiente
└── start.ps1               ← Script de inicialização
```

---

## Comandos Úteis

```bash
make up        # sobe todos os containers
make down      # para os containers (mantém dados)
make restart   # reinicia
make logs      # logs em tempo real
make build     # rebuild sem cache
make clean     # apaga tudo incluindo o banco (cuidado!)
make status    # status dos containers
```

---

## Testes

```powershell
cd tests/unit/CardGameStore.Tests
dotnet test
```

62 testes unitários cobrindo os serviços principais: Auth, Comanda, VendaAvulsa, Product, User, Championship.

---

## Deploy em Produção

Consulte [`CHECKLIST-PRODUCAO.md`](./CHECKLIST-PRODUCAO.md) para o guia completo de deploy, incluindo configuração de HTTPS, CORS, backups e troca de credenciais.

---

## Licenciamento Comercial

Este software é **proprietário**. O código-fonte é disponibilizado exclusivamente para avaliação técnica. A implantação em ambiente comercial requer contrato de licenciamento.

Para contratação ou informações comerciais, consulte o arquivo [`LICENSE`](./LICENSE).

---

## Branches

| Branch | Descrição |
|---|---|
| `main` | Código estável — testado e aprovado |
| `dev` | Desenvolvimento ativo |

---

softNerd © 2025 — Todos os direitos reservados.
