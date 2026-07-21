# Auditoria de Escalonamento e Consistência — Tenant-ERP

> Data: 2026-07-17 (consolidada em 3 frentes: multitenancy/dados, API/segurança, frontend/deploy/testes)
> Escopo: backend .NET 8 (`CardGameStore/`, ~199 arquivos, 37+ controllers), frontend Next.js (`frontend/`), testes (`tests/`), CI/CD (`.github/workflows/ci.yml`), deploy (`deploy/`), docs da raiz.
> Produto: ERP SaaS multi-tenant para varejo de pequeno/médio porte. "CardGameStore" é nomenclatura legada.
> Arquitetura de isolamento: **schema-per-tenant no PostgreSQL** (catálogo em `public` + um schema por loja, `SET search_path` por conexão).

---

## Sumário executivo

O projeto está mais maduro do que o `STATUS.md` sugere: MongoDB eliminado, squash de migrations feito, CI com Postgres 16 real, 224 testes de backend incluindo isolamento de tenant contra Postgres real. A auditoria encontrou **12 problemas críticos** — **9 já corrigidos e verificados em 2026-07-20** (placar na seção 📊 Verificação), 1 parcial e 3 de arquitetura/escala ainda abertos (C3, C4, C5). O módulo fiscal segue com os 4 graves (F1–F4) sem correção.

O **módulo fiscal (NFC-e/SEFAZ)** recebeu auditoria dedicada (seção própria abaixo): o núcleo é funcional de verdade, mas **4 problemas graves impedem a homologação hoje** (F1–F4) e há mais 11 achados + 2 lacunas de escopo.

| Severidade | Quantidade | Com correção em andamento |
|---|---|---|
| 🔴 Crítico | 12 | 4 (+1 parcial) |
| 🟠 Bloqueio multi-instância | 4 | 0 |
| 🟡 Médio | 27 | 1 parcial |
| 🔵 Baixo | 13 | 0 |
| 🧾 Fiscal (seção dedicada) | 15 (4 🔴 / 8 🟡 / 3 🔵) + 2 lacunas | 0 |

---

## 📊 Verificação pós-correções — 2026-07-20 (o que foi feito de verdade)

Verificado item a item contra o código atual (HEAD `fbce607`). Commits analisados: `39a8e84` (tenant em jobs/NFC-e), `f63de14` (subdomínio 404), `8a17f1f` (deploy backup/rollback), `45e05e5` (limpeza Campeonatos), `fbce607` (audit fixes + mTLS por tenant).

### Placar

| Grupo | ✅ Corrigido | 🟡 Parcial | ❌ Não corrigido |
|---|---|---|---|
| 🔴 Críticos C1–C12 | 9 | 1 (C12) | 3 (C3, C4, C5) |
| 🧾 Fiscal F1–F15 | 0 | 3 (F2, F12, F15) | 12 |
| 🟡 Médios M1–M27 | 0 | 4 (M1, M18, M25, M26) | 23 |
| 🔵 Baixos B1–B13 | 0 | 0 | 13 |
| 🟠 Escala H1–H4 | 0 | 0 | 4 |
| Lacunas fiscais L1–L5 | 0 | 0 | 5 |

### ✅ Feito de verdade (verificado no código)

- **C1** — propagação de tenant na emissão NFC-e da comanda (`ComandaService.cs:698-701`) — commit `39a8e84`
- **C2** — 5 background services iteram tenants via `ForEachActiveTenantAsync` (`Multitenancy/TenantIteration.cs`) — commit `39a8e84`
- **C6** — guarda de status no `CloseComandaAsync` (`ComandaService.cs:447-448`) — commit `fbce607`
- **C7** — venda avulsa: validação antecipada + `CreateExecutionStrategy` + transação cobrindo estoque/venda/pós-venda (`VendaAvulsaService.cs:44-378`); NFC-e fora da transação com tenant propagado — commit `fbce607`
- **C8** — Operator não reseta senha de Admin (`UserService.cs:264-266`) nem auto-eleva perfil (`UserController.cs:417-418,:424-425`) — commit `fbce607`
- **C9** — certificado mTLS do Inter por tenant (`IntegrationConfig.CertificateCrtEncrypted/KeyEncrypted`, descriptografado por request em `InterSyncService.cs:325-328`; upload em `ContasReceberController.cs:434-445`) — commit `fbce607`
- **C10** — subdomínio inexistente retorna 404 (`TenantResolutionMiddleware.cs:63-68`) — commit `f63de14`
- **C11** — `update.sh` com backup pré-deploy, imagens `:rollback`, health check e auto-rollback — commit `8a17f1f`
- **C12** 🟡 — cron diário + backup inicial + verificação de integridade + opção off-site (`BACKUP_REMOTE_CMD`); **persistem**: retenção 7 dias e dump full sem restore por tenant
- **M18** 🟡 — Campeonatos removido de manual/perfis/termos/privacidade/cadastro/api.ts/tests (`45e05e5`); **sobraram**: remotePatterns TCG em `next.config.js:18,21`, seção TCG em `admin/manual/page.tsx:309-325`, `termos/page.tsx:178`, placeholder em `admin/usuarios/page.tsx:1123`

### ❌ Não corrigido (confirmado no código atual)

- **Críticos de arquitetura/escala:** C3 (tenant-zero silencioso), C4 (migrations no boot sem lock), C5 (SignalR sem backplane)
- **Fiscal:** 12 de 15 abertos — inclusive os 4 graves **F1–F4** (cupom de contingência com chave/QR errados; retransmissão morre em ~2,5 h; certificado vencido → contingência indevida; XML sem `nfeProc`). Parciais: F2 (alerta existe, mas passivo), F12 (pré-voo antes da reserva, mas sem CNPJ/IE/CSC), F15 (pontos de emissão checam módulo; jobs e DF-e não)
- **Médios:** 23 de 27 abertos (M2–M17, M19–M24, M27)
- **Baixos e escala:** B1–B13 e H1–H4 todos abertos (não eram alvo dos commits)
- **Lacunas fiscais L1–L5:** nenhuma implementada

### ⚠️ Problema novo introduzido

- O commit `fbce607` versionou `.next/trace` e `.next/trace-build` (artefatos de build) — a raiz `.next/` não está no `.gitignore` (só `frontend/.next/`). Remover do tracking (`git rm --cached`) e adicionar `/.next/` ao `.gitignore`.

---

## 🔴 Críticos — risco de dados, segurança ou bloqueio imediato

### C1. Emissão de NFC-e no fechamento de comanda grava no tenant errado  `✅ corrigido e verificado (20/07)`

- **Onde:** `CardGameStore/Services/Implementations/ComandaService.cs:688-691`
- **Problema:** o escopo criado para emitir a nota não propagava o `ITenantContext` da requisição → `NotaFiscalEmitida` gravada no schema `public` (tenant-zero), presa em `PendenteEmissao` para sempre, reprocessada em loop pelo retry fiscal.
- **Status:** corrigido no working copy (`Set(...)` propagado, mesmo padrão que `VendaAvulsaService.cs:200-201` já tinha). **Falta commit + teste de regressão.**

### C2. Cinco dos seis background services operam silenciosamente só no tenant-zero  `✅ corrigido e verificado (20/07)`

- **Onde:** `FiscalRetryBackgroundService.cs:45`, `FiscalXmlExportBackgroundService.cs:49`, `FiscalAlertBackgroundService.cs:46`, `SefazDistBackgroundService.cs:48`, `InterSyncService.cs:365`
- **Problema:** `CreateScope()` sem `ITenantContext.Set(...)` → retry de NF-e, envio mensal de XML ao contador, alerta de vencimento do certificado A1, manifestação do destinatário (DDA) e sync do Banco Inter **nunca rodavam para tenants reais** — só para o tenant-zero. O cabeçalho de `FechamentoBackgroundService.cs:5-14` documentava o problema.
- **Status:** corrigido no working copy via novo `Multitenancy/TenantIteration.cs` (`ForEachActiveTenantAsync`), mesmo padrão do `FechamentoBackgroundService`. **Falta commit.**

### C3. `ITenantContext` defaulta para tenant-zero em vez de falhar

- **Onde:** `CardGameStore/Multitenancy/ITenantContext.cs:35-47`
- **Problema:** qualquer caminho que crie escopo manual e esqueça o `Set(...)` opera no tenant-zero **silenciosamente** — causa-raiz de C1 e C2, e fonte de regressões futuras a cada novo service.
- **Sugestão:** modo fail-fast fora de request HTTP (ou marcador explícito de "tenant-zero intencional"), pelo menos em `Production`. **Aberto** (decisão de design; as correções C1/C2 mitigam os casos atuais).

### C4. Migrations por tenant rodam no boot de cada instância, em loop serial, sem lock

- **Onde:** `CardGameStore/Program.cs:489-513`
- **Problemas:** (1) startup O(N) — 200 tenants = 200 `MigrateAsync` sequenciais bloqueando o `app.Run()` e o `/health`; (2) com 2+ instâncias (rolling deploy), corrida de DDL em `__EFMigrationsHistory`; (3) `catch` em `:505-510` **engole falhas** — tenant fica sem migrar silenciosamente até o próximo restart.
- **Sugestão:** separar "processo migrador" de "processo web" (job único no deploy), com `pg_advisory_lock` e status de migration por tenant no catálogo. **Aberto.**

### C5. SignalR sem backplane — eventos não cruzam instâncias

- **Onde:** `CardGameStore/Program.cs:288` (`AddSignalR()` puro; nenhuma referência a Redis no projeto)
- **Problema:** eventos de comanda via `IHubContext<ComandaHub>` só alcançam conexões da instância local. Com 2+ instâncias, o dashboard em tempo real deixa de ser confiável.
- **Sugestão:** `AddStackExchangeRedis(...)` ao subir a 2ª réplica. **Aberto** (só bloqueia multi-instância).

### C6. Fechamento de comanda sem guarda de status — duplo fechamento duplica crediário, pontos e NFC-e  `✅ corrigido em fbce607 (verificado 20/07)`

- **Onde:** `ComandaService.cs` — `CloseComandaAsync` não verifica se a comanda já está `Fechada`/`Cancelada` (todos os outros métodos da classe verificam: `:382,:722,:840,:884,:1019`).
- **Efeito:** um segundo `POST /close` (duplo clique, retry de rede) re-executa todos os efeitos colaterais: crediário duplicado, segundo débito de pontos/cashback e — se `emitirNotaFiscal=true` — **segunda NFC-e autorizada na SEFAZ para a mesma venda** (`:685-699`, sem a guarda de duplicidade que a emissão manual tem em `FiscalController.cs:372-374`). `ComandaController.cs:408` captura `InvalidOperationException` esperando "já fechada", exceção que nunca é lançada.
- **Sugestão:** uma linha de guarda de status no início + constraint/índice. Resolve também a dupla emissão.

### C7. Venda avulsa faz commit parcial sem transação  `✅ corrigido em fbce607 (verificado 20/07)`

- **Onde:** `Services/Implementations/VendaAvulsaService.cs` — estoque decrementado via `ExecuteUpdateAsync` (`:96-111`), venda gravada (`:180-181`), NFC-e possivelmente autorizada (`:192-204`), e **só depois** validações pós-venda lançam `InvalidOperationException` (`:217-219,:223,:286-363`).
- **Efeito:** cliente recebe HTTP 400 mas a venda fica persistida, o estoque baixado e a nota possivelmente autorizada. Validação do 2º pagamento (`:141-149`) também ocorre após o decremento de estoque. Contraste com `CancelComandaAsync` (`ComandaService.cs:739-763`), que usa `CreateExecutionStrategy` + transação.
- **Sugestão:** validar antes de escrever + transação explícita com execution strategy.

### C8. Operator com permissão "usuarios" tem poderes de fato de Admin (escalação de privilégio)  `✅ corrigido em fbce607 (verificado 20/07)`

- **Onde:** `Program.cs:181` (policy `AdminOnly` aceita Admin **e** Operator) + `Perfil.cs:69` (permissão "usuarios" libera prefixo `/api/user`) + `UserService.cs`.
- **Efeito:** `AdminResetPasswordAsync` (`:255-272`) não restringe a role do alvo → **Operator reseta a senha do Admin e toma a conta**; `UserController.AtualizarPerfil` (`:409-444`) → Operator atribui a si mesmo qualquer perfil; `AdminCreateUserAsync` (`:200-253`) → cria outro Operator com qualquer `PerfilId`. A permissão granular vira escalação total.
- **Sugestão:** restringir role do alvo (Operator só gerencia Customer/Operator), impedir auto-elevação de perfil, auditar essas ações.

### C9. Certificado mTLS do Banco Inter é global do servidor, não por tenant  `✅ corrigido em fbce607 (verificado 20/07)`

- **Onde:** `InterSyncService.cs:329-339` (`BuildMtlsClient` lê `Inter:CertificatePath`/`KeyPath` do processo — um único cert para todos os tenants) + `ContasReceberController.cs:426-449`.
- **Efeito:** ClientId/ClientSecret são por tenant, mas o certificado é compartilhado — e o endpoint `UploadCertificado` permite que **qualquer admin de qualquer tenant sobrescreva o certificado usado por todos** → cobranças Pix de outros tenants quebradas ou direcionadas à conta errada.
- **Sugestão:** mover certificado para config por tenant (mesmo padrão do ClientId/Secret e do certificado A1 fiscal, que já é por tenant).

### C10. Subdomínio desconhecido serve silenciosamente a loja do tenant-zero  `✅ corrigido e verificado (20/07)`

- **Onde:** `CardGameStore/Multitenancy/TenantResolutionMiddleware.cs:84-86`
- **Problema:** slug bem-formado inexistente no catálogo (ex: `loja-errada.2esysten.com.br`) caía no fallback do tenant-zero — exibindo vitrine, produtos e tela de login **da loja errada**, com cookies válidos para aquele host. O usuário podia logar/comprar sem perceber.
- **Status:** corrigido no working copy — slug bem-formado sem tenant agora retorna **404** ("Loja não encontrada"), com teste novo em `TenantResolutionMiddlewareTests.cs`. **Falta commit.**

### C11. Deploy sem backup prévio e sem rollback  `✅ corrigido e verificado (20/07)`

- **Onde:** `deploy/update.sh:20-32` (versão commitada: `git pull` → `build` → `up -d` → prune), disparado automaticamente pelo CI a cada push verde na `main`.
- **Problema:** migrations rodam no boot; se uma migration quebrasse um schema ou o boot, não havia backup pré-deploy, imagem anterior, nem rollback.
- **Status:** corrigido no working copy — backup obrigatório antes de atualizar (aborta se falhar), imagens tagueadas `:rollback`, health check em `/health` com reversão automática. Limite documentado: rollback reverte código, não schema. **Falta commit.**

### C12. Backup não agendado por padrão e só na própria VPS  `🟡 parcial — cron/integridade/off-site ok; retenção 7d e restore por tenant pendentes (verificado 20/07)`

- **Onde:** `deploy/setup.sh` (versão commitada: nenhum cron), `deploy/backup.sh` (destino `/opt/tenant-erp/backups` na mesma máquina, retenção 7 dias, sem verificação de integridade).
- **Status no working copy:** setup agenda cron diário (03:00) + backup inicial; `backup.sh` valida integridade (`gzip -t` + tamanho mínimo) e oferece `BACKUP_REMOTE_CMD` para cópia off-site (ainda opcional — default continua local).
- **Persistem:** retenção curta (7 dias), sem teste de restore automatizado, dump full único sem restore por tenant (ver M20). **Revisar e commitar.**

---

## 🟠 Bloqueios para multi-instância / escala horizontal

### H1. Uploads em disco local
`UploadController.cs` salva em `wwwroot/uploads/` (volume Docker `api_uploads`, `docker-compose.prod.yml:123-124`); nginx serve `/uploads/` (`nginx.conf:96-102`). Imagens não cruzam réplicas → precisa de storage compartilhado (S3/MinIO) ou CDN antes da 2ª instância.

### H2. Cache e rate limiter em memória, por instância
Resolução de tenant: `TenantResolutionMiddleware.cs:24,42-49` — `IMemoryCache` TTL 30 s, sem invalidação (suspender tenant/módulos leva até 30 s × N instâncias para propagar). Rate limiter: `Program.cs:207-260` — FixedWindow in-memory; limites por IP multiplicam por N réplicas (`locate-account` 5/h vira 5×N/h).

### H3. Jobs agendados rodam em toda réplica, sem coordenação
Os 6 `BackgroundService` executam em toda instância. `FechamentoBackgroundService` sobrevive por idempotência (índice único `ix_fechamentos_periodo_janela`, `AppDbContext.cs:495-497`). **`SefazDistBackgroundService.cs:42`** consulta a SEFAZ a cada 2 h — com 2 instâncias dobra a frequência por CNPJ → risco de `cStat 656` (bloqueio de 1 h pela SEFAZ). `FiscalXmlExport`/`InterSync` duplicam e-mail/sync. `_provisionLock` estático (`TenantProvisioningService.cs:32`) só protege dentro de uma instância → provisionamentos simultâneos do mesmo slug deixam schema órfão. Sugestão: leader election via lock no Postgres.

### H4. Custo por conexão e limite de pooling
`TenantConnectionInterceptor.cs:48-94` — todo open executa `SET search_path` + `SELECT current_schema()`: 2 RTTs por request antes da primeira query útil. Invariante documentado (`:10-13`) proíbe `Multiplexing=true` e PgBouncer transaction mode → pool de 100 conexões/instância × N instâncias contra Postgres de 512 MB estoura rápido. Alternativas: `SET LOCAL` em transação, pool por tenant, ou quitar a verificação.

---

## 🟡 Inconsistências e dívidas — severidade média

### Integridade de dados e transações

**M1. Caminhos quentes de comanda sem transação** — `ComandaService.cs:181-203` (`AddItemAsync`), `:311-348` (`RemoveItemAsync`), `:369-418` (`UpdateItemAsync`): `ExecuteUpdateAsync` + `SaveChangesAsync` sem `BeginTransaction`; falha no meio debita estoque sem o item existir. O padrão correto existe em `CancelComandaAsync:739-763` e não foi aplicado.

**M2. Saldos de pontos/cashback sem concorrência atômica** — `ComandaService.cs:481-499,:609-640,:829-874` faz check-then-mutate via change tracker (dois débitos simultâneos passam); `VendaAvulsaService.cs:281-330` faz o contrário certo (`ExecuteUpdateAsync` atômico com predicado de saldo). Zero `RowVersion`/`xmin` no projeto.

**M3. Idempotência existe apenas no pagamento de crediário** — bem implementada (`CrediariosController.cs:381-466`, índice único `idempotency_key`, `AppDbContext.cs:283-286`). Venda avulsa, fechamento de comanda e criação de cobrança Pix não têm chave nem constraint — retries de rede geram duplicidades reais (ver C6/C7).

**M4. Reservas com corridas** — `ReservationController.cs`: `Homologar` (`:235-288`) sem transação e sem checagem atômica de status (duas homologações concorrentes debitam estoque duas vezes); `Create` (`:85-93`) com checagem de estoque TOCTOU.

**M5. Crediário: split não validado e reconciliação Pix com corrida** — `CrediariosController.cs`: `RegistrarPagamento` (`:390-424`) não valida o split contra o saldo; reconciliação Pix tem corrida em `pix.PagoEm is null` (`:560`) — dupla baixa Pix+manual depende só do índice de idempotency do request HTTP.

**M6. Edição de comanda fechada aceita preço do request** — `ComandaService.cs:1095-1097,:1117-1120` (`EditarComandaFechadaAsync`): contradiz a regra de `ResolveItemAsync` (preço sempre do cadastro) e alimenta o financeiro — admin/Operator pode reescrever retroativamente valores de vendas fechadas sem trilha explícita.

**M7. E-mail fire-and-forget de crediário perde o tenant** — `ComandaService.cs:598-603`: `Task.Run` com scope novo sem `Set(...)` → `EmailService` lê `EmailConfigs`/`SiteConfigs` do schema `public` (SMTP próprio do tenant e branding ignorados); exceções não observadas.

### Performance e custo por request

**M8. Fechamento financeiro oficial trunca em 2.000 vendas, silenciosamente** — `FinanceiroCalculoService.cs:54,:486` via `GetRecentAsync` (`VendaAvulsaService.cs:405-408`): mês com >2.000 vendas avulsas exclui as mais antigas de receita **e custo**, e o resultado errado é gravado como snapshot "congelado" em `FechamentoPeriodo`. Sem log/flag de truncamento. Idem `AnalyticsController.cs:58` (Take 5.000 com o jsonb inteiro).

**M9. Agregações de relatórios materializam tabelas em memória** — `RelatoriosController.cs:62-76`: todos os `ComandaItems` do mês (2 `Include`) + todas as `VendasAvulsas` do mês agregados em `Dictionary` — deveria ser `GROUP BY` no SQL.

**M10. `AccountLocatorService` é O(tenants) por chamada** — `AccountLocatorService.cs:60-86`: scope + query + `BCrypt.Verify` (~100 ms) por tenant ativo, sequencial. Com 200 lojas: ~20 s e 200 conexões por chamada legítima.

**M11. Painéis de plataforma agregam tenant-por-tenant por request** — `PlatformController.cs:497-540` (audit-logs), `:206-253` (overview), `ContadorPortalController.cs:69-100`: scope + query por tenant por request HTTP. O comentário em `:493-496` já reconhece que precisa virar job agregador.

### Segurança e contrato de API

**M12. Entidade EF `Product` no bind e no retorno — vaza preço de custo** — `ProductController.cs:98-115` (bind direto, mass assignment) e `GetAllStore` (`:44-51`, qualquer role, inclusive Customer) + GET anônimo (`:33`) serializam a entidade completa: `CostPriceInCents` (documentado como "visível só para o admin", `Product.cs:55-57`), margem e estoque mínimo expostos.

**M13. Permissões por prefixo amplas demais** — `Perfil.cs:62-76`: "estoque" (`/api/product`) cobre `/api/products/*` e variantes; "relatorios" expõe o relatório de crediário com nome/e-mail/WhatsApp dos devedores (`RelatoriosController.cs:160-234`); "lgpd" inclui `/api/audit` — Operator lê **todos** os audit logs (diffs de produtos/vendas/usuários, hash de IP).

**M14. Token CSC do QR Code NFC-e armazenado em claro** — `FiscalConfig.cs:98-101` + `FiscalController.cs:83`: só o `.pfx` e a senha do certificado são criptografados (AES-256-GCM). CSC em claro permite gerar QR codes válidos em nome da loja se o banco vazar.

**M15. Erros internos vazam ao cliente e padrão de erro é inconsistente** — `AiChatController.cs:50-55` devolve `ex.Message` com HTTP 200; `ComandaController.cs:342-345` devolve 500 com `ex.Message`. Controllers oscilam entre `{Message}`, `{message,traceId}` (middleware global) e erros de modelo — sem `ProblemDetails`, sem versionamento de rota.

**M16. Gemini: chave global, sem quota por tenant** — `GeminiChatService.cs:55` lê `GeminiSettings:ApiKey` do processo (`docker-compose.prod.yml:102`), rate-limitada só por IP ("api"): um único tenant pode esgotar a quota de todos. Inconsistente com o modelo de módulos/planos por loja.

**M17. DTOs sem validação + sequestro de subscrição push** — `SiteConfigController.cs:44-49` retorna entidade EF e `SaveSiteConfigRequest` (`:148-174`) não tem `[MaxLength]`/range; `PushController.cs:49-75` permite a qualquer autenticado reassociar a si qualquer `Endpoint` já cadastrado (sequestra notificações de outro usuário).

### Frontend, testes e deploy

**M18. Resíduos de Campeonatos/TCG — feature removida do backend, presente na UI** `🛠 limpeza parcial no working copy` — o backend não tem mais nada de campeonato, mas restam: manual in-app ensinando a feature inexistente (`admin/manual/page.tsx:123-136,:224,:251`), labels de permissões mortas (`admin/perfis/page.tsx:15,19` — parcialmente limpo), textos legais (`termos/page.tsx:24,118,178`, `privacidade/page.tsx:104,123,141-158`, `cadastro/page.tsx:67` — parcialmente limpos), tipagem morta (`api.ts:618-630` — removida no working copy), `tests/api/04-championship.http` inteiro contra endpoints 404, e `tests/README.md:16,31,141` citando `ChampionshipServiceTests.cs` inexistente.

**M19. Build do frontend passa com erros de TypeScript e ESLint** — `frontend/next.config.js:8-13` (`ignoreDuringBuilds` + `ignoreBuildErrors`); o CI roda só `npm run build` → **nada no pipeline valida tipagem/lint**; com deploy automático, erro de tipo vai direto para produção.

**M20. Smoke test pós-deploy é raso** — `.github/workflows/ci.yml` (último step): `curl http://$HOST/` só prova nginx+frontend; não bate `/health` nem `/api/...`. API morta no boot = deploy declarado sucesso. (O `update.sh` novo faz health check real — cobre parcialmente.)

**M21. Backup é dump único full — sem restore por tenant** — `deploy/backup.sh:47-49`: `pg_dump` do banco inteiro; não há como restaurar **uma** loja sem restaurar todas, nem teste de restore documentado.

**M22. SignalR não trata token expirado na reconexão** — `frontend/lib/signalr.ts:27-35`: `withAutomaticReconnect` sem `accessTokenFactory`/refresh → token de 60 min expira, negotiate leva 401 e o cliente tenta em loop para sempre. O admin tem refresh proativo (`admin/layout.tsx:20,40`), mas `/cliente` e `/mesa` não — comanda aberta >60 min perde o realtime silenciosamente. Bônus: `startHub()` sem `.catch` em `cliente/page.tsx:295` (unhandled rejection).

**M23. Suíte E2E Playwright é fachada** — `playwright.config.ts:74-78` (`webServer` comentado), nenhum script de teste no `package.json`, CI não roda, `example.spec.ts` é o template intocado (testa `playwright.dev`), `cliente.spec.ts:13` hardcoded `localhost:3000`. `README.md:204-208` manda rodar como se houvesse suíte real.

### Operação e documentação

**M24. Hardcodes de infraestrutura no frontend** — IP do VPS `179.197.67.64` (`plataforma/tenants/[id]/page.tsx:335`) e `2esysten.com.br` (`CreateTenantModal.tsx:62`, `page.tsx:319-320,336`, `middleware.ts:3-5`) em textos de UI — quebra white-label e troca de VPS sem rebuild.

**M25. Documentação viva diverge do código a cada sprint** — `STATUS.md` inteiro datado de 2026-07-10 (diz que multi-tenant "travou"; tudo implementado há uma semana); `BACKLOG.md` lista como pendentes Mongo e squash (feitos) e diz "Playwright sem nenhum teste" (há 2 specs mortos); `DOCUMENTACAO-COMPLETA.md:63` diz `/hub/*` (real: `/hubs/*`), `:143` atribui emissão SignalR ao controller (real: `ComandaService` → grupo `AdminDashboard_{tenantId}`), `:268` lista `.AsNoTracking()` como pendência (já aplicado); `README.md:61` "SignalR (SSE + Long Polling)" (cliente prefere WebSocket); `CASOS_DE_USO.md:17` "access token de 15 min" (appsettings: 480; prod: 60); `KYC_PLANNING.md:99-100` prescreve DDL via `ExecuteSqlRaw` (padrão agora é EF Migrations).

**M26. Senha default `SenhaForte@123` como fallback no código** — `Program.cs:521,:547`: há warning, mas o admin/dono da plataforma é criado mesmo assim se a env faltar. Sugestão: em `Production`, falhar o boot em vez de criar com senha conhecida.

**M27. Limites de recursos do compose subdimensionados para o boot O(N)** — `docker-compose.prod.yml`: API 1 CPU/512 MB, Postgres 512 MB, frontend 512 MB, nginx 128 MB. Com o loop de migrations por tenant crescendo (C4), o boot da API estoura memória antes de o Postgres virar gargalo.

---

## 🔵 Baixo impacto

**Backend**
- **B1.** `SefazNfeService.cs:259-313` — "Ciência da Operação" manifestada automaticamente em massa (até 60 notas/ciclo, sem revisão humana): evento com efeito jurídico; merece ciência explícita do lojista/contador.
- **B2.** `ComandaService.cs:996-999` — estoque baixa ao adicionar item e só retorna em remoção/cancelamento; **sem expiração de comandas abertas** → comanda abandonada prende estoque indefinidamente.
- **B3.** `ComandaHub.cs:56-59` — só `Role=="Admin"` entra no grupo admin do tenant (Operator fica sem tempo real); `CloseComanda` do hub exige Admin enquanto o REST permite Operator — inconsistência de superfície.
- **B4.** `LgpdController.cs:112` — JSON montado na mão com e-mail interpolado (quebra com aspas) + salt fallback hardcoded (`:52`); `EncryptionService.cs:19-26` aceita chave zero em dev; `appsettings.json:19` access token de **8 horas** (janela longa para token não revogável).
- **B5.** Estilos de autorização mistos sem critério documentado — `Roles="Admin"` (`TimerController.cs:11`, `PerfisController.cs:24`), `Roles="Admin,Operator"` (`ProductController.cs:55`), policies — `TimerController` inacessível a Operators mesmo com perfil; DTOs do timer aceitam duração negativa e ação desconhecida retorna 200 silencioso (`TimerController.cs:55-92`).
- **B6.** Superfícies anônimas: `PublicProfileController.cs:34-60` expõe saldo de pontos e nº de compras por GUID; `ProductVariantController.cs:26-39` expõe estoque exato; `MensageriaRequest` (`MensageriaController.cs:174-187`) sem limite de tamanho de título/corpo.
- **B7.** `UserController.cs:119-122,:191-194` devolvem 404 para `InvalidOperationException` (conflito de negócio, não "não encontrado").

**Frontend/Testes/Deploy**
- **B8.** `TestDbFactory.cs:37-43` — porta fixa 5433 sem preflight: se ocupada, os ~224 testes falham em massa com erro cru de conexão.
- **B9.** `next.config.js:17-25` — `remotePatterns` legados de TCG (`pokemontcg.io`, `scryfall.io`, `tcgplayer.com`) e `http://localhost` permitido na config que vai para produção.
- **B10.** `run-tests.ps1:1` — path absoluto de outra máquina (repo antigo); roda só `CreditarioServiceTests`.
- **B11.** `deploy/cleanup.sh:14-15` — comentários prometendo não tocar em `mongo_data`/container `mongo`, que não existem mais.
- **B12.** `deploy/promote-tenant-schema.sh` — script one-shot órfão (não chamado por setup/update/CI); lista `TABLES` (`:87-94`) sincronizada manualmente com as migrations — reutilização futura pode mover dados parcialmente (o próprio script avisa, `:84-86`).
- **B13.** Fetch cru fora do cliente centralizado em 3 chamadas LGPD públicas (`CookieBanner.tsx:36`, `app/lgpd/page.tsx:76,103`) apesar de `lgpdApi` existir em `api.ts:911-927` — sem impacto funcional.

---

## 🧾 Módulo Fiscal (NFC-e/SEFAZ) — auditoria dedicada

> **Veredito:** o núcleo é funcional de verdade, não fachada — monta o XML NFC-e 4.00 completo, assina com o A1 do tenant, transmite síncrono via Zeus, persiste status/protocolo, e tem retry, contingência offline, cancelamento por evento, inutilização automática, exportação ao contador (3 vias) e pipeline completo de DF-e/manifestação. **Mas 4 problemas graves (F1–F4) impedem a homologação hoje.**
> Formato checklist para correção item a item.

### Fluxo suportado hoje, etapa por etapa

| # | Etapa | Status | Observação |
|---|---|---|---|
| 1 | FiscalConfig (CRT, série, CSC, ambiente, e-mail contador) | ✅ | `FiscalController.cs:51-106`; default homologação. Ressalva: `ProximoNumeroNfce` não é ajustável via API (loja migrando de outro emissor precisa de SQL manual) |
| 2 | Naturezas de operação (CFOP/CSOSN por produto + padrão) | ✅ | `FiscalController.cs:158-303`; CSOSN 201/202/203 bloqueados com orientação ao contador |
| 3 | Upload/validação certificado A1 | 🟡 | Valida senha/chave, criptografa (AES-256-GCM) — **não rejeita expirado** (doc do endpoint promete rejeitar, `FiscalController.cs:108-110`); alertas 30/15/7/1 dias ✅, mas vencido não bloqueia emissão |
| 4 | Gatilhos de emissão | ✅ | Fechamento de comanda (opt-in + guarda de módulo), venda avulsa, manual tardia (`FiscalController.cs:369-393`) |
| 5 | Montagem do XML (itens, tributos, pagamentos, QR) | 🟡 | Completo; **só Simples Nacional** (ver F10); sem CSC transmite sem QR (ver F12) |
| 6 | Transmissão SEFAZ | ✅ | Síncrono, layout 4.00, timeout 15 s, fuso America/Sao_Paulo explícito |
| 7 | Tratamento de rejeição | 🟡 | Inutiliza o número na hora — inclusive rejeições corrigíveis (CPF inválido) que deveriam retransmitir com o mesmo número |
| 8 | Contingência offline (tpEmis=9) | 🟡 | Existe e preserva cNF/dhCont — mas com F1/F2/F5 abaixo; **EPEC/SVC não existem** |
| 9 | Cancelamento | 🟡 | Evento real, cStat 135/136, justificativa ≥15 — janela 30 min hardcoded, **protocolo do cancelamento descartado**, não estorna nada no ERP |
| 10 | Inutilização de numeração | 🟡 | Só automática de rejeitada; **sem inutilização manual de faixa**; buracos por crash ficam invisíveis (F6) |
| 11 | Carta de correção (CC-e, 110110) | ❌ | Zero ocorrências; a lib suporta. Escopo estreito em NFC-e — ausência defensável, mas não documentada como decisão |
| 12 | Exportação mensal ao contador | 🟡 | ZIP manual + e-mail dia 1 + portal do contador — **conteúdo errado** (F4); bug de fuso na janela manual (F11) |
| 13 | Distribuição DF-e + manifestação | ✅ | A parte mais bem construída: NSU incremental, cStat 656 com parada imediata, ciência em lotes de 20, parser `<cobr><dup>` → contas a pagar, cancelamento do emitente propaga (`SefazNfeService.cs:238-255`). Só "ciência" — sem confirmação/desconhecimento |
| 14 | Feature flag de módulo | ✅ | `[RequireModule("fiscal")]` no controller; gatilhos ignoram silenciosamente sem o módulo. Ressalva: background services e DF-e não filtram (F15) |

### Lógica fiscal brasileira — verificada como correta

Chave de 44 dígitos pela lib com cDV em `ide.cDV` · cNF aleatório de 8 dígitos **preservado entre tentativas de contingência** (`NfceEmissionService.cs:467` + `NotaFiscalEmitida.CnfContingencia`) · numeração com reserva atômica `UPDATE...RETURNING` (`:430-448`) · QR Code 2.0 com CSC id+token pela lib (`:554`) · `tpAmb` configurável, default homologação · CSOSN por item via NaturezaOperacao → padrão da loja → default 102 · CFOP default 5102 · PIS/COFINS CST 99 zerados e ICMSTot zerado (prática de mercado p/ Simples) · `dhEmi` = momento da venda, não da transmissão (`:466`) · justificativa de cancelamento ≥15 caracteres · inutilização automática de número rejeitado (`:626-637`).

### Achados fiscais — checklist de correção

- [ ] **F1 (🔴 Alta) — Contingência offline: o cupom impresso tem chave e QR que nunca existirão na SEFAZ** — `NfceEmissionService.cs:462-469,:571-580,:606`
  - **Problema:** na 1ª tentativa o XML é montado como emissão normal (tpEmis=1) e essa chave/QR são persistidos como artefatos do cupom. Na retransmissão vira tpEmis=9 → **chave de 44 dígitos diferente** (tpEmis é o 35º dígito), com `dhCont/xJust` e QR de fórmula offline. O comentário `:459-461` ("a MESMA chave") está errado. Banco ≠ papel entregue ao cliente, que não consegue consultar a compra.
  - **Correção:** ao entrar em contingência, reconstruir o XML com tpEmis=9 + `dhCont`/`xJust`, **reassinar e regenerar o QR offline antes de imprimir/persistir**; gravar a chave offline já na 1ª gravação. Teste de regressão: "chave do cupom == chave retransmitida".

- [ ] **F2 (🔴 Alta) — Retransmissão de contingência desiste após ~2,5 h; prazo legal é 24 h** — `NfceEmissionService.cs:76,:124-136` + `FiscalRetryBackgroundService.cs:44`
  - **Problema:** `MaxTentativasReprocessamento=10` vale também para `AutorizadaContingencia`; cada ciclo de 15 min consome 1 tentativa. Esgotado, a nota nunca mais é retransmitida — **nem pelo botão manual** (mesmo guarda). Sem alerta proativo. Estourada a NT 2015.002 (24 h), a venda fica permanentemente sem documento fiscal válido.
  - **Correção:** política própria para contingência (tentar por 24 h com backoff); retry manual independente do contador; alerta (dashboard/e-mail) ao se aproximar do prazo.

- [ ] **F3 (🔴 Alta) — Certificado vencido ⇒ "contingência offline" indevida (tpEmis=9 com SEFAZ operante)** — `FiscalCertificadoService.cs:18-37` + `NfceEmissionService.cs:94-100,:564-585`
  - **Problema:** upload não checa `NotAfter`; na emissão, a falha de handshake mTLS vira `HttpRequestException` → `EhFalhaDeConectividade` retorna true → cai no branch de contingência: cupons tpEmis=9 (só legais com a **SEFAZ** fora) que jamais autorizarão. Nada bloqueia emissão com certificado vencido.
  - **Correção:** rejeitar certificado expirado no upload (como a doc do endpoint já promete); bloquear emissão com certificado vencido; no classificador, distinguir falha de TLS/certificado local de indisponibilidade da SEFAZ — erro de certificado nunca pode cair em tpEmis=9.

- [ ] **F4 (🔴 Alta) — `XmlAutorizado` é o XML de envio (enviNFe), não o nfeProc — o ZIP do contador não contém o documento fiscal hábil** — `NfceEmissionService.cs:611` + `FiscalXmlExportService.cs:36-45`
  - **Problema:** `retorno.EnvioStr` é o envelope `<enviNFe>` **sem `protNFe`** (confirmado no fonte da Zeus). O comentário do model ("com protNFe anexado", `NotaFiscalEmitida.cs:78-79`) está errado. O `nfeProc` (obrigatório para guarda de 5 anos e aceito em sistemas contábeis) nunca é montado — a lib devolve o protocolo em `retorno.Retorno.protNFe`. Exportação errada nas 3 vias (manual, e-mail, portal).
  - **Correção:** montar e persistir o `nfeProc` (NFe assinada + protNFe); exportar esse arquivo; corrigir o comentário do model.

- [ ] **F5 (🟡 Média) — Rejeição pós-contingência entra em loop: inutiliza o número e o retry reutiliza o número já inutilizado** — `NfceEmissionService.cs:124,:463,:626-637`
  - **Correção:** rejeitada ex-contingência não deve reutilizar número inutilizado — reservar número novo (e inutilizar o anterior se ainda não foi) ou marcar como não reprocessável e exigir ação manual.

- [ ] **F6 (🟡 Média) — Buracos de numeração silenciosos quando a exceção ocorre entre a reserva e a resposta da SEFAZ** — `NfceEmissionService.cs:463,:279-286,:571-572,:600-601`
  - **Correção:** persistir o número na nota imediatamente após a reserva (antes de transmitir); rotina para detectar saltos e inutilizar (ver inutilização manual de faixa, lacuna L2).

- [ ] **F7 (🟡 Média) — Cancelar a NFC-e não estorna nada — e a UX manda o usuário usá-lo como estorno** — `NfceEmissionService.CancelarAsync (:151-189)` + `ComandaService.cs:730-733`
  - **Problema:** estoque, crediário, pontos/cashback e financeiro não são revertidos; venda avulsa nem tem método de cancelamento/estorno. Venda cancelada no Fisco continua receita e estoque baixado no ERP.
  - **Correção:** ao confirmar o cancelamento na SEFAZ, estornar os efeitos no ERP (ou criar fluxo de estorno explícito); implementar cancelamento/estorno de venda avulsa.

- [ ] **F8 (🟡 Média) — TOCTOU na emissão manual tardia: dupla NFC-e para a mesma origem** — `FiscalController.cs:372-374,:387-389` + `AppDbContext.cs:176-185` (índices **não únicos** em `ComandaId`, nenhum em `VendaAvulsaId`)
  - **Correção:** índice único parcial por origem (`ComandaId`/`VendaAvulsaId`, excluindo canceladas/rejeitadas); violação → 409. (Distinto de C6, que é a dupla emissão pelo duplo fechamento.)

- [ ] **F9 (🟡 Média) — Protocolo e procEventoNFe do cancelamento são descartados** — `NfceEmissionService.cs:175-185` (lê só cStat); o model nem tem campo
  - **Correção:** adicionar `ProtocoloCancelamento` + `XmlEventoCancelamento`, persistir e incluir no ZIP do contador (guarda documental exige a prova do cancelamento).

- [ ] **F10 (🟡 Média) — Regime tributário configurável mas quebrado fora do Simples — sem aviso** — `NfceEmissionService.cs:703,:720-742,:744-749` + `FiscalController.cs:85-87,:174-185`
  - **Problema:** config permite Lucro Presumido/Real e `MapCrt` emite `RegimeNormal`, mas os itens sempre levam classes `ICMSSN*` (CSOSN de Simples) → XML inconsistente CRT×CSOSN → 100% de rejeição.
  - **Correção:** bloquear a escolha de LP/LR na config com aviso "somente Simples Nacional" (ou montar ICMS CST por regime).

- [ ] **F11 (🟡 Média) — Exportação manual de XMLs com janela de fuso errada** — `FiscalController.cs:448` + `ContadorPortalController.cs:242`
  - **Problema:** `inicio.ToUniversalTime()` sobre `DateTime` Unspecified de query string interpreta como horário local do servidor (container em UTC): notas entre 21h–24h de Brasília na virada de mês caem no ZIP do mês errado. O job automático já documenta e corrige esse bug (`FiscalXmlExportBackgroundService.cs:85-96`) — só no caminho automático.
  - **Correção:** aplicar a mesma interpretação de fuso do job nos dois endpoints manuais.

- [ ] **F12 (🟡 Média) — Pré-voo de configuração não cobre CNPJ/IE/CSC — sem CSC, transmite sem QR e queima número** — `NfceEmissionService.cs:395-401,:552-556` + `FiscalConfig.cs:42-44`
  - **Problema:** `AbrirConfiguracaoSefazAsync` só exige certificado e endereço; `Cnpj` default `""` → chave inválida; sem CSC, `qrCodeUrl` sai null e a transmissão segue **sem `infNFeSupl.qrCode`** → rejeição da NFC-e (QR obrigatório) + número inutilizado à toa.
  - **Correção:** validar CNPJ, IE, CSC id+token, série e numeração **antes de reservar número** → `FiscalNaoConfiguradoException`.

- [ ] **F13 (🔵 Baixa) — Nota autorizada após rejeição+inutilização fica com estado contraditório** — `NfceEmissionService.cs:603-613` + `FiscalController.cs:339`
  - **Correção:** ao autorizar com número novo, limpar `InutilizadoEm`/`ProtocoloInutilizacao` do número antigo.

- [ ] **F14 (🔵 Baixa) — Janela de cancelamento fixa em 30 min; contingência autorizada tardiamente nasce incancelável** — `NfceEmissionService.cs:73,:162-164,:608-610`
  - **Correção:** janela configurável por UF; em contingência, medir a janela a partir da autorização (protocolo), não de `EmitidoEm` preservado da venda.

- [ ] **F15 (🔵 Baixa) — Background services fiscais e endpoints de DF-e não respeitam o módulo "fiscal"** — `Multitenancy/TenantIteration.cs:42-45` + `ContasReceberController.cs:12-15`
  - **Correção:** `ForEachActiveTenantAsync` deve filtrar por `EnabledModules` nos jobs fiscais (e-mail de certificado/XML dispara para tenant sem o módulo se houver config residual); adicionar `[RequireModule("fiscal")]` no `ContasReceberController`.

### Lacunas de escopo (decisões a tomar/documentar, não bugs)

- **L1 — Carta de correção (CC-e, evento 110110) não existe.** Em NFC-e o escopo da CC-e é estreito, então a ausência é defensável — documentar como decisão ou implementar (`RecepcaoEventoCartaCorrecao` existe na lib).
- **L2 — Sem inutilização manual de faixa de numeração** (só automática de rejeitada). Necessária para cobrir os buracos de F6 e para operação real (PDV queimado, faixa perdida). Sugestão: endpoint com justificativa ≥15 caracteres.
- **L3 — `ProximoNumeroNfce` não ajustável via API** — loja migrando de outro emissor precisa de SQL manual para continuar a sequência.
- **L4 — Manifestação do destinatário só faz "ciência"** — sem confirmação/desconhecimento/operação não realizada (avaliar se entra no escopo do produto).
- **L5 — EPEC/SVC não existem** como formas de contingência (só tpEmis=9 offline).

### Cobertura de testes fiscais

27 métodos de teste, **todos de lógica interna defensiva** — zero contra SEFAZ real ou mockada. `NfceEmissionServiceTests` (20): "nunca lança, sempre PendenteEmissao", limites de reprocessamento, guards de cancelamento, mapeamento CSOSN (6), classificador de conectividade (3). `FiscalCertificadoServiceTests` (3 — **não testa expirado**, porque o código não valida). `FiscalXmlExport*` (3). `FiscalAlertCalculatorTests` (8 casos).

**Sem nenhum teste:** `SefazNfeService` inteiro (~500 linhas do pipeline DF-e); montagem do XML/chave/QR; entrada e retransmissão de contingência (pegaria F1); inutilização; reserva atômica de número; `FiscalController`; propagação de tenant da correção C1; classificação TLS-expirado→contingência (F3). **Sugestão:** mock de `ServicosNFe` para testar rejeição/autorização/contingência sem SEFAZ real.

---

## ✅ Verificado como correto (não é dívida)

**Multitenancy e dados**
- `TenantConnectionInterceptor` sem fallback para `public`, valida nome de schema (regex) e confere `current_schema()` — defesa contra SQL injection (`:96-129`); isolamento **físico por schema**, sem dependência de query filters; cache de IModel do EF não é problema (modelo idêntico entre schemas).
- `MigrationsHistoryTable` por schema (`Program.cs:82-87`) — corrige mismatch real de detecção da tabela de histórico.
- Provisionamento na ordem certa (catálogo → `CREATE SCHEMA` → `MigrateAsync` → admin) com rollback da entrada do catálogo; regex de slug e nomes reservados (`TenantProvisioningService`).
- `TenantClaimGuardMiddleware` rejeita token de um tenant usado no host de outro com 401 (`:41-49`).
- Migrations no boot tolerantes por tenant — um schema quebrado não trava os demais (ressalvas de arquitetura em C4).
- `AccountLocatorService` cria scope por tenant com `Set`, tickets de 90 s, e-mail mascarado em log (`:88,:111-119`).

**Auth e segurança**
- Refresh tokens SHA-256 em banco, logout revoga, forgot-password anti-enumeração com equalização de tempo; cookies HttpOnly; aviso de `COOKIE_SECURE=false` em todo boot de produção.
- Rate limiting particionado por `CF-Connecting-IP` (não pelo IP do nó Cloudflare), com políticas global/auth/locate-account/comanda-hub.
- CORS por config + liberação de subdomínios do `RootDomain`; headers de segurança + CSP diferenciado para `/swagger`; Swagger só em Development.
- Plataforma: policy `PlatformOwnerOnly`, tickets de impersonação de uso único/90 s/vinculados ao tenant; suporte cross-tenant responde 404 (não 403) para não confirmar existência (`SupportController.cs:129-131`).
- Upload validado: whitelist de MIME + extensão, 5 MB, `[RequestSizeLimit]`, policy AdminOnly.
- Auditoria: `AuditSaveChangesInterceptor` com redaction de `PasswordHash`/`RefreshToken`/`PasswordResetToken`, IP via HMAC-SHA256, nunca aborta o `SaveChanges` do usuário.

**Fiscal (motor NFC-e/SEFAZ)**
- Numeração reservada com `UPDATE...RETURNING` atômico (`NfceEmissionService.cs:430-448`); número rejeitado **inutilizado automaticamente** na SEFAZ (`:626-637`); cancelamento com janela legal de 30 min e justificativa ≥15 caracteres; contingência offline preserva chave/cNF; CSOSN de ICMS-ST bloqueado de propósito com orientação ao contador; emissão nunca derruba a venda.
- Certificado A1 **por tenant**, validado no upload, AES-256-GCM, decriptado só em memória, nunca retornado; guarda de duplicidade na emissão manual tardia (`FiscalController.cs:372-374`); flag de emissão ignorada sem o módulo fiscal contratado.
- SEFAZ DF-e: certificado do tenant, `cStat 656` respeitado com parada imediata, `ultNSU` persistido por lote, contas a pagar canceladas junto com a nota.

**Estoque e financeiro**
- Decremento de estoque atômico com trava otimista no `WHERE` (`ComandaService.cs:996-1003`, `VendaAvulsaService.cs:96-111`); `CancelComandaAsync` transacional com devolução de estoque e pontos.
- `FechamentoBackgroundService` itera tenants com `Set` por scope, upsert idempotente por janela com índice único e tratamento de corrida (`FinanceiroCalculoService.cs:444-466`).
- `FinanceiroCalculoService` único compartilhado entre dashboard e fechamento — ponto de consolidação correto.

**Frontend e AI**
- Cliente de API realmente centralizado (`api.ts`, 1442 linhas, ~99% das chamadas) com mutex de refresh e redirect por tipo de página; baseURL relativa correta para multi-domínio; cookies host-only + `localStorage` por origem + reload por subdomínio → sem vazamento de estado entre tenants.
- Re-join de grupos SignalR garantido no servidor (`ComandaHub.cs:49-72`); tenant suspenso redireciona para `/loja-suspensa` (`SiteConfigContext.tsx:64-70`).
- AiChat **não executa ações** — só emite marcadores `NAV`/`WIZARD` interpretados pelo frontend; contexto anonimizado ("Cliente #N"); isolado por schema do tenant.
- SMTP por tenant com fallback global, senha criptografada e nunca retornada; HTML de anúncios escapado.

**Testes, CI e infra**
- 224 `[Fact]/[Theory]` cobrindo pontos críticos: `ComandaServiceTests` (25), `TenantResolutionMiddlewareTests`, `TenantIsolationTests` usando o interceptor **real** contra Postgres (não mock), `AuthServiceTests` (20), `NfceEmissionServiceTests` (20), idempotência de crediário. `TestDbFactory` com schema isolado por teste e pool único.
- CI com Postgres 16 real, build + testes backend, build frontend, CD via SSH com secrets sanitizados; `promote-tenant-schema.sh` bem construído (validação, confirmação, backup prévio, transação única).
- MongoDB efetivamente removido do código (restam só comentários históricos); `bin/`, `obj/`, `.next/` fora do git; `deploy/certs/` ignorado; `.gitignore` cobre `.env`.
- Posse verificada em endpoints de cliente: `NotificationsController`, `MinhasNotasController` (cupom só da própria nota), `ProductWaitListController`.
- LGPD: posse por protocolo+e-mail, HTML encoding, anonimização em vez de delete físico; `KycController` intencionalmente inerte.

---

## Plano de ação priorizado

| P | Ação | Achados | Por quê |
|---|------|---------|---------|
| **P0a** | Revisar, testar e **commitar** as correções do working copy (tenant na NFC-e, background services, 404 de subdomínio, deploy com backup/rollback, cron de backup) | C1, C2, C10, C11, C12 | Correções prontas de bugs de isolamento fiscal e segurança de deploy — paradas só por falta de commit |
| **P0b** | Guarda de status no `CloseComandaAsync`; restringir role do alvo em reset de senha/perfil (Operator); certificado Inter por tenant; transação + validação antecipada na venda avulsa | C6, C8, C9, C7 | Dupla NFC-e, escalação a Admin, Pix cross-tenant, commit parcial — todos abertos, todos com correção pequena |
| **P0c** | Fiscal — os 4 graves que impedem homologação: cupom de contingência com chave/QR corretos (tpEmis=9 antes de imprimir), retransmissão por 24 h com alerta, certificado vencido bloqueado (upload + emissão + classificador), persistir/exportar `nfeProc` | F1–F4 | Sem isso o módulo fiscal não homologa: cupom inválido, contingência expirada, emissão irregular e XML sem valor legal ao contador |
| **P1** | Tirar migrations do boot: job migrador separado, `pg_advisory_lock`, status por tenant, falha visível; considerar `ITenantContext` fail-fast fora de request | C4, C3 | Deploy trava/corrompe com o crescimento de tenants |
| **P2** | Pacote multi-instância: Redis (backplane SignalR + cache distribuído), storage compartilhado de uploads, leader election nos jobs, revisão do interceptor de conexão | C5, H1–H4 | Só necessário ao sair de 1 réplica |
| **P3** | Integridade e performance: transações nos caminhos quentes, concorrência em saldos, idempotência sistêmica, agregações em SQL, `AccountLocator` e painéis de plataforma | M1–M11 | Corrida e custo por request em tenant grande |
| **P4** | Segurança média e higiene: DTOs de produto/config/push, permissões por prefixo, CSC criptografado, padrão de erro/`ProblemDetails`, Gemini por tenant, type-check no CI, smoke test real, resíduos TCG, hardcodes, docs, senha default, compose | M12–M27, B1–B13 | Superfície de ataque e dívida de consistência |
| **P5** | Fiscal — demais correções e decisões: loop de rejeição pós-contingência, buracos de numeração, estorno no cancelamento, índice único por origem, protocolo do cancelamento, CRT fora do Simples, fuso na exportação manual, pré-voo de config; decidir CC-e, inutilização manual de faixa e ajuste de numeração inicial | F5–F15, L1–L5 | Robustez fiscal pós-homologação |

---

## Questão em aberto que afeta a priorização

**O plano é continuar em VPS único (escala vertical) ou multi-instância está no horizonte próximo?**

- Se **VPS único** por enquanto: o bloco 🟠 (H1–H4) e C5 podem esperar; foco total em P0/P1.
- Se **multi-instância** em breve: P2 sobe para junto do P1, pois C5/H1/H3 quebram funcionalidades no dia em que a 2ª réplica subir.
