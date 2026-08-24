# SantuárioNerd — Documentação Técnica Completa
**Versão**: 2.1 | **Data**: 2026-06-23 | **Autor**: Engenharia | **Sistema em**: v1.7.4

---

## Índice

1. [Visão Geral do Sistema](#1-visão-geral-do-sistema)
2. [Arquitetura de Infraestrutura](#2-arquitetura-de-infraestrutura)
3. [Schema do Banco de Dados](#3-schema-do-banco-de-dados)
4. [Mapa Completo de Endpoints](#4-mapa-completo-de-endpoints)
5. [Auditoria de Backend](#5-auditoria-de-backend)
6. [Auditoria de Frontend](#6-auditoria-de-frontend)
7. [Auditoria de Infraestrutura e Banco](#7-auditoria-de-infraestrutura-e-banco)
8. [Pontos Fortes do Sistema](#8-pontos-fortes-do-sistema)
9. [Plano de Correções Priorizadas](#9-plano-de-correções-priorizadas)
10. [Roadmap de Escalabilidade](#10-roadmap-de-escalabilidade)

---

## 1. Visão Geral do Sistema

### 1.1 Propósito

O SantuárioNerd é um **sistema de gestão completo para loja de TCG (Trading Card Games)**. Resolve o problema específico de lojas que operam simultaneamente como varejo, espaço de lazer e clube competitivo — contextos que nenhum ERP genérico atende.

### 1.2 Stack Tecnológica

| Camada | Tecnologia | Versão |
|--------|-----------|--------|
| Backend API | ASP.NET Core | 8.0 |
| ORM | Entity Framework Core (Npgsql) | 8.0.10 |
| Banco Relacional | PostgreSQL | 16 |
| Banco de Eventos | MongoDB | 7 |
| Frontend | Next.js (App Router) | 14 |
| UI | TypeScript + Tailwind CSS | — |
| Real-time | SignalR (WebSocket) | — |
| IA | Google Gemini API | — |
| Container | Docker Compose | — |
| Reverse Proxy | Nginx | 1.27 |

### 1.3 Módulos Funcionais

```
Sistema SantuárioNerd
├── Marketplace Público      → Landing page + catálogo /produtos
├── Comanda Digital          → Cliente abre, adiciona itens, Maikon fecha
│   └── Histórico filtrado   → Busca por nome + intervalo de horário (v1.7.4)
├── PDV (Venda Avulsa)       → Venda de balcão sem comanda, sem cliente cadastrado
├── Estoque (Produtos)       → CRUD + visibilidade marketplace vs PDV
├── Crediário                → Fiado com pagamento parcial
│   ├── Recebimentos/período → Crediários recebidos no financeiro com modal (v1.7.3)
│   └── Relatório PDF        → Situação atual de devedores + pagamentos do mês (v1.7.3)
├── Campeonatos              → Organização, inscrição, pódio
├── Analytics                → Dashboard KPIs + insights financeiros
│   └── Filtro por pagamento → Financeiro filtrável por forma de pagamento (v1.7.3)
├── Usuários & Perfis        → Roles Admin/Operator/Client, permissões granulares
├── TCG (Cards Cache)        → Busca de cartas Pokémon/MTG/YuGiOh via APIs externas
├── LGPD                     → Compliance: direitos do titular, audit log
└── IA Chat                  → Chat Gemini com contexto de dados da loja
```

### 1.4 Atores e Contextos de Uso

| Ator | Como usa o sistema |
|------|-------------------|
| **Dono (Admin)** | Todos os painéis. Cria produtos, fecha comandas, visualiza analytics, responde LGPD |
| **Operador (Maikon)** | Mesmo que Admin exceto configurações críticas |
| **Cliente autenticado** | Abre comanda via mesa, adiciona itens, aplica pontos, vê histórico |
| **Público anônimo** | Vê marketplace, vê campeonatos, pré-inscrição, solicitação LGPD |

### 1.5 Regra de Visibilidade de Produtos (crítica)

```
showOnMarketplace = true  → aparece no marketplace público e nas comandas
showOnMarketplace = false → aparece APENAS nas comandas e PDV (não no marketplace)
isActive = false          → some de tudo (marketplace + comandas + PDV)
```

**Três endpoints de produto:**
- `GET /api/product` → `[AllowAnonymous]` → filtra `isActive && showOnMarketplace`
- `GET /api/product/store` → `[Authorize]` → filtra apenas `isActive`
- `GET /api/product/admin` → `[Authorize Admin/Operator]` → filtra apenas `isActive`

---

## 2. Arquitetura de Infraestrutura

### 2.1 Topologia de Deployment

```
Internet (HTTPS via Cloudflare TLS 1.3)
         │
         ▼
    Hostinger KVM1 (VPS Linux)
         │
    Nginx :80 (HTTP interno)
         │
    ┌────┴────────────────────┐
    │                         │
    ▼                         ▼
Frontend :3000            API :5000
Next.js SSR               ASP.NET Core 8
         │                    │
         │              ┌─────┴──────┐
         │              ▼            ▼
         │         PostgreSQL   MongoDB
         │          :5432        :27017
         │
    Rede Docker: cardgame_network (bridge)
```

### 2.2 Serviços Docker

| Serviço | Imagem | Porta | Volumes | Health Check |
|---------|--------|-------|---------|--------------|
| nginx | nginx:1.27-alpine | 80→80 | — | curl /health |
| frontend | node:20-alpine | 3000 (interno) | — | GET / |
| api | custom dotnet:8 | 5000 (interno) | api_uploads | GET /health |
| postgres | postgres:16-alpine | 5432 (interno) | postgres_data | pg_isready |
| mongodb | mongo:7-jammy | 27017 (interno) | mongo_data | mongosh ping |

### 2.3 Fluxo de Autenticação JWT

```
1. POST /api/auth/login → API valida credenciais
2. API gera AccessToken (60 min) + RefreshToken (30 dias)
3. Ambos armazenados em cookies HttpOnly (HTTPS) → proteção XSS
4. Browser envia cookie automaticamente (withCredentials: true)
5. Axios interceptor detecta 401 → chama POST /api/auth/refresh
6. Se refresh falhar → redirect para /entrar
```

### 2.4 SignalR (Real-time)

- Protocolo: WebSocket com fallback para SSE e Long Polling
- Hub: `/hubs/comanda`
- Autenticação: JWT via query string (`?access_token=...`) quando cookie não está disponível
- Grupos: Por comanda ID para notificar apenas os participantes relevantes

---

## 3. Schema do Banco de Dados

### 3.1 Diagrama de Entidades (PostgreSQL)

```
users
 ├── id (uuid, PK)
 ├── email (unique, nullable — clientes podem usar CPF)
 ├── cpf (unique, nullable)
 ├── whatsapp
 ├── name
 ├── role (Admin | Operator | Client)
 ├── perfil_id (FK → perfis, nullable)
 ├── points_balance, monetary_balance_in_cents
 ├── refresh_token, refresh_token_expires_at
 └── profile_image_url

products
 ├── id (uuid, PK)
 ├── name, category, barcode (unique, nullable)
 ├── price_in_cents, cost_price_in_cents
 ├── discount_price_in_cents (nullable — promoção)
 ├── stock_quantity, min_stock_alert
 ├── is_active, is_featured, is_pre_venda
 ├── show_on_site (LEGACY — não filtra mais nada)
 ├── show_on_marketplace (boolean — controla marketplace)
 ├── image_url, image_urls[] (galeria)
 └── full_description (TEXT, nullable)

comandas
 ├── id (uuid, PK)
 ├── user_id (FK → users, RESTRICT)
 ├── status (Open | Closed | Cancelled)
 ├── table_number
 ├── total_in_reais, points_applied
 ├── payment_method (nullable, seta ao fechar)
 ├── championship_id (FK → championships, nullable)
 └── opened_at, closed_at

comanda_items
 ├── id (uuid, PK)
 ├── comanda_id (FK → comandas, CASCADE)
 ├── product_id (FK → products, RESTRICT)
 ├── product_name (snapshot — preço pode mudar)
 ├── quantity, price_in_reais
 └── added_at

crediarios
 ├── id (uuid, PK)
 ├── user_id (FK → users, RESTRICT)
 ├── comanda_id (FK → comandas, nullable)
 ├── valor_total, valor_pago
 ├── status (Aberto | Pago | Vencido)
 ├── data_vencimento
 ├── observacoes
 └── itens_json (TEXT — LEGADO, usar pagamentos_crediario)

pagamentos_crediario
 ├── id (uuid, PK)
 ├── crediario_id (FK → crediarios, CASCADE)
 ├── valor_pago
 ├── metodo_pagamento
 └── pago_em

championships
 ├── id (uuid, PK)
 ├── title, description, game_type
 ├── status (Planejado | Inscricoes | EmAndamento | Finalizado | Cancelado)
 ├── max_participants, entry_fee_in_cents
 ├── start_date, end_date
 ├── image_url
 └── podio_json (VARCHAR(1000) — JSON fraco, ver issue #3.2.3)

championship_participants
 ├── id (uuid, PK)
 ├── championship_id (FK → championships, CASCADE)
 ├── user_id (FK → users, RESTRICT)
 ├── placement (nullable — seta ao finalizar)
 └── registered_at

championship_pre_inscricoes
 ├── id (uuid, PK)
 ├── championship_id (FK → championships, CASCADE)
 ├── name, email, whatsapp, cpf
 └── created_at

announcements
 ├── id (uuid, PK)
 ├── title, content
 ├── is_active
 └── created_at, updated_at

perfis (roles customizados)
 ├── id (uuid, PK)
 ├── nome, descricao
 └── permissoes (string[] — lista de permissões)

audit_logs
 ├── id (uuid, PK)
 ├── actor_user_id (FK → users)
 ├── entity_type, entity_id
 ├── action, old_value, new_value (JSON)
 └── created_at

lgpd_requests
 ├── id (uuid, PK)
 ├── request_type (Acesso | Exclusao | Retificacao | Portabilidade)
 ├── status (Pendente | EmAnalise | Concluido | Negado)
 ├── requester_name, requester_email, requester_cpf
 ├── protocol_number (único, para consulta pública)
 ├── admin_response, admin_response_at
 └── created_at

product_categories
 ├── id (uuid, PK)
 ├── name (unique)
 └── created_at
```

### 3.2 MongoDB (cardgamestore_cache)

| Collection | Propósito | TTL | Crítico |
|-----------|-----------|-----|---------|
| `card_cache` | Cache de cartas de APIs externas (Pokémon TCG, Scryfall, etc.) | 7 dias | Não |
| `vendas_avulsas` | Event store de vendas de balcão | Permanente | Sim |

`vendas_avulsas` é um event store imutável — nunca deletado, apenas appended. Usado para analytics histórico.

### 3.3 Índices Existentes

```sql
-- users
CREATE UNIQUE INDEX ON users(email) WHERE email IS NOT NULL;
CREATE UNIQUE INDEX ON users(cpf)   WHERE cpf IS NOT NULL;
CREATE INDEX ON users(whatsapp);

-- products
CREATE UNIQUE INDEX ON products(barcode) WHERE barcode IS NOT NULL;
CREATE INDEX ON products(category);
CREATE INDEX ON products(is_active);

-- comandas
CREATE INDEX ON comandas(user_id, status);
CREATE INDEX ON comandas(status);
CREATE INDEX ON comandas(championship_id);

-- comanda_items
CREATE INDEX ON comanda_items(comanda_id);

-- crediarios
CREATE INDEX ON crediarios(user_id, status);
CREATE INDEX ON crediarios(status);

-- championships
CREATE INDEX ON championships(status);
CREATE INDEX ON championships(start_date);

-- audit_logs
CREATE INDEX ON audit_logs(entity_type, entity_id);
CREATE INDEX ON audit_logs(actor_user_id);
CREATE INDEX ON audit_logs(created_at);

-- lgpd_requests
CREATE INDEX ON lgpd_requests(status);
CREATE INDEX ON lgpd_requests(requester_email);
```

**Índices faltando** (ver seção 5.5):
- `crediarios(data_vencimento)` — relatório filtra por vencimento
- `comandas(status, closed_at)` — dashboard filtra ambos

---

## 4. Mapa Completo de Endpoints

### 4.1 Autenticação (`/api/auth`)

| Método | Rota | Auth | Rate Limit | Descrição |
|--------|------|------|-----------|-----------|
| POST | `/api/auth/login` | Anônimo | 5/min | Login admin (email + senha) |
| POST | `/api/auth/quick-login` | Anônimo | 5/min | Login cliente via QR (CPF + WhatsApp) |
| POST | `/api/auth/client-login` | Anônimo | 5/min | Login cliente por email/senha |
| POST | `/api/auth/refresh` | Anônimo | 5/min | Renova access token |
| POST | `/api/auth/logout` | Autenticado | — | Encerra sessão |
| POST | `/api/auth/cpf-lookup` | Anônimo | 5/min | Verifica se CPF existe |
| POST | `/api/auth/setup-account` | Anônimo | 5/min | Setup inicial de conta cliente |
| POST | `/api/auth/forgot-password` | Anônimo | 5/min | Solicita reset de senha por email |
| POST | `/api/auth/reset-password` | Anônimo | 5/min | Redefine senha com token |
| POST | `/api/auth/test-email` | Admin | — | Teste de configuração SMTP |

### 4.2 Produtos (`/api/product`)

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/api/product` | Anônimo | Lista ativos com `showOnMarketplace=true` |
| GET | `/api/product/store` | Autenticado | Lista ativos (sem filtro marketplace) — comanda |
| GET | `/api/product/admin` | Admin/Op | Lista ativos (sem filtro marketplace) — PDV |
| GET | `/api/product/{id}` | Anônimo | Busca por ID |
| GET | `/api/product/barcode/{code}` | Autenticado | Busca por código de barras |
| GET | `/api/product/low-stock` | Admin | Produtos abaixo do estoque mínimo |
| POST | `/api/product` | Admin | Cria produto |
| PUT | `/api/product/{id}` | Admin | Atualiza produto (deve enviar objeto completo) |
| DELETE | `/api/product/{id}` | Admin | Soft delete (seta `isActive=false`) |
| PATCH | `/api/product/{id}/stock` | Admin | Ajusta estoque com delta |

### 4.3 Comandas (`/api/comanda`)

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/api/comanda/admin-open` | Admin/Op | Admin abre comanda para cliente |
| GET | `/api/comanda/dashboard` | Admin/Op | Lista todas as comandas abertas |
| GET | `/api/comanda/history` | Admin/Op | Histórico do dia |
| GET | `/api/comanda/{id}` | Admin/Op | Detalhe de uma comanda |
| GET | `/api/comanda/my` | Autenticado | Comanda ativa do cliente |
| GET | `/api/comanda/my-history` | Autenticado | Histórico do cliente |
| POST | `/api/comanda/{id}/items` | Autenticado | Adiciona item (cliente ou admin) |
| DELETE | `/api/comanda/{id}/items/{itemId}` | Admin/Op | Remove item |
| PATCH | `/api/comanda/{id}/items/{itemId}` | Admin/Op | Atualiza quantidade |
| PUT | `/api/comanda/{id}/close` | Admin/Op | Fecha comanda com método de pagamento |
| PUT | `/api/comanda/{id}/cancel` | Admin/Op | Cancela comanda |
| POST | `/api/comanda/{id}/apply-points` | Autenticado | Aplica pontos de fidelidade |
| DELETE | `/api/comanda/{id}/apply-points` | Autenticado | Remove pontos aplicados |

### 4.4 Usuários (`/api/user`)

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/api/user` | Admin/Op | Lista clientes (com busca full-text) |
| POST | `/api/user` | Admin | Admin cria conta de cliente |
| GET | `/api/user/me` | Autenticado | Perfil do usuário logado |
| PUT | `/api/user/me` | Autenticado | Edita próprios dados (LGPD — retificação) |
| DELETE | `/api/user/me` | Autenticado | Solicita exclusão (LGPD Art. 18) |
| GET | `/api/user/me/preferences` | Autenticado | Preferências de UI |
| PUT | `/api/user/me/preferences` | Autenticado | Salva preferências de UI |
| GET | `/api/user/{id}` | Admin/Op | Detalhe de cliente |
| GET | `/api/user/{id}/historico` | Admin/Op | Histórico completo (comandas, crediários) |
| POST | `/api/user/{id}/points` | Admin | Adiciona pontos |
| POST | `/api/user/{id}/balance` | Admin | Ajusta saldo monetário |
| PUT | `/api/user/{id}/reset-password` | Admin | Redefine senha do cliente |
| PUT | `/api/user/{id}/perfil` | Admin | Atribui/remove perfil de operador |
| DELETE | `/api/user/{id}` | Admin | Anonimiza usuário (LGPD exclusão) |

### 4.5 Crediários (`/api/crediarios`)

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/api/crediarios` | Admin | Cria crediário manual |
| GET | `/api/crediarios` | Admin | Lista com filtro de status |
| GET | `/api/crediarios/usuario/{userId}` | Admin | Crediários de um cliente |
| GET | `/api/crediarios/meu` | Autenticado | Crediário ativo do cliente |
| GET | `/api/crediarios/historico` | Autenticado | Histórico do cliente |
| PUT | `/api/crediarios/{id}/pagar` | Admin | Quita 100% (legado — usar `/pagamento`) |
| PATCH | `/api/crediarios/{id}` | Admin | Edita valor/obs/vencimento |
| POST | `/api/crediarios/{id}/pagamento` | Admin | Registra pagamento parcial |
| DELETE | `/api/crediarios/{id}` | Admin | Deleta (só sem pagamentos) |

**⚠️ Existe também `CreditarioController` duplicado** — ver seção 5.2.

### 4.6 Analytics (`/api/analytics`)

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/api/analytics/dashboard` | Admin | KPIs principais |
| GET | `/api/analytics/clientes` | Admin | Insights por cliente (inatividade, frequência) |
| GET | `/api/analytics/financeiro` | Admin | Receita, custo, margem com breakdown por período |

**Query params de `/api/analytics/financeiro`**:
- `inicio` / `fim` — datas BR no formato `YYYY-MM-DD` (default: mês atual)
- `filterPaymentMethod` (**novo v1.7.3**) — filtra comandas e avulsas por forma de pagamento (ex: `Pix`, `Dinheiro`, `CartaoCredito`)

**`FinanceiroDto` — campos adicionados em v1.7.3**:
- `RecebidoCrediario` — total recebido de crediários no período
- `PagamentosCrediarioPeriodo` — lista de pagamentos individuais com cliente, forma, valor, hora

### 4.7 Venda Avulsa (`/api/venda-avulsa`)

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/api/venda-avulsa` | Admin/Op | Registra venda de balcão |
| GET | `/api/venda-avulsa/recent` | Admin/Op | Últimas vendas (MongoDB) |
| GET | `/api/venda-avulsa/by-date` | Admin/Op | Vendas por data |
| POST | `/api/venda-avulsa/backfill-costs` | Admin | Preenche custos históricos ausentes |

### 4.8 Campeonatos (`/api/championship`)

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/api/championship` | Anônimo | Lista ativos (Planejado/Inscricoes) |
| GET | `/api/championship/admin/all` | Admin | Todos os campeonatos |
| GET | `/api/championship/{id}` | Anônimo | Detalhe com participantes |
| GET | `/api/championship/my-participations` | Autenticado | Campeonatos do usuário |
| GET | `/api/championship/{id}/participants` | Autenticado | Lista participantes |
| POST | `/api/championship` | Admin | Cria campeonato |
| PUT | `/api/championship/{id}` | Admin | Edita campeonato |
| DELETE | `/api/championship/{id}` | Admin | Exclui (só Finalizado/Cancelado) |
| POST | `/api/championship/{id}/register` | Autenticado | Inscreve usuário |
| POST | `/api/championship/{id}/admin-register` | Admin | Admin inscreve usuário |
| DELETE | `/api/championship/{id}/participants/{pid}` | Admin | Remove participante |
| PUT | `/api/championship/{id}/status` | Admin | Muda status |
| PUT | `/api/championship/{id}/participants/{pid}/placement` | Admin | Define colocação |
| PUT | `/api/championship/{id}/image` | Admin | Atualiza imagem capa |
| POST | `/api/championship/{id}/preinscricoes` | Anônimo | Pré-inscrição pública |
| GET | `/api/championship/{id}/preinscricoes` | Admin | Lista pré-inscrições |
| DELETE | `/api/championship/{id}/preinscricoes/{preId}` | Admin | Remove pré-inscrição |
| PATCH | `/api/championship/{id}/podio` | Admin | Salva pódio |

### 4.9 Outros Módulos

| Módulo | Prefixo | Principais Endpoints |
|--------|---------|---------------------|
| **Anúncios** | `/api/announcements` | GET (anônimo), POST/PUT/DELETE (admin) |
| **IA Chat** | `/api/ai` | POST `/chat` (admin) — Gemini com contexto da loja |
| **Audit** | `/api/audit` | GET (admin, paginado) |
| **LGPD** | `/api/lgpd` | Solicitações, consentimento, respostas, relatórios |
| **Perfis** | `/api/perfis` | CRUD de roles customizados |
| **Relatórios** | `/api/relatorios` | Vendas por categoria + crediário (ver abaixo) |
| **TCG** | `/api/tcg` | Busca de cartas com cache MongoDB |
| **Upload** | `/api/upload` | Imagem de produto, foto de perfil |
| **Categoria** | `/api/category` | CRUD de categorias de produto |

**Endpoints de Relatórios (`/api/relatorios`):**

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/relatorios/vendas?mes=&ano=` | Vendas por categoria e produto no mês (PostgreSQL + MongoDB) |
| GET | `/api/relatorios/crediario?mes=&ano=` | **[v1.7.3]** Situação atual de devedores + pagamentos recebidos no mês |

**`RelatorioCrediarioDto`** — campos:
- `Devedores[]` — todos crediários `Aberto` com saldo, vencimento, dias de atraso
- `PagamentosNoMes[]` — pagamentos de crediário registrados no mês filtrado
- KPIs: `TotalEmAbertoEmReais`, `TotalVencidoEmReais`, `RecebidoNoMesEmReais`, `QtdAbertos`, `QtdVencidos`, `QtdPagamentosNoMes`

---

## 5. Auditoria de Backend

### 5.1 Bugs Críticos

#### 5.1.1 Estoque Pode Ficar Negativo

**Arquivo**: `ComandaService.cs` e `VendaAvulsaService.cs`

A validação de estoque e o decremento não são atômicos. Entre a leitura do estoque e o `UPDATE`, outro request concorrente pode ter decrementado o mesmo produto.

```csharp
// Padrão ATUAL (problemático):
var produto = await _db.Products.FindAsync(id);
if (produto.StockQuantity < quantidade) return false;  // Check
// ← aqui outro thread pode entrar e decrementar ←
produto.StockQuantity -= quantidade;  // Decrement
await _db.SaveChangesAsync();

// Padrão CORRETO (atômico via SQL):
var rows = await _db.Database.ExecuteSqlRawAsync(
    "UPDATE products SET stock_quantity = stock_quantity - {0} " +
    "WHERE id = {1} AND stock_quantity >= {2}",
    quantidade, id, quantidade);
return rows > 0;  // 0 rows = estoque insuficiente
```

#### 5.1.2 Race Condition em Cadastro de Cliente

**Arquivo**: `AuthService.cs` — `QuickLoginAsync()`

Dois clientes com o mesmo CPF chegando simultaneamente podem gerar `DbUpdateException` (constraint violation no CPF único). O retry após exceção é correto, mas há uma janela entre o segundo `SaveChangesAsync` falhar e o re-fetch encontrar o registro já criado.

**Fix**: Usar `UPSERT` via SQL ou transaction serializable.

#### 5.1.3 Partial Update Corrompe Produto

**Arquivo**: `ProductController.cs:106-110`

O endpoint `PUT /api/product/{id}` recebe `[FromBody] Product product` — objeto completo. Qualquer campo não enviado pelo frontend é desserializado como `null` ou `0` pelo C#, corrompendo o registro.

Esta foi a causa raiz do bug do marketplace que foi resolvido adicionando o spread `{ ...p, showOnMarketplace: next }` no frontend. O padrão já está correto no frontend, mas o backend deveria aceitar `PATCH` com campos parciais para proteger contra futuras regressões.

**Risco atual**: Se alguma página do admin enviar um PUT parcial, todos os outros campos do produto serão zerados silenciosamente.

#### 5.1.4 `show_on_site` Legado Sem Remoção

**Arquivo**: `Product.cs`, todas as migrations

O campo `show_on_site` existe no banco e no modelo C#, mas não filtra mais nada (foi substituído por `show_on_marketplace`). O campo aparece na UI de edição de produto, confundindo o admin.

**Fix**: Remover do modal de edição no frontend; deprecar no model com `[Obsolete]`; planejar remoção de coluna na próxima migration de manutenção.

---

### 5.2 Código Morto / Duplicado

#### 5.2.1 `CreditarioController` Duplicado

Existem dois controllers para crediários:
- `CrediariosController.cs` — completo (pagamentos parciais, PATCH, etc.)
- `CreditarioController.cs` — resumido (~150 linhas, apenas GET e `PUT /pagar`)

**Impacto**: Rota `/api/creditario/{id}/pagar` ainda pode estar sendo chamada por código legado. Frontend deve usar exclusivamente `/api/crediarios`.

**Fix**: Verificar uso de `CreditarioController` e removê-lo.

#### 5.2.2 `itens_json` em Crediários

A coluna `crediarios.itens_json` (TEXT) duplica o que a tabela normalizada `pagamentos_crediario` já armazena. Dois sources of truth para o mesmo dado.

**Fix**: Migration para remover `itens_json` após confirmar que nenhuma query a usa.

#### 5.2.3 Endpoint de Backfill em API Pública

`POST /api/venda-avulsa/backfill-costs` é uma operação de manutenção que faz table scan completo do MongoDB. Não deveria ser endpoint HTTP — deveria ser um job de background (Hangfire) ou script avulso.

#### 5.2.4 `VendaAvulsaService.BackfillCostsAsync()` sem Idempotência

Chamadas repetidas ao backfill podem reprocessar vendas já corrigidas.

---

### 5.3 Segurança

#### 🔴 CRÍTICO — Variável `COOKIE_SECURE=false` Permite HTTP em Produção

**Arquivo**: `AuthController.cs`

```csharp
var secureCookies = !_env.IsDevelopment()
    && !string.Equals(Environment.GetEnvironmentVariable("COOKIE_SECURE"), "false", ...);
```

Se `COOKIE_SECURE=false` estiver no `.env` de produção, os cookies JWT são transmitidos sem a flag `Secure`, permitindo envio por HTTP. Atacante em MITM captura os tokens de autenticação.

**Fix**: Remover essa brecha. Em produção, cookies SEMPRE com flag `Secure`.

#### 🔴 CRÍTICO — Refresh Token em Plain Text no Banco

O refresh token é armazenado como string aleatória não-hashed. Se o banco for comprometido, todos os tokens são reutilizáveis imediatamente.

**Fix**: Hash com BCrypt ou SHA-256 antes de persistir; comparar o hash na validação.

#### 🟡 ALTO — JWT Secret em `.env` Plain Text

Credenciais sensíveis (JWT secret, SMTP key, Gemini API key) estão em arquivo `.env`. Se commitado acidentalmente no Git, ficam expostos permanentemente no histórico.

**Fix**: Usar `.env.example` como template (commitado), `.env` no `.gitignore`.

#### 🟡 ALTO — CORS não Configurado Explicitamente

Se não houver configuração explícita de CORS, requisições cross-origin do frontend para a API falham silenciosamente em produção.

**Fix confirmar**:
```csharp
builder.Services.AddCors(options =>
    options.AddPolicy("FrontendPolicy", b =>
        b.WithOrigins("https://santuarionerd.tech")
         .AllowAnyMethod()
         .AllowAnyHeader()
         .AllowCredentials()));
```

#### 🟡 ALTO — CPF Sem Validação de Dígito Verificador

**Arquivo**: `AuthService.cs`, `LgpdController.cs`

CPF é aceito como string sem validar formato (11 dígitos) e dígitos verificadores. É possível cadastrar `"00000000000"` ou `"abc"`.

**Fix**: Implementar algoritmo de validação de CPF brasileiro.

#### 🟡 MÉDIO — Pagamento de Crediário Sem Idempotência

`POST /api/crediarios/{id}/pagamento` sem `Idempotency-Key` header. Request retentada (timeout de rede, duplo clique) debita o valor duas vezes.

**Fix**: Adicionar `idempotency_key` (UUID) por transação financeira, armazenado com timestamp de 24h.

#### 🟡 MÉDIO — Rate Limiting Ausente em Endpoints Sensíveis

- `POST /api/user/{id}/points` — admin pode adicionar pontos infinitos sem throttle
- `POST /api/championship/{id}/register` — spam de inscrições

#### 🟡 MÉDIO — IP Hash sem HMAC

**Arquivo**: `LgpdController.cs`

```csharp
var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(_ipSalt + ip));
```

Concatenação simples é vulnerável a length extension attacks. Usar `HMACSHA256` com salt como chave.

---

### 5.4 Performance

#### 5.4.1 N+1 Query em Analytics

**Arquivo**: `AnalyticsController.cs` — `GetClienteInsights()`

```csharp
// Carrega TODOS os usuários em memória
var usuarios = await _db.Users.Where(...).ToListAsync();

// Carrega TODOS os históricos em memória
var estatisticas = await _db.Comandas.GroupBy(...).ToListAsync();

// Join em LINQ to Objects — O(N×M) em memória
```

Com 5.000 clientes e 50.000 comandas: ~55.000 linhas em memória antes de qualquer agrupamento.

**Fix**: Executar GroupBy direto no banco:
```csharp
var stats = await _db.Comandas
    .GroupBy(c => c.UserId)
    .Select(g => new { UserId = g.Key, Count = g.Count(), LastVisit = g.Max(c => c.ClosedAt) })
    .ToListAsync();
```

#### 5.4.2 VendaAvulsa Carrega 5.000 Docs do MongoDB para Filtrar em Memória

**Arquivo**: `AnalyticsController.cs` — `GetDashboard()`

```csharp
var vendas60Dias = (await _vendas.GetRecentAsync(5000, ha60Dias)).ToList();
var vendasHoje  = vendas60Dias.Where(v => v.SoldAt >= hojeInicio).ToList();
var vendasOntem = vendas60Dias.Where(v => v.SoldAt >= ontemInicio && ...).ToList();
// ... mais 3 filtros
```

**Fix**: Fazer queries separadas com filtro no MongoDB, ou usar pipeline de aggregation.

#### 5.4.3 Histórico de Usuário Sem Paginação

`GET /api/user/{id}/historico` retorna todas as comandas e crediários do cliente sem limite. Cliente ativo há 2 anos pode ter 500+ comandas.

**Fix**: Paginação obrigatória (`?page=1&pageSize=50`).

#### 5.4.4 SignalR Sem Batching

Cada item adicionado/removido de comanda dispara imediatamente um broadcast WebSocket. 10 itens adicionados rapidamente = 10 mensagens + 10 re-renders no browser.

**Fix**: Debounce de 200ms antes de broadcast.

#### 5.4.5 Índices Faltando

```sql
-- Relatório filtra por vencimento — table scan sem esse índice:
CREATE INDEX idx_crediarios_vencimento ON crediarios(data_vencimento);

-- Dashboard filtra status E data:
CREATE INDEX idx_comandas_status_closed ON comandas(status, closed_at);
```

---

### 5.5 Dívida Técnica

#### 5.5.1 Magic Strings para Método de Pagamento

`"Pix"`, `"Crediario"`, `"CartaoCredito"`, `"Dinheiro"` aparecem em 10+ lugares como strings literais. Um typo causa falha silenciosa em relatórios.

**Fix**: Enum `PaymentMethod` compartilhado entre backend e frontend (via geração de código ou constantes).

#### 5.5.2 `UserController` com Responsabilidades Excessivas

O controller gerencia: usuários, preferências, perfis, histórico, pontos, saldo — 6 responsabilidades distintas em ~400 linhas.

**Fix**: Separar em `UserController`, `PreferencesController`, `PerfilController`.

#### 5.5.3 Duplicação de Lógica de Timezone

`DiaBrasil()`, `BrDateToUtcStart()` e variantes similares existem em `ComandaService`, `AnalyticsController`, `RelatoriosController` e `VendaAvulsaService` como funções locais.

**Progresso v1.7.2**: `RelatoriosController` passou a usar `BrazilZone` identicamente ao `AnalyticsController` — consistência melhorada, mas o helper ainda não é centralizado.

**Fix pendente**: Criar `BrazilTimeZone` static class compartilhada para eliminar as 3 cópias.

#### 5.5.4 `AppDbContextModelSnapshot.cs` Desatualizado

Migrations criadas com `migrationBuilder.Sql()` (raw SQL) não atualizam automaticamente o snapshot EF Core. O snapshot não reflete `show_on_marketplace`, `profile_image_url`, `image_urls[]`, etc.

**Impacto**: `dotnet ef migrations add` pode gerar migrations incorretas; `dotnet ef database update` em banco novo não cria todas as colunas.

---

## 6. Auditoria de Frontend

### 6.1 Mapa de Chamadas de API por Página

| Página | Endpoint Chamado | Correto? | Observação |
|--------|-----------------|----------|-----------|
| `app/page.tsx` (landing) | `productApi.list()` | ✅ | Filtra marketplace public |
| `app/produtos/page.tsx` | `productApi.list()` | ✅ | Filtra marketplace public |
| `app/cliente/page.tsx` | `productApi.listStore()` | ✅ | Todos ativos (para comanda) |
| `app/admin/estoque/page.tsx` | `productApi.listAdmin()` | ✅ | |
| `app/admin/venda-avulsa/page.tsx` | `productApi.listAdmin()` | ✅ | |
| `app/admin/dashboard/page.tsx` | `productApi.listAdmin()` | ✅ | |
| `app/admin/relatorios/page.tsx` | `productApi.listAdmin()` | ✅ | |
| `app/page.tsx` (campeonatos) | `championshipApi.list()` + filtro client-side | ⚠️ | Filtro `status === 'Planejado' || 'Inscricoes'` deveria ser query param |

### 6.2 Bugs e Inconsistências

#### 6.2.1 Inconsistência de Unidades: Pontos vs Centavos

**Arquivo**: `app/admin/dashboard/page.tsx:456-470` (CloseComandaModal)

```typescript
const totalRestante  = comanda.totalInReais - comanda.pointsApplied / 100
const saldoPontos    = comanda.userPointsBalance  // Unidades? Centavos?
...
saldoPontos < Math.round(totalRestante * 100)  // Comparando unidades com centavos!
```

Se `pointsApplied` está em centavos e `userPointsBalance` em unidades, a validação aceita ou rejeita pagamentos incorretamente.

**Fix**: Documentar e padronizar na API: pontos são sempre `integer` (1 ponto = R$0,01).

#### 6.2.2 `JSON.parse` sem Try-Catch

**Arquivo**: `app/mesa/[mesa]/page.tsx:76`

```typescript
const raw = localStorage.getItem(STORAGE_KEY)
const user: SavedUser = JSON.parse(raw)  // Crash se localStorage corrompido
```

**Fix**: Envolver em try-catch (o padrão correto já existe em `PreferencesContext.tsx`).

#### 6.2.2-A ✅ CORRIGIDO (v1.7.2) — `toDateInput` Usava UTC

**Arquivo**: `app/admin/financeiro/page.tsx`

A função `toDateInput` usava `toISOString()` que retorna data em UTC. No Brasil (UTC-3), após as 21h locais o código enviava "amanhã" para o backend — filtro "Hoje" retornava zero.

**Fix aplicado**: A função agora usa `getFullYear()`, `getMonth()`, `getDate()` do objeto Date local:
```typescript
function toDateInput(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`
}
```

**Nota**: Mesmo bug existia nas funções de geração de PDF — também corrigido em v1.7.2.

---

#### 6.2.3 Filtragem Redundante Client-Side

**Arquivo**: `app/produtos/page.tsx:86`

```typescript
.then(r => setProducts(r.data.filter(p => p.isActive && p.stockQuantity > 0 && p.showOnMarketplace !== false)))
```

`productApi.list()` já retorna apenas `isActive && showOnMarketplace`. A filtragem client-side de `isActive` e `showOnMarketplace` é redundante. A de `stockQuantity > 0` é válida (esconde esgotados da UI mas mantém em cache).

### 6.3 Performance Frontend

#### 6.2.4 [v1.7.4] Filtros do Histórico — Client-Side com Risco de Volume

**Arquivo**: `app/admin/dashboard/page.tsx:1220-1256`

Os novos filtros (nome de cliente, horário `de HH:mm` até `HH:mm`) são aplicados sobre a lista já carregada do servidor:

```typescript
const filteredHistory = history.filter(c => {
  if (histSearch && !c.userName.toLowerCase().includes(...)) return false
  if ((histHoraDe || histHoraAte) && c.closedAt) {
    const t = new Date(c.closedAt).toTimeString().slice(0,5)
    if (histHoraDe && t < histHoraDe) return false
    if (histHoraAte && t > histHoraAte) return false
  }
  return true
})
```

**Observação positiva**: Para um dia (10–50 comandas) é aceitável e rápido.

**Risco latente**: Se o histórico for carregado sem filtro de data (i.e., toda a base histórica), a filtragem client-side fica problemática. Confirmar que `comandaApi.history(data)` sempre filtra pelo dia atual.

**Boa prática futura**: Quando o volume de comandas/dia crescer, mover nome e horário para query params do backend.

---

#### 6.3.1 Re-renders Desnecessários no Dashboard

**Arquivo**: `app/admin/dashboard/page.tsx:1194-1243`

Cálculos pesados sem `useMemo`:

```typescript
// Executado a cada render, sem memoização:
const patrimonioCusto = allProducts.reduce((s, p) => s + p.costPriceInCents * p.stockQuantity, 0) / 100
const patrimonioVenda = allProducts.reduce((s, p) => s + p.priceInCents * p.stockQuantity, 0) / 100
const lucroEstoque    = patrimonioVenda - patrimonioCusto
const totalPecas      = allProducts.reduce((s, p) => s + p.stockQuantity, 0)
const totalAberto     = comandas.reduce((s, c) => s + c.totalInReais, 0)
```

**Fix**: `useMemo(() => calculcate(), [allProducts, comandas])`

#### 6.3.2 SignalR + Polling Paralelos

**Arquivo**: `app/admin/dashboard/page.tsx:1082-1142`

SignalR já envia updates em tempo real. Polling paralelo baseado em `dp.refreshInterval` causa requisições duplicadas quando SignalR está ativo e conectado.

**Fix**: Usar polling apenas como fallback quando SignalR estiver em estado `Disconnected`.

#### 6.3.3 jsPDF não Lazy-Loaded

**Arquivo**: `package.json` / múltiplas páginas

`jsPDF` (105 KB minificado) é incluído no bundle principal. Apenas a página de venda avulsa usa PDFs.

**Fix**:
```typescript
// Substituir import estático por:
const { jsPDF } = await import('jspdf')
```

#### 6.3.4 Imagens sem Next.js Image Component

**Arquivo**: `app/page.tsx`, `app/produtos/page.tsx`, `app/admin/dashboard/page.tsx`

Tags `<img>` nativas sem `loading="lazy"`, sem `width`/`height` (CLS), sem compressão automática.

**Fix**: Usar `<Image>` do Next.js onde possível.

### 6.4 Código Morto

| Item | Arquivo | Problema |
|------|---------|---------|
| `useThrottle` | `lib/hooks.ts` | Exportado, nunca importado |
| `Medal` icon | `app/admin/dashboard/page.tsx:14` | Importado — agora **renderizado** na tab Análises (verificar antes de remover) |
| `navHover` state | `app/page.tsx` | Estado atualizado mas lógica usa `navVisible` |
| `ChevronLeft` | `app/admin/venda-avulsa/page.tsx:11` | Importado, nunca usado |

### 6.5 Segurança Frontend

#### 6.5.1 ✅ Tokens em Cookies HttpOnly

JWT armazenado em cookies HttpOnly com `withCredentials: true`. Não acessível por JavaScript. Correto.

#### 6.5.2 ✅ Interceptor de 401 com Mutex

```typescript
// lib/api.ts — Refresh com mutex para evitar race condition de múltiplos refreshes simultâneos
```

Evita múltiplos refreshes paralelos em caso de tokens expirados. Correto.

#### 6.5.3 ⚠️ Preferências Sensíveis em localStorage

`contexts/PreferencesContext.tsx` salva preferências de UI em localStorage. Aceitável para preferências de tema. Garantir que dados financeiros (pontos, saldo) não sejam persistidos localmente.

---

## 7. Auditoria de Infraestrutura e Banco

### 7.1 Problemas Críticos de Infraestrutura

#### 🔴 CRÍTICO — Sem Backup Automatizado

```yaml
volumes:
  postgres_data:  # Sem pg_dump automático
  mongo_data:     # Sem mongodump automático
  api_uploads:    # Sem rsync para storage externo
```

Falha de disco = perda total e irreversível de dados.

**Fix imediato**:
```bash
# Crontab no host — backup diário às 3h
0 3 * * * docker exec santuarionerd_postgres pg_dump -U cardgame_user cardgamestore | gzip > /backups/pg_$(date +%Y%m%d).sql.gz
```

#### 🔴 CRÍTICO — Secrets em Plain Text

Todos os secrets (JWT, SMTP, Gemini API, MongoDB) estão em `.env` plain text no servidor e potencialmente rastreáveis no Git.

**Fix**:
1. Garantir `.env` no `.gitignore` (verificar histórico do Git para possíveis leaks)
2. Usar Hostinger Secrets Manager ou `docker secret` com Docker Swarm

#### 🔴 CRÍTICO — Migrations Desincronizadas

Migrations #4, #7, #8, #9 usam `migrationBuilder.Sql()` com `ALTER TABLE ... IF NOT EXISTS`. O `AppDbContextModelSnapshot.cs` não reflete essas colunas.

**Impacto**: Em um novo ambiente, `dotnet ef database update` roda as migrations mas o snapshot está errado; `dotnet ef migrations add` pode gerar migration inválida duplicando colunas.

**Fix**: Converter migrations raw para Fluent API e sincronizar o snapshot.

#### 🟡 ALTO — Single Points of Failure em Tudo

- NGINX: 1 instância → todo tráfego para
- API: 1 instância → restart = downtime
- PostgreSQL: 1 instância sem replica → falha = perda de dados
- Host KVM: hardware único → sem failover

**SLA estimado atual**: ~95% (múltiplos SPOFs em séries)

#### 🟡 ALTO — Sem Resource Limits em Containers

Sem `deploy.resources.limits` no compose, um container em loop pode consumir 100% da CPU/RAM do host, derrubando todos os outros.

#### 🟡 ALTO — `show_on_marketplace` como INTEGER

```sql
-- Migration #9 criou como INTEGER:
ALTER TABLE products ADD COLUMN IF NOT EXISTS show_on_marketplace INTEGER NOT NULL DEFAULT 1;
```

Npgsql (EF Core PostgreSQL) mapeia `bool` C# para tipo `boolean` do PostgreSQL. Uma coluna `INTEGER` retorna erro de conversão de tipo ao ser lida via EF Core.

**Fix já aplicado manualmente no banco de produção**:
```sql
ALTER TABLE products DROP COLUMN show_on_marketplace;
ALTER TABLE products ADD COLUMN show_on_marketplace boolean NOT NULL DEFAULT true;
```

**Fix na migration**: Corrigir o SQL na migration #9 para `boolean`.

### 7.2 Observabilidade

| Componente | Status |
|-----------|--------|
| Health checks (`/health`) | ✅ Implementado |
| Logs estruturados | ⚠️ Console apenas, sem agregação |
| Métricas (CPU, latência, req/s) | ❌ Não implementado |
| Alertas (downtime, erros críticos) | ❌ Não implementado |
| Distributed tracing | ❌ Não implementado |
| Log rotation | ✅ `max-size: 10m, max-file: 3` |

### 7.3 Segurança de Infraestrutura

| Item | Status |
|------|--------|
| TLS externo (Cloudflare) | ✅ |
| Headers de segurança HTTP | ✅ (`X-Frame-Options`, `X-Content-Type-Options`, etc.) |
| HTTPS interno (Nginx → API) | ❌ HTTP plain |
| TLS na conexão PostgreSQL | ❌ `sslmode` não definido |
| Portas internas expostas ao host | ⚠️ Verificar iptables |
| Refresh token rotation | ❌ Mesmo token por 30 dias |

---

## 8. Pontos Fortes do Sistema

### 8.1 Segurança de Autenticação Robusta

- Cookies HttpOnly para JWT (proteção XSS)
- Rate limiting configurado nos endpoints de auth (5 req/min)
- Refresh token com expiração de 30 dias
- Mutex no refresh para evitar race conditions no frontend

### 8.2 LGPD Compliance Real

- Art. 18 (direitos do titular) implementado: acesso, retificação, portabilidade, exclusão
- Anonimização em vez de hard delete (preserva integridade referencial)
- Audit log imutável de todas as operações
- Consentimento de cookies rastreado com hash de IP

### 8.3 Arquitetura de Dados Bem Pensada

- PostgreSQL para dados transacionais (ACID)
- MongoDB para event store imutável (VendaAvulsa) e cache de cartas TCG
- Separação de concerns clara entre os dois bancos
- Health check com degraded state (MongoDB opcional)

### 8.4 Validação de Upload Segura

- Magic bytes check (previne rename attack: `.exe` → `.jpg`)
- Filename gerado por GUID (previne path traversal)
- Size limit de 5 MB
- Extensão whitelist

### 8.5 SignalR com Reconexão e Fallback

- Transporte com fallback automático (WebSocket → SSE → Long Polling)
- Reconexão automática com backoff
- Grupos por comanda (não broadcast global)
- JWT via query string para compatibilidade com WebSocket

### 8.6 Design System Consistente no Frontend

- Tailwind com paleta de cores customizada bem definida
- Temas light/dark funcionando
- CSS Variables para theming dinâmico
- Componentes de UI consistentes entre páginas

### 8.7 Elicitação de Requisitos — O que o Sistema Faz Bem

Com base na análise completa, o sistema atende corretamente os seguintes fluxos de negócio:

1. **Fluxo de comanda**: Cliente chega, abre comanda na mesa, adiciona produtos, aplica pontos, admin fecha com pagamento. Real-time via SignalR.
2. **Fluxo de PDV**: Balcão vende sem cadastro de cliente, registra no MongoDB como event store.
3. **Fluxo de crediário**: Admin cria fiado, cliente paga parcialmente ao longo do tempo, sistema rastreia saldo.
4. **Fluxo de campeonato**: Criação, pré-inscrição anônima, inscrição autenticada, definição de pódio.
5. **Fluxo de marketplace**: Produto marcado com `showOnMarketplace=true` aparece na landing page pública.

---

## 9. Plano de Correções Priorizadas

> **Legenda**: ✅ Resolvido | ⏳ Em progresso | ⬜ Pendente

### Prioridade 1 — Crítico (Implementar em até 1 semana)

| # | Status | Problema | Arquivo | Esforço |
|---|--------|----------|---------|---------|
| 1.1 | ✅ | Backup automático PostgreSQL + MongoDB | `deploy/backup.sh` criado — configurar cron no VPS | — |
| 1.2 | ✅ | Remover brecha `COOKIE_SECURE=false` | `AuthController.cs` | — |
| 1.3 | ✅ | `.env` no `.gitignore` | Já estava correto | — |
| 1.4 | ⬜ | Adicionar índices faltando | Nova migration SQL | 1h |
| 1.5 | ⬜ | Corrigir tipo `show_on_marketplace` na migration | Migration raw SQL | 30min |
| 1.6 | ✅ | Refresh token SHA-256 (não mais plain text) | `AuthService.cs` — ⚠️ invalida sessões ao deploy | — |
| 1.7 | ✅ | Partial update corrompe produto | `ProductService.UpdateAsync` | — |

### Prioridade 2 — Alto (Implementar em até 1 mês)

| # | Status | Problema | Arquivo | Esforço |
|---|--------|----------|---------|---------|
| 2.1 | ⬜ | Remover `CreditarioController` duplicado | `CreditarioController.cs` | 1h |
| 2.2 | ⬜ | Implementar estoque atômico (UPDATE com WHERE) | `ComandaService.cs`, `VendaAvulsaService.cs` | 3h |
| 2.3 | ⬜ | Hash de refresh token antes de persistir | `AuthService.cs` | 2h |
| 2.4 | ⬜ | Validação de CPF com dígito verificador | `AuthService.cs`, `LgpdController.cs` | 2h |
| 2.5 | ⬜ | Paginação em `GET /api/user/{id}/historico` | `UserController.cs` | 2h |
| 2.6 | ⬜ | `useMemo` nos cálculos do dashboard | `dashboard/page.tsx` | 1h |
| 2.7 | ✅ | Lazy-load jsPDF | `relatorio-admin.ts` usa `await import('jspdf')` | — |
| 2.8 | ⬜ | Remover `itens_json` legado | Nova migration | 2h |
| 2.9 | ⬜ | Resource limits no docker-compose.prod.yml | `deploy/docker-compose.prod.yml` | 1h |
| 2.10 | ⬜ | Try-catch no `JSON.parse` de mesa | `app/mesa/[mesa]/page.tsx` | 15min |

### Prioridade 3 — Médio (Implementar em até 3 meses)

| # | Status | Problema | Esforço |
|---|--------|----------|---------|
| 3.1 | ⬜ | Enum `PaymentMethod` (eliminar magic strings) | 4h |
| 3.2 | ⬜ | Fix N+1 queries no Analytics (`GetClienteInsights`) | 4h |
| 3.3 | ⬜ | Correção do SignalR vs Polling paralelo | 2h |
| 3.4 | ⬜ | Refatorar `UserController` em sub-controllers | 8h |
| 3.5 | ⬜ | `BrazilTimeZone` helper compartilhado (3 cópias) | 2h |
| 3.6 | ⬜ | Sincronizar `AppDbContextModelSnapshot` | 4h |
| 3.7 | ⬜ | Idempotência em pagamentos de crediário | 4h |
| 3.8 | ⬜ | `<Image>` do Next.js em vez de `<img>` | 3h |
| 3.9 | ⬜ | Remover código morto (useThrottle, navHover, ChevronLeft) | 1h |
| 3.10 | ⬜ | Logging centralizado (Serilog) | 6h |
| 3.11 | ⬜ | Filtros nome/horário histórico: mover para server quando volume crescer | `ComandaController.cs` + frontend | 3h |

### Bugs Corrigidos (histórico)

| # | Versão | Problema Resolvido |
|---|--------|--------------------|
| ✅ | v1.7.2 | `toDateInput` usava UTC → "Hoje" zerava após 21h no Brasil |
| ✅ | v1.7.2 | Gráfico de receita sem labels (backend retornava `dd/MM`, frontend esperava `yyyy-MM-dd`) |
| ✅ | v1.7.2 | PDFs com data incorreta (usavam `toISOString()` em vez de data local) |
| ✅ | v1.7.2 | `RelatoriosController` ignorava fuso horário de Brasília |
| ✅ | *atual* | `COOKIE_SECURE=false` bypass removido — cookies sempre `Secure` em produção |
| ✅ | *atual* | Refresh token agora armazenado como SHA-256 hex (não mais plain text) ⚠️ invalida sessões existentes |
| ✅ | *atual* | QuickLogin: `ChangeTracker.Clear()` antes de re-fetch após `DbUpdateException` |
| ✅ | *atual* | `ProductService.UpdateAsync` carrega entity antes de salvar — partial update não corrompe mais |
| ✅ | *atual* | `ProductService.AdjustStockAsync` usa `ExecuteUpdateAsync` atômico |
| ✅ | *atual* | `deploy/backup.sh` criado — PostgreSQL + MongoDB, retenção 7 dias |
| ✅ | anterior | Race condition de estoque (`ExecuteUpdateAsync` já estava em `ComandaService` e `VendaAvulsaService`) |
| ✅ | anterior | `.env` no `.gitignore` — secrets não vão ao Git |

---

## 10. Roadmap de Escalabilidade

### 10.1 Estado Atual vs 10× de Carga

| Métrica | Hoje (estimado) | 10× | Gargalo |
|---------|----------------|-----|---------|
| Req/dia | ~500 | ~5.000 | Dashboard query pesada |
| Comandas/dia | ~20 | ~200 | PostgreSQL single instance |
| Produtos no estoque | ~200 | ~2.000 | Sem impacto |
| Usuários cadastrados | ~100 | ~1.000 | Analytics N+1 |
| Conexões WebSocket | ~5 | ~50 | SignalR sem sticky session |

### 10.2 Cache Layer (Prioridade Alta)

**Redis** resolve 3 problemas imediatos:

1. **Cache de dashboard**: Recalcular KPIs a cada request é caro. Cache de 5 minutos reduz 95% das queries analíticas.
2. **Session store**: Em vez de RefreshToken no banco, usar Redis com TTL automático.
3. **Token blacklist**: Logout imediato sem esperar expiração do JWT.

```csharp
// Exemplo: Cache do dashboard com Redis
var key = "dashboard:latest";
var cached = await _redis.GetAsync<DashboardDto>(key);
if (cached != null) return Ok(cached);

var dashboard = await CalcularDashboard();
await _redis.SetAsync(key, dashboard, TimeSpan.FromMinutes(5));
return Ok(dashboard);
```

### 10.3 Background Jobs (Prioridade Alta)

Operações que hoje bloqueiam requests devem ser assíncronas:

| Operação | Hoje | Com Hangfire |
|----------|------|-------------|
| Envio de email (LGPD, senha) | Síncrono — bloqueia request | Fila → retentativa automática |
| `BackfillCosts` | Endpoint HTTP público | Job agendado |
| Purge de cache TCG | Endpoint HTTP público | Job recorrente |
| Recálculo de dashboard | A cada request | Job recorrente a cada 5min |

### 10.4 Database (Para 100× de Carga)

```
Hoje:          PostgreSQL single instance
10× load:      Read replica para queries analíticas
               PgBouncer para connection pooling
100× load:     PostgreSQL cluster (Patroni)
               Particionamento de comanda_items por ano
               Elasticsearch para relatórios
```

### 10.5 Kubernetes (Para 10× de Carga)

```yaml
# Exemplo mínimo de HPA para API:
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
spec:
  scaleTargetRef:
    kind: Deployment
    name: santuarionerd-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

**SignalR com múltiplas instâncias** requer Redis backplane:
```csharp
builder.Services.AddSignalR().AddStackExchangeRedis(redisConStr);
```

### 10.6 CDN para Assets

Uploads de imagem hoje são servidos do `wwwroot` do servidor. Com Cloudflare R2 ou AWS S3 + CloudFront:

- Imagens entregues de edge servers (~50ms vs ~300ms)
- Servidor não gasta banda em assets estáticos
- Custo: ~$0.015/GB vs ~$0.09/GB em egress de VPS

### 10.7 Custo Estimado por Estágio

| Estágio | Infra | Suporte | Total/mês |
|---------|-------|---------|-----------|
| Hoje (MVP) | $30 | $0 | $30 |
| 10× (cache + jobs) | $80 | $100 | $180 |
| 50× (replicas + K8s) | $300 | $500 | $800 |
| 100× (full prod-grade) | $800+ | $1.500 | $2.300+ |

---

## Apêndice A: Checklist de Operação

- [ ] Backup PostgreSQL testado e restaurado com sucesso
- [ ] `.env` no `.gitignore` e sem histórico de commit com secrets
- [ ] Resource limits definidos em `docker-compose.prod.yml`
- [ ] Índices `crediarios(data_vencimento)` e `comandas(status, closed_at)` criados
- [ ] `show_on_marketplace` como `boolean` no banco
- [ ] Migration #9 corrigida no código
- [ ] `CreditarioController.cs` removido ou marcado deprecated
- [ ] `itens_json` removido de crediários
- [ ] CPF com validação de dígito verificador
- [ ] Rate limiting em `POST /api/user/{id}/points`
- [ ] `useMemo` nos cálculos do dashboard
- [x] jsPDF lazy-loaded ✅ (resolvido em `relatorio-admin.ts`)

---

## Apêndice B: Changelog da Análise

| Versão doc | Data | Mudanças |
|-----------|------|---------|
| **2.1** | 2026-06-23 | Atualizada para sistema v1.7.4. Novo endpoint `/api/relatorios/crediario`, filtro `filterPaymentMethod` no financeiro, filtros client-side no histórico do dashboard, bugs de timezone corrigidos em v1.7.2. |
| **2.0** | 2026-06-22 | Análise completa inicial do sistema v1.7.1. |

---

## Apêndice C: O que Mudou em v1.7.2 – v1.7.4

### v1.7.4 (2026-06-23)
**Dashboard — Filtros no Histórico de Comandas**
- Tab Histórico agora exibe barra de filtros: input de nome (`histSearch`) + dois inputs de horário `de HH:mm` / `até HH:mm`
- Filtragem 100% client-side sobre `history[]` do dia (`filteredHistory` na linha 1220 de `dashboard/page.tsx`)
- Breakdown por forma de pagamento e totais calculados sobre `filteredHistory` (dinâmico ao digitar)
- Botão "Limpar filtros" visível quando qualquer filtro está ativo

### v1.7.3 (2026-06-22)
**Financeiro — Crediários Recebidos no Período**
- `GET /api/analytics/financeiro` passa a retornar `recebidoCrediario` (R$) e `pagamentosCrediarioPeriodo[]`
- Card "Crediários abertos" no Financeiro mostra sub-texto com total recebido; clique abre modal com lista
- Novo query param `filterPaymentMethod` no financeiro — filtra comandas E vendas avulsas pela forma de pagamento (verifica `PaymentMethod` e `SecondPaymentMethod`)

**Relatório PDF de Crediário**
- Novo endpoint `GET /api/relatorios/crediario?mes=&ano=`
- `RelatorioCrediarioDto` com `Devedores[]` (saldo, vencimento, dias de atraso) e `PagamentosNoMes[]`
- Frontend gera PDF via `gerarRelatorioCrediario()` em `relatorio-admin.ts`
- PDF tem 2 seções: devedores em aberto (tabela âmbar) + pagamentos recebidos no mês (tabela verde) + subtotal
- `RelatoriosController` usa timezone de Brasília (consistente com `AnalyticsController`)

### v1.7.2 (2026-06-22) — Bugfixes de Timezone e Datas
- **`toDateInput` corrigido**: `financeiro/page.tsx` usava `toISOString()` → data enviada ao backend era UTC, fazendo "Hoje" zerar após 21h no Brasil. Fix: extração manual `getFullYear/Month/Date`.
- **Labels do gráfico de receita**: backend retornava `dd/MM` mas frontend aplicava `.slice(5)` (esperava `yyyy-MM-dd`) — labels em branco. Fix: backend agora retorna `yyyy-MM-dd` em `DiaFinanceiroDto.Dia`.
- **PDFs com data incorreta**: `relatorio-admin.ts` e `relatorio.ts` usavam `toISOString()` para cálculo de períodos — podia pular ou duplicar um dia por causa do UTC. Fix: usando data local explícita.
- **RelatoriosController sem fuso**: queries de mês usavam `DateTime.UtcNow` sem ajuste de timezone — vendas após 21h podiam cair no mês errado. Fix: `BrazilZone` aplicado igual ao `AnalyticsController`.
- [ ] SignalR sem polling paralelo quando conectado
- [ ] Plano de DR documentado e testado

---

## Apêndice B: Comandos Úteis de Manutenção

```bash
# Deploy
cd /opt/santuarionerd && git pull && bash deploy/update.sh

# Logs em tempo real
docker logs -f santuarionerd_api
docker logs -f santuarionerd_postgres

# Backup manual PostgreSQL
docker exec santuarionerd_postgres pg_dump -U cardgame_user cardgamestore > backup_$(date +%Y%m%d).sql

# Restaurar backup
cat backup_YYYYMMDD.sql | docker exec -i santuarionerd_postgres psql -U cardgame_user cardgamestore

# Acessar banco diretamente
docker exec -it santuarionerd_postgres psql -U cardgame_user -d cardgamestore

# Health check
curl http://localhost/health | jq

# Reiniciar apenas a API sem downtime completo
docker compose -f deploy/docker-compose.prod.yml restart api
```

---

*Documentação gerada com base em auditoria completa do código-fonte em 2026-06-22.*
*Próxima revisão recomendada: após implementação das correções de Prioridade 1 e 2.*
