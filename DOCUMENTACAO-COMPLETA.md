# softNerd — Documentação Técnica

> Referência técnica para desenvolvedores. Atualizada em 06/05/2026.

---

## 1. Visão geral

O **softNerd** é um sistema de gestão para loja de card games com mesas de jogos. Atende três fluxos de negócio principais:

1. **Venda Avulsa** — Admin vende produtos no balcão sem exigir login do cliente. Desconto opcional, pagamento em múltiplos métodos, estoque decrementado no PostgreSQL e evento registrado no MongoDB.
2. **Comanda de Mesa** — Cliente escaneia QR Code da mesa, faz login rápido (CPF + WhatsApp), adiciona itens ao pedido. Admin acompanha em tempo real via SignalR e fecha/cancela quando quiser.
3. **Campeonatos TCG** — Admin cria torneios, jogadores se inscrevem, o sistema gerencia vagas, decks e colocação final.

---

## 2. Arquitetura

### Stack

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core 8, C# |
| ORM | Entity Framework Core 8 (PostgreSQL) |
| Banco relacional | PostgreSQL 16 |
| Banco de documentos | MongoDB 7 |
| Autenticação | JWT (HS256) + BCrypt |
| Tempo real | SignalR (WebSocket) |
| Frontend | Next.js 14, TypeScript, Tailwind CSS |
| Infraestrutura | Docker + Docker Compose |

### Diagrama ASCII

```
  Admin (Maikon)              Cliente (Jogador)
        │                           │
        ▼                           ▼
  localhost:3000/admin     localhost:3000/mesa/3
        │                           │
        └─────────┬─────────────────┘
                  │ HTTP + WebSocket
                  ▼
        ASP.NET Core 8 — porta 5000
         ┌──────────────────────────┐
         │  Controllers (REST API)  │
         │  SignalR Hub (ComandaHub)│
         │  JWT Middleware          │
         └──────┬──────────┬────────┘
                │          │
         ┌──────▼──┐  ┌────▼──────────┐
         │Postgres │  │   MongoDB      │
         │  5432   │  │    27017       │
         │         │  │                │
         │ users   │  │ vendas_avulsas │
         │ products│  │ card_cache     │
         │ comandas│  └────────────────┘
         │ champs. │
         └─────────┘
```

**PostgreSQL** é a fonte da verdade para estoque. **MongoDB** armazena eventos de venda avulsa (imutáveis, estilo caixa) e cache de cartas TCG.

---

## 3. Como rodar

### Pré-requisito

Docker Desktop instalado e em execução.

### Subir tudo

```powershell
# Na raiz do repositório (pasta softNerd)
.\start.ps1
```

O script faz o build, sobe PostgreSQL + MongoDB + API + frontend, executa `EnsureCreatedAsync` e cria o usuário admin via seed.

- Primeira execução: ~3-5 min (download das imagens)
- Execuções seguintes: ~30 s

### URLs

| Serviço | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API + Swagger | http://localhost:5000 |
| PostgreSQL | localhost:5432 |
| MongoDB | localhost:27017 |
| pgAdmin (opcional) | http://localhost:5050 |

### Credenciais padrão (desenvolvimento)

| Recurso | Usuário | Senha |
|---|---|---|
| Admin da API | admin@cardgamestore.com.br | SenhaForte@123 |
| PostgreSQL | cardgame_user | CardGame@2025 |
| pgAdmin | admin@cardgame.com | admin |

### Parar

```powershell
docker-compose down          # mantém os dados
docker-compose down -v       # apaga volumes (reseta banco)
```

---

## 4. Estrutura do projeto

```
softNerd/
├── CardGameStore/                   # API Backend (ASP.NET Core 8)
│   ├── Controllers/                 # Endpoints REST
│   ├── Services/
│   │   ├── Interfaces/              # Contratos (IAuthService, IComandaService…)
│   │   └── Implementations/         # Lógica de negócio real
│   ├── Models/
│   │   ├── PostgreSQL/              # Entidades EF Core (User, Product, Comanda…)
│   │   └── MongoDB/                 # Documentos (VendaAvulsa, CardCache)
│   ├── DTOs/                        # Objetos de entrada/saída da API
│   ├── Data/AppDbContext.cs         # DbContext + seed do admin
│   ├── Hubs/ComandaHub.cs           # Hub SignalR para tempo real
│   └── Program.cs                   # Bootstrap: DI, JWT, CORS, EF, MongoDB
│
├── frontend/                        # Next.js 14 (App Router)
│   ├── app/
│   │   ├── admin/                   # Área administrativa
│   │   │   ├── dashboard/           # Comandas ao vivo + histórico do dia
│   │   │   ├── venda-avulsa/        # Caixa do balcão (sem login do cliente)
│   │   │   ├── estoque/             # CRUD de produtos
│   │   │   ├── campeonatos/         # Gestão de torneios
│   │   │   ├── cartas/              # Busca TCG
│   │   │   ├── usuarios/            # Gestão de clientes e pontos
│   │   │   └── qrcodes/             # Geração de QR Codes de mesa
│   │   ├── login/                   # Login do admin
│   │   └── mesa/[mesa]/             # Página do cliente (QR Code)
│   ├── lib/api.ts                   # Axios + interceptors JWT
│   └── components/                  # Componentes reutilizáveis
│
├── tests/unit/CardGameStore.Tests/  # Testes xUnit
│   └── Services/                    # Um arquivo por serviço
│
├── docker-compose.yml               # Orquestra todos os containers
└── start.ps1                        # Script de inicialização
```

---

## 5. Backend — Endpoints

### AuthController — `/api/auth`

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/api/auth/login` | Anônimo | Login admin (email + senha). Retorna JWT (60 min) + refresh token (30 dias). |
| POST | `/api/auth/quick-login` | Anônimo | Login cliente via QR Code (CPF + WhatsApp + mesa). Cria usuário se não existir. Abre comanda automaticamente. Retorna JWT + `comandaId`. |
| POST | `/api/auth/refresh` | Anônimo | Renova o access token usando o refresh token. |
| POST | `/api/auth/logout` | JWT | Invalida o refresh token do usuário autenticado. |

### VendaAvulsaController — `/api/venda-avulsa`

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/api/venda-avulsa` | Admin | Registra venda no balcão. Decrementa estoque (PostgreSQL) e persiste evento (MongoDB). Desconto aplicado no backend. |
| GET | `/api/venda-avulsa/recent` | Admin | Retorna as últimas N vendas avulsas (padrão 50, máximo 200). |

### ProductController — `/api/product`

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/product` | Anônimo | Lista todos os produtos ativos. Aceita `?category=` para filtrar. |
| GET | `/api/product/{id}` | Anônimo | Busca produto por ID. |
| GET | `/api/product/low-stock` | Admin | Lista produtos com estoque abaixo do mínimo. |
| POST | `/api/product` | Admin | Cria novo produto. |
| PUT | `/api/product/{id}` | Admin | Atualiza produto. |
| DELETE | `/api/product/{id}` | Admin | Desativa produto (soft delete). |
| PATCH | `/api/product/{id}/stock` | Admin | Ajusta estoque. `delta` positivo = entrada, negativo = saída. |

### ComandaController — `/api/comanda`

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/comanda/dashboard` | Admin | Lista todas as comandas abertas/em andamento. |
| GET | `/api/comanda/history` | Admin | Comandas fechadas/canceladas do dia. |
| GET | `/api/comanda/my` | JWT | Comanda ativa do usuário autenticado. |
| GET | `/api/comanda/{id}` | JWT | Detalhes de uma comanda específica. |
| POST | `/api/comanda/{id}/items` | JWT | Adiciona item. Preço resolvido pelo servidor — campo `unitPriceInCents` do cliente é ignorado para produtos cadastrados. |
| DELETE | `/api/comanda/{id}/items/{itemId}` | JWT | Remove item e restaura estoque. |
| POST | `/api/comanda/{id}/apply-points` | JWT | Aplica pontos do cliente como desconto (uma vez por comanda). |
| PUT | `/api/comanda/{id}/close` | Admin | Fecha a comanda (cobra). |
| PUT | `/api/comanda/{id}/cancel` | Admin | Cancela sem cobrar. Restaura estoque de todos os itens. |

### ChampionshipController — `/api/championship`

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/championship` | Anônimo | Lista campeonatos com status Planejado ou Inscricoes. |
| GET | `/api/championship/{id}` | Anônimo | Detalhes de um campeonato. |
| GET | `/api/championship/{id}/participants` | JWT | Lista participantes inscritos. |
| POST | `/api/championship` | Admin | Cria campeonato. |
| POST | `/api/championship/{id}/register` | JWT | Inscreve o usuário autenticado. `DeckName` opcional. |
| PUT | `/api/championship/{id}/status` | Admin | Muda status (Planejado → Inscricoes → EmAndamento → Finalizado/Cancelado). |
| PUT | `/api/championship/{id}/participants/{pid}/placement` | Admin | Define colocação final do participante. |

### UserController — `/api/user`

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/user` | Admin | Lista clientes. Aceita `?search=` para buscar por nome/CPF/WhatsApp. |
| GET | `/api/user/me` | JWT | Perfil completo do usuário autenticado (pontos, dados pessoais). |
| GET | `/api/user/{id}` | Admin | Detalhes de um cliente. |
| POST | `/api/user/{id}/points` | Admin | Adiciona pontos ao saldo do cliente. |

### TcgController — `/api/tcg`

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/tcg/search` | JWT | Pesquisa cartas por nome. Cache-first: consulta MongoDB antes da API externa. Parâmetros: `name`, `game`, `page`, `pageSize`. |
| GET | `/api/tcg/cards/{tcgCardId}` | JWT | Busca carta por ID prefixado (`pokemon:` ou `mtg:`). |
| GET | `/api/tcg/sets` | JWT | Lista sets disponíveis para um jogo. Parâmetro: `game`. |
| POST | `/api/tcg/cards/{tcgCardId}/refresh` | Admin | Força atualização do cache de uma carta. |
| DELETE | `/api/tcg/cards/{tcgCardId}/cache` | Admin | Remove uma carta do cache MongoDB. |
| POST | `/api/tcg/purge-cache` | Admin | Remove todos os documentos expirados do cache. |

---

## 6. Regras de negócio importantes

**Venda Avulsa**
- É um evento de caixa, não gera usuário no sistema. O campo `ClientName` é opcional e livre.
- Desconto calculado no backend a partir do `DiscountPercent` (0–100). O campo `TotalInReais` enviado pelo cliente é ignorado.
- A validação de todos os itens ocorre antes de qualquer escrita (fail-fast): se um produto não existe, está inativo ou com estoque insuficiente, nenhum estoque é decrementado.
- Estoque decrementado no PostgreSQL (`SaveChangesAsync`) antes da inserção no MongoDB. Se o MongoDB falhar após o SaveChanges, o estoque já foi decrementado mas o evento não fica registrado — o PostgreSQL é a fonte da verdade.

**Comanda**
- Ciclo de vida: `Aberta` → `EmAndamento` → `Fechada` | `Cancelada`.
- Ao cancelar, o estoque de todos os itens com `ProductId` é restaurado automaticamente.
- Preço de itens: quando `ProductId` está presente, o serviço busca o preço atual do banco e ignora qualquer valor enviado pelo cliente (evita manipulação de preço).
- Pontos só podem ser aplicados uma vez por comanda e antes de fechar. Pontos com `PointsExpiresAt` no passado são recusados.
- `PointsExpiresAt` é armazenado em UTC.

**Campeonatos**
- Inscrição só é aceita quando status = `Inscricoes`.
- Se `MaxParticipants` está definido, inscrições são recusadas quando a lista está cheia.
- O número do jogador (`PlayerNumber`) é gerado pelo serviço.

**TCG — Cache de Cartas**
- Estratégia Cache-Aside (Lazy Loading): lê MongoDB primeiro; se miss ou expirado (TTL de 7 dias), busca na API externa e salva no MongoDB.
- IDs de cartas são prefixados: `pokemon:` para PokemonTCG.io, `mtg:` para Scryfall.
- Pokemon → PokemonTCG.io API (`api.pokemontcg.io/v2`). MTG → Scryfall (`api.scryfall.com`). Outros jogos retornam lista vazia sem erro.
- Um TTL index no MongoDB também expira documentos automaticamente (além do `PurgeExpiredCacheAsync` manual).

---

## 7. Testes

Testes unitários com xUnit + Moq + FluentAssertions. EF Core InMemory para PostgreSQL; MongoDB mockado com Moq.

| Arquivo | Testes | Cobertura |
|---|---|---|
| `AuthServiceTests.cs` | 7 | Login admin, quick-login (novo/existente), refresh token, logout |
| `ChampionshipServiceTests.cs` | 10 | Criação, inscrição, limite de vagas, colocação final |
| `ComandaServiceTests.cs` | 13 | Abrir, adicionar item (preço servidor), remover, fechar, cancelar, pontos |
| `ProductServiceTests.cs` | 11 | CRUD, estoque baixo, ajuste de delta |
| `UserServiceTests.cs` | 11 | Listagem, busca, adição de pontos, expiração |
| `VendaAvulsaServiceTests.cs` | 10 | Registro com desconto, múltiplos produtos, fail-fast, GetRecent |
| **Total** | **62** | |

**Comando para rodar:**

```powershell
cd tests/unit/CardGameStore.Tests
dotnet test
```

---

## 8. Variáveis de ambiente

Configuradas no `docker-compose.yml` para o serviço `api`:

| Variável | Descrição | Valor padrão (dev) |
|---|---|---|
| `ConnectionStrings__PostgreSQL` | Connection string do PostgreSQL | `Host=postgres;Database=cardgamestore;Username=cardgame_user;Password=CardGame@2025` |
| `ConnectionStrings__MongoDB` | Connection string do MongoDB | `mongodb://mongo:27017` |
| `JwtSettings__SecretKey` | Chave de assinatura JWT (min. 32 chars) | `CardGameStore_SecretKey_2025_MinLength32Chars!` |
| `JwtSettings__Issuer` | Issuer do JWT | `http://localhost:5000` |
| `JwtSettings__Audience` | Audience do JWT | `http://localhost:3000` |
| `JwtSettings__AccessTokenExpirationMinutes` | Validade do access token | `60` |
| `JwtSettings__RefreshTokenExpirationDays` | Validade do refresh token | `30` |
| `TcgSettings__PokemonApiKey` | Chave da PokemonTCG.io (opcional, aumenta rate limit) | _(vazio)_ |
| `NEXT_PUBLIC_API_URL` | URL da API para o frontend | `http://localhost:5000` |
| `ASPNETCORE_ENVIRONMENT` | Ambiente da aplicação | `Production` (em Docker) |

---

## 9. Frontend — Páginas do Admin

Todas as páginas ficam em `frontend/app/admin/`. A autenticação é verificada pelo layout (`admin/layout.tsx`); rotas sem token válido redirecionam para `/login`.

| Página | Rota | Funcionalidades principais |
|---|---|---|
| Dashboard | `/admin/dashboard` | Comandas ativas em tempo real (SignalR), busca por cliente, adicionar item inline, badge de tempo colorido (verde/amarelo/vermelho), histórico do dia (tab), total consolidado |
| Venda Avulsa | `/admin/venda-avulsa` | Catálogo com chips de categoria + busca, carrinho, desconto rápido (0/5/10/15/20%), calculador de troco (modo Dinheiro), histórico do dia (tab) |
| Estoque | `/admin/estoque` | CRUD de produtos, ajuste de delta de estoque, alerta de estoque baixo |
| Campeonatos | `/admin/campeonatos` | Criar torneio, inscrever jogadores, mudar status, definir colocação |
| Cartas TCG | `/admin/cartas` | Busca por nome com filtro de jogo (Pokémon/MTG), visualização de preços de mercado |
| Usuários | `/admin/usuarios` | Lista de clientes, busca, saldo de pontos, adicionar pontos manualmente |
| QR Codes | `/admin/qrcodes` | Gerar QR Codes de mesa para impressão |

**Comunicação com a API:**
- `frontend/lib/api.ts` centraliza todas as chamadas. Axios com interceptor que injeta o JWT em todas as requisições e renova o token automaticamente ao receber 401.
- `frontend/lib/signalr.ts` gerencia a conexão WebSocket com o hub `/hubs/comanda`.

---

## 10. Requisitos Não Funcionais (RNF)

### 10.1 Segurança

| # | Requisito | Implementação |
|---|---|---|
| RNF-S1 | Autenticação baseada em token | JWT HS256, access token 60 min + refresh token 30 dias |
| RNF-S2 | Proteção de senhas | BCrypt com work factor padrão (≥12 rounds) |
| RNF-S3 | Rate limiting em endpoints sensíveis | FixedWindow: 5 req/min por IP em `/api/auth/{login,quick-login,refresh}`; 200 req/min por IP nos demais |
| RNF-S4 | Headers HTTP de segurança | X-Content-Type-Options, X-Frame-Options: DENY, X-XSS-Protection, Referrer-Policy, Permissions-Policy |
| RNF-S5 | HTTPS | Gerenciado pelo reverse proxy (Nginx/Cloudflare) — sem redirect interno |
| RNF-S6 | CORS restrito | Apenas origens explicitamente listadas; em produção substituir pelos domínios reais |
| RNF-S7 | Separação de papéis | RBAC via políticas JWT: `AdminOnly` e `CustomerOrAdmin` |
| RNF-S8 | Prevenção de cliques duplos | Frontend: botão de logout com `disabled` durante chamada; páginas com estado `submitting` |

### 10.2 Desempenho e Disponibilidade

| # | Requisito | Implementação |
|---|---|---|
| RNF-P1 | Timeout de requisição | 30 s padrão; 60 s para endpoints de busca TCG (`"long"`) |
| RNF-P2 | Resiliência do banco relacional | EF Core com retry automático em falhas transitórias (`EnableRetryOnFailure: 5`) |
| RNF-P3 | Graceful degradation do MongoDB | Se MongoDB estiver indisponível, as funcionalidades de VendaAvulsa e cache TCG ficam fora; o restante do sistema opera normalmente |
| RNF-P4 | Tempo real com fallback | SignalR usa WebSocket com fallback automático para Long Polling |
| RNF-P5 | Timeout de seleção de servidor MongoDB | 3 segundos — evita que a API trave aguardando um Mongo off-line |

### 10.3 Manutenibilidade

| # | Requisito | Implementação |
|---|---|---|
| RNF-M1 | Separação de responsabilidades | Controller → Service → Repository (EF Core). Regras de negócio nunca nos controllers. |
| RNF-M2 | Tipagem estática no frontend | TypeScript estrito; DTOs espelham os contratos da API |
| RNF-M3 | Testes unitários | xUnit + Moq; cobertura dos serviços principais (Auth, Comanda, VendaAvulsa, Product, User, Championship) |
| RNF-M4 | Configuração por ambiente | `appsettings.json` + variáveis de ambiente / Docker secrets; sem segredos em código |

### 10.4 Portabilidade

| # | Requisito | Implementação |
|---|---|---|
| RNF-O1 | Multi-banco relacional | SQLite em dev local, PostgreSQL em produção/Docker (seleção automática por connection string) |
| RNF-O2 | Containerização | `docker-compose.yml` com serviços api, frontend, postgres e mongo |

---

## 11. Pontos de atenção

- **Dupla escrita sem transação distribuída**: o decremento de estoque (PostgreSQL) e a gravação do evento (MongoDB) não estão em uma transação atômica. Se o MongoDB falhar após o `SaveChangesAsync`, o estoque fica decrementado mas sem registro de venda. Em produção, monitore os logs e considere reconciliação periódica.

- **EnsureCreatedAsync, não migrations**: mudanças de schema requerem intervenção manual (recriar o banco ou escrever SQL de alter). Migrar para `dotnet ef migrations` antes de qualquer alteração de schema em produção.

- **Rate limits das APIs TCG externas**: PokemonTCG.io limita requisições anônimas (~1000/dia). Configure `TcgSettings__PokemonApiKey` para aumentar o limite. Scryfall pede um delay de 100ms entre requisições; o cliente atual não implementa este throttle.

- **Sem validação de CPF**: o campo aceita qualquer string de 11 caracteres. CPFs inválidos podem ser cadastrados.

- **Refresh token sem revogação por lista negra**: um refresh token comprometido fica válido por 30 dias. A única forma de revogar é o próprio logout (que zera o campo no banco).

- **CORS permissivo**: em produção, atualizar `Program.cs` para aceitar apenas o domínio real em vez dos localhosts.

- **Secret JWT exposta no docker-compose**: o valor padrão deve ser substituído antes de qualquer deploy público. Veja o CHECKLIST-PRODUCAO.md.

- **Histórico de comandas limitado ao dia corrente**: `GET /api/comanda/history` filtra por `ClosedAt.Date == DateTime.UtcNow.Date`. Para acesso a dias anteriores seria necessário um novo endpoint com parâmetro de data.

- **Desconto de venda avulsa apenas em percentual fixo**: os botões da UI oferecem 0/5/10/15/20%. Não há campo de desconto em valor absoluto nem percentual personalizado.
