# Backlog — Tenant-ERP

## Concluído (sessão 2026-07-14, avaliações externas + vulnerabilidades npm)
- Duas avaliações externas (`avaliacao_completa_softnerd.md` /
  `avaliacao_completa_2esysten.md`, ambas de 14/07, fora do repo) foram
  verificadas item a item contra o código atual antes de agir. Vários achados
  já estavam **desatualizados**: `JSON.parse` sem try-catch (os 7 call sites já
  estão protegidos), MongoDB no docker-compose de produção (já removido),
  migrations frágeis (já squashadas na `InitialSquash` de 10/07).
- **Corrigido — vulnerabilidades npm** (9 → 2): `npm audit fix` resolveu axios
  (8 CVEs: ReDoS, vazamento de Proxy-Authorization, prototype pollution),
  form-data (CRLF injection), js-cookie (hijack de protótipo) e ws (DoS por
  exaustão de memória, via SignalR) dentro dos ranges já declarados.
- **Corrigido — next 14.2.5 → 14.2.35**: ~25 CVEs acumulados (cache poisoning,
  SSRF via middleware, bypass de autorização, XSS com nonce de CSP). Patch na
  mesma branch 14.2.x; `next build` e `next dev` smoke-testados, e o
  `scripts/patch-next.js` do postinstall continua aplicando.
- **Corrigido — jspdf 2.x → 4.2.1 + jspdf-autotable 3.x → 5.0.8**: elimina o
  dompurify vulnerável (14 CVEs de XSS/bypass de sanitização). Migração via
  `applyPlugin(jsPDF)` no helper `getJsPDF()` dos 3 geradores de relatório
  (`lib/relatorio*.ts`) — API `doc.autoTable`/`lastAutoTable.finalY` preservada,
  smoke test de runtime validou PDF válido com os padrões usados.
- **Restam 2 vulns npm** (next high + postcss moderate) que só saem com upgrade
  major pro Next 16 — fora do escopo, ver backlog abaixo.
- **Corrigido — COOKIE_SECURE**: warning em todo boot de produção (não só no
  seed) enquanto `COOKIE_SECURE=false`, + comentários inequívocos no
  `deploy/setup.sh` e `deploy/.env.example` sobre o follow-up obrigatório ao
  configurar domínio+Cloudflare.
- **Corrigido — senha padrão removida do STATUS.md** (ficava em texto puro
  rastreado pelo git; agora aponta pra `ADMIN_SEED_PASSWORD`).
- **Corrigido — hash de IP LGPD**: `SHA256(salt + ip)` (vulnerável a
  length-extension) → `HMACSHA256(key: salt, msg: ip)` nos 3 pontos
  (`AuditService`, `AuditSaveChangesInterceptor`, `LgpdController`). Hashes
  antigos no banco ficam órfãos de correlação — aceitável, são pseudônimos.
- **Corrigido — magic strings de pagamento**: constantes `PaymentMethod` (já
  existiam em `VendaAvulsa.cs`) agora usadas em ComandaService/Controller,
  CrediariosController, AnalyticsController, NfceEmissionService,
  ReservationController e DTOs — antes cada um tinha literais `"Pix"` etc.
- **Corrigido — `.AsNoTracking()`** nas queries de leitura de Analytics,
  FinanceiroCalculoService, Relatórios e background services de
  fechamento/export fiscal.
- **Corrigido — `BrazilTime` centralizado** (`Common/BrazilTime.cs`): as cópias
  de `DiaBrasil()`/conversões de fuso duplicadas em controllers/services agora
  delegam pra um único helper.
- **Criado — CI/CD no GitHub Actions** (`.github/workflows/ci.yml`): job de
  backend com Postgres 16 real (mesma porta/credenciais do `TestDbFactory`,
  connection string default da suíte já funciona) + job de frontend com
  `npm ci` + `next build`. Era P0 nas duas avaliações. Push na main com CI
  verde dispara deploy automático via SSH (`update.sh` no VPS) + smoke test.
  **Pendente de ativação**: cadastrar 3 secrets no GitHub (Settings → Secrets
  and variables → Actions): `DEPLOY_HOST` (IP do VPS), `DEPLOY_USER` (ex:
  root) e `DEPLOY_SSH_KEY`. Gerar a chave dedicada NO VPS:
  `ssh-keygen -t ed25519 -f ~/.ssh/gh_deploy -N "" && cat ~/.ssh/gh_deploy.pub
  >> ~/.ssh/authorized_keys && cat ~/.ssh/gh_deploy` — o conteúdo privado
  impresso vai no secret. Sem os secrets o job de deploy pula com aviso, o CI
  continua normal.
- **Corrigido — idempotência no pagamento de crediário**: duplo clique/retry
  em `POST /api/crediarios/{id}/pagamento` podia debitar duas vezes. Agora o
  frontend manda `IdempotencyKey` (GUID por tentativa — vida do modal de
  pagamento), gravada em `PagamentoCrediario` com índice único filtrado
  (migration `AddPagamentoIdempotencyKey`). Replay devolve 200 com o estado
  atual (checado antes da validação de "já quitado", pra retry de quitação não
  virar 400); corrida entre retries simultâneos cai no índice único (23505) e
  devolve o estado gravado. Coberto por 3 testes novos em
  `CrediariosControllerTests` contra Postgres real — suíte completa 195/195.

- **Criado — testes de isolamento multi-tenant** (`TenantIsolationTests`, 7
  testes): exercitam o `TenantConnectionInterceptor` REAL de produção contra
  Postgres — dado do tenant A invisível pro B, troca de tenant no mesmo escopo,
  allowlist de nome de schema, e a rede de segurança do `current_schema()`.
  O teste da rede de segurança **achou bug real**: com schema inexistente,
  `current_schema()` retorna NULL SQL e o cast direto `(string?)` estourava
  `InvalidCastException` antes da mensagem de diagnóstico — corrigido com
  `as string` (isolamento nunca esteve em risco, só o erro era obscuro).
- **Feito — primeira fatia da decomposição do financeiro**: `financeiro/page.tsx`
  caiu de 2198 → 913 linhas. Extraídos pra `components/admin/financeiro/`:
  `CurvaABCSection` (449 l), `FinanceiroCharts` (BarChart/DayPieChart/
  MargemDonut/DateQuickFilter, 361 l), `FormasPagamentoSection` (197 l),
  `KpiChartModal` (138 l), `DayDetailModal` (133 l) e `financeiro-shared`
  (fmt/FORMA_LABELS/Preset/getRange). Zero mudança de comportamento — bundle
  idêntico (22 kB), typecheck e build verdes. Faltam as próximas fatias:
  comanda (104 KB), venda-avulsa (87 KB), estoque (71 KB), usuarios (61 KB).

## Backlog — pendências das avaliações externas de 14/07
Em ordem de prioridade sugerida pelas avaliações, já descontado o que foi feito:
- **Decompor os 5 maiores arquivos do frontend** — `financeiro/page.tsx` já
  feito (113 KB / 2198 linhas → 913 linhas, 6 componentes extraídos pra
  `components/admin/financeiro/`, sem mudança de comportamento). Faltam:
  comanda (104 KB), venda-avulsa (87 KB), estoque (71 KB), usuarios (61 KB) +
  landing `app/page.tsx` (48 KB). Sobrepõe com o "retrabalho de UI/UX" abaixo.
- **Bug de hidratação React (#425/#418/#423)** — pré-existente, sistêmico no
  admin; suspeita: script inline de FOUC. Parcialmente atacado no commit
  `2364296`, mas as avaliações ainda o listam — reverificar se persiste.
- **Upgrade Next 16** — elimina as 2 vulns npm restantes; major com breaking
  changes (App Router). Fazer com calma, não junto de outras mudanças.
- **Testes de integração** — feito pro multi-tenancy: `TenantIsolationTests`
  usa o `TenantConnectionInterceptor` real (não mock) contra Postgres real,
  cobrindo isolamento entre schemas, troca de tenant no mesmo escopo,
  rejeição de nome de schema inválido e a rede de segurança do
  `current_schema()` — achou e corrigiu de quebra um bug real (`InvalidCastException`
  em vez de erro claro quando o schema não existe, por causa de cast direto pra
  `DBNull`). Falta ainda: `WebApplicationFactory` end-to-end pra outros fluxos
  (não só multi-tenancy).
- **SSL Cloudflare "Flexible" → "Full (Strict)"** — hoje Cloudflare→VPS trafega
  HTTP puro. Requer cert no nginx do VPS (origin certificate da Cloudflare é o
  caminho barato). Ação de infra, não de código.
- **Monitoring básico** — UptimeRobot (ou similar) no `/health`; hoje se a API
  cair ninguém fica sabendo.
- **Zero-downtime deploy** — claim da avaliação estava exagerado: `update.sh`
  já faz `build` antes do `up -d`, então o downtime é só a recriação dos
  containers (segundos). Blue-green de verdade só vale quando houver tráfego
  que justifique.
- **`.pptx` fora do repo** (5 arquivos, >1 MB) e, se o repo for publicado um
  dia, `git filter-repo`/BFG pra limpar senha/IP do histórico.
- **Coverage (Coverlet) + testes Playwright** — Playwright está configurado
  mas sem nenhum teste escrito; mínimo: login, abrir comanda, fechar comanda.

## Backlog — configuração fiscal por tenant (motores de cálculo de tributos)
Proposta nova da `avaliacao_completa_2esysten.md` (a única seção que difere da
outra avaliação). Hoje a emissão de NFC-e já existe (Zeus/DFe.NET no
`NfceEmissionService`), mas o **cálculo de tributos** é fixo (Simples Nacional,
PIS/COFINS CST 99) e igual pra todo tenant:
- Campo `FiscalMode` (`Online` | `Offline` | `Hybrid`) no `FiscalConfig` do
  tenant, com escolha de motor de cálculo por loja.
- Candidatos avaliados na análise: MotorTributarioNet (cálculo completo,
  multi-UF), Fiscal.Net, API pública do IBPT (alíquotas por NCM — precisa de
  cache), Focus NFe (emissão em escala, pago), ACBrNCM (lookup offline).
- Chaves de API por tenant (`IbptApiKey` etc.) criptografadas — o mecanismo
  AES-256-GCM com `ENCRYPTION_KEY` já existe e é usado pra certificado/Inter.
- Frontend expõe só as opções permitidas ao admin da loja.
- Escopo a decidir antes de implementar: quais motores entram no MVP e se
  isso vira módulo de billing (como Fiscal/Estoque já são).

## Concluído (sessão 2026-07-12, achados da análise técnica externa)
- Documento externo (`analise_tecnica.md`, feito pelo usuário com Gemini/outra
  ferramenta) apontou vários riscos — verificados um a um contra o código
  atual antes de agir (vários já estavam mitigados ou desatualizados: SQL
  injection no search_path já tem allowlist, refresh token já é hasheado,
  secret do JWT já está fora do Docker build, o vazamento de SignalR descrito
  não existe do jeito relatado).
- **Corrigido** (commit `06b921d`): `ComandaHub.AdminGroup` era uma constante
  ÚNICA compartilhada por todos os tenants — todo admin conectado recebia
  atualizações de comanda de TODAS as lojas, não só a própria. Virou
  `GetAdminGroup(tenantId)`.
- **Corrigido** (commit `06b921d`): em `ComandaService.ResolveItemAsync`, uma
  linha resalvava `product.StockQuantity` na entidade rastreada depois do
  decremento atômico (que já era seguro) — o próximo `SaveChangesAsync` da
  mesma requisição sobrescrevia esse valor sem trava, apagando silenciosamente
  o decremento de qualquer venda concorrente do mesmo produto. Removida.
- **Verificado, não era bug** — fechamento de comanda (`CloseComandaAsync`) já
  é atômico: um `SaveChangesAsync` só, todas as mutações (status, crediário,
  pontos) em cima de entidades rastreadas antes dele.
- **Corrigido** (commit `2c61ff0`), achado ao verificar o item acima:
  `CancelComandaAsync` restaura estoque via `ExecuteUpdateAsync` por item
  (gravado na hora) ANTES do `SaveChangesAsync` que marca a comanda como
  Cancelada — sem transação, se esse save falhasse depois do estoque já
  restaurado, um retry do cancelamento restauraria o mesmo estoque de novo
  (a guarda de "já cancelada" não pegava esse estado intermediário). Envolvido
  numa transação explícita.
- **Testado ao vivo e confirmado** (madrugada de 2026-07-12): seed de cliente
  de teste rodado em `loja-final`/`loja-teste3`, comanda aberta numa loja não
  apareceu na outra — vazamento do `AdminGroup` confirmado corrigido.
- **Corrigido** (commit `45fe05b`), achado testando o fix do `CancelComandaAsync`
  ao vivo: `AppDbContext` usa `EnableRetryOnFailure(5)`, e uma transação manual
  solta (`BeginTransactionAsync` sem `CreateExecutionStrategy()`) não é
  suportada com execution strategy que faz retry — o EF lança
  `InvalidOperationException` dentro do `SaveChangesAsync`, quebrando o
  cancelamento com 500. Corrigido ali e em mais 2 casos pré-existentes com o
  mesmo bug latente (nunca exercitados em produção) em
  `FiscalController.CreateNatureza`/`UpdateNatureza`.
- **Corrigido** (commit `b655e34`): lock de concorrência na criação de tenant —
  `SemaphoreSlim` em memória (proporcional à app rodar como instância única;
  precisaria de lock distribuído de verdade só se um dia virar multi-réplica).
- **Corrigido** (commit `11ef5e3`): rate limit dedicado (30/min por IP) nas
  conexões do `ComandaHub` — só conta negotiate/upgrade, não mensagens de uma
  conexão já estabelecida.
- **Corrigido** (commit `677c0a1`): página dedicada de "loja suspensa" — antes
  a casca do frontend carregava vazia (sem produtos/config) em vez de avisar
  claramente. Reaproveitou o fetch de `SiteConfigContext` já existente (que
  silenciava todo erro) em vez de endpoint+middleware novos.
- **Corrigido** (commit `0c2d42b`): VLibras escondido no mobile via media
  query — mecanismo de toggle já existia, só faltava isso.
- **Corrigido** (commit `d83cc5c`): 2 gaps do portal do contador — convite
  cego (convidar por e-mail antes de existir conta, vira `Approved` na hora
  que o contador se cadastra) e endpoint de recusar solicitação pendente
  (apaga o vínculo, não bloqueia um re-pedido futuro), mais o seletor de
  "pra qual contador" no formulário de aviso quando há mais de um aprovado.

## Backlog — achados de menor urgência (mesma análise, não corrigidos ainda)
- `Program.cs` (570 linhas) e `CrediariosController.cs` (700 linhas) — god
  classes de verdade, valeria quebrar em serviços/extension methods menores.
- Middleware do Next.js (`middleware.ts`) não valida tenant do JWT vs
  subdomínio sozinho — mitigado hoje pelo `TenantClaimGuardMiddleware` no
  backend, mas seria mais robusto ter as duas camadas.

## Backlog — diretório de lojas + personalização por tenant
- Pedido original (ainda não implementado): no site institucional principal,
  uma seção/página listando os tenants ativos com link direto pra
  landing-page de cada um (uma espécie de redirecionador — "veja as lojas que
  usam a plataforma").
- Personalização por tenant do próprio visual do sistema deles: ícone do PWA,
  favicon do site institucional da loja, e o ícone que aparece no admin —
  hoje só existe personalização de nome/textos/cores via `SiteConfig`
  (`frontend/contexts/SiteConfigContext.tsx`), não de ícones/imagens.
- Escopo a decidir: upload de imagem por tenant (onde fica armazenado — hoje
  não há serviço de blob storage no projeto, só `uploadProfileImage` local
  pra perfil de usuário) e se o ícone do PWA precisa de manifest.json gerado
  dinamicamente por tenant (hoje é estático).

## Concluído (sessão 2026-07-11/12, melhorias do portal do contador)
- Polling de 20s na lista de solicitações pendentes em `/admin/fiscal` (só
  enquanto a página está aberta) — antes só carregava uma vez no mount, então
  o lojista não via um pedido novo sem F5.
- Lembrete visual de vencimento do DAS (dia 20) pra lojas no Simples Nacional
  — puramente informativo, não calcula valor nem guarda "pago/não pago".
- Resumo do período (faturamento autorizado, nº de notas, valor cancelado) no
  drill-down do contador, calculado a partir dos dados já buscados.
- Badges de saúde na lista de clientes do contador: certificado A1 vencendo
  (usa `FiscalConfig.CertificadoValidade`, já existia) e "sem nota há Xd"/
  "nenhuma nota emitida ainda".
- Mural de avisos simples (`ContadorAviso`, catálogo/schema `public`, preso a
  um `ContadorTenantLink`) — contador e lojista trocam recados curtos.
  Endpoints reaproveitam as mesmas guardas de isolamento de `convidar`/`aprovar`.
  Commit `32882a2`.
- **Gap encontrado testando**: quando uma loja tem **mais de um contador
  Approved** ao mesmo tempo (ex: troca de escritório em andamento), o backend
  de `POST /api/fiscal/contador/avisos` exige `linkId` no corpo pra saber pra
  qual contador é o recado — mas o formulário do lojista não tem seletor
  nenhum, só um campo de texto. Hoje isso só dá erro nesse cenário raro; falta
  um dropdown de "pra qual contador" quando há mais de um vinculado.

## Concluído (sessão 2026-07-11/12, portal do contador — versão cross-tenant)
- **Substitui por completo** a primeira versão (commit `dfa7d5f`, Contador como
  `User` dentro do schema de UMA loja) por uma versão cross-tenant de verdade:
  `ContadorAccount` + `ContadorTenantLink` (N:N) vivem no catálogo
  (`CatalogDbContext`, schema `public`), mesmo andar arquitetural do
  `PlatformOwner`/`Tenant`. Um contador loga uma vez pelo domínio raiz e vê só
  os clientes (lojas) vinculados e aprovados.
- Dois fluxos de vínculo: lojista convida por e-mail em `/admin/fiscal`
  (`Approved` na hora — exige que o contador já tenha se cadastrado antes) ou o
  contador se cadastra sozinho em `/contador/cadastro` e solicita acesso por
  slug (`Pending` até o lojista aprovar). Commit `89b54c8`.
- Ponto de maior risco (isolamento entre tenants) revisado com cuidado extra:
  `ContadorPortalController.AutorizarEObterTenantAsync` exige um
  `ContadorTenantLink` `Approved` (consultado sempre contra o catálogo,
  schema `public`, nunca afetado pela troca de tenant) antes de trocar o
  `ITenantContext` e servir dado de qualquer loja — confirmado que não tem
  jeito de pedir dado de um tenant sem vínculo aprovado.
- **Gaps conhecidos** (decisão consciente do fork, revisar depois se for
  problema real):
  - Convite por e-mail só funciona se o contador **já** tem conta — não tem
    "convite cego" (pré-criar vínculo antes de existir a conta); precisaria de
    uma tabela de convite pendente separada.
  - Só existe endpoint de **aprovar** solicitação, não de rejeitar/recusar.

## Concluído (sessão 2026-07-09/10)
- Branding genericizado: nome/e-mail/endereço/logo da loja vêm de `SiteConfig` dinâmico
  em vez de string fixa "softNerd"/"Santuário Nerd" — backend (`EmailService`,
  `LgpdController` etc.) e frontend (`contexts/SiteConfigContext.tsx`, `useSiteConfig()`).
- Domínio próprio configurado: `2esysten.com.br` na Cloudflare, apontando pro VPS do
  Tenant-ERP (`179.197.67.64`), com registro wildcard (`*.2esysten.com.br`) já pronto
  pra quando o roteamento por subdomínio do multi-tenant estiver no ar.
- Domínio do Maikon (`santuarionerd.tech`) migrado da conta Cloudflare do usuário pra
  conta própria do Maikon (estava incorretamente numa conta que não era dele).
- **Bug de login corrigido (2 causas empilhadas):**
  1. `setup.sh` detectava o IP público via `curl ifconfig.me` sem forçar IPv4 — em VPS
     com IPv6 configurado, gravava um endereço IPv6 inacessível em `APP_URL`, quebrando
     a URL da API embutida no build do frontend e o `JwtSettings:Issuer`. Login falhava
     silenciosamente, sem nenhuma requisição de rede sequer aparecer.
  2. `AuthController.SetAuthCookies` nunca lia a variável `COOKIE_SECURE` — calculava
     `Secure` só por `!_env.IsDevelopment()`, sempre `true` em produção. Login retornava
     200 e setava o cookie, mas o navegador descartava por ser `Secure` numa origem HTTP
     pura (sem domínio/HTTPS ainda) — parecia "deslogar sozinho".

## Concluído (sessão 2026-07-10/11)
- Multi-tenant (isolamento por schema no Postgres + resolução por subdomínio +
  remoção do MongoDB + squash de migrations) — mergeado pelo Ultraplan.
- Refatoramento visual/estrutural do painel admin: `PageHeader`/`StatCard`
  compartilhados, cor do tenant (`SiteConfig.colorPrimary`) agora propaga pro admin
  inteiro via CSS vars, e split do antigo `dashboard/page.tsx` (2610 linhas, 4
  responsabilidades misturadas) em `/admin/comanda` (comanda ao vivo, SignalR) e
  `/admin/dashboard` (Painel Geral, analytics) — ver commits `595976e`..`b94a785`.

## Concluído (sessão 2026-07-11)
- Painel do dono da plataforma (`/plataforma`): listar/cadastrar/suspender-reativar
  tenants. Cadastrar provisiona o schema Postgres novo, roda as migrations
  (`InitialSquash`) nele e já cria o admin inicial da loja — tudo síncrono no mesmo
  request. Role `PlatformOwner` + policy dedicada; login do dono reusa a tela normal
  de `/login`, só muda o redirect. Seed do primeiro dono da plataforma é automático
  no boot via `PLATFORM_OWNER_EMAIL`/`PLATFORM_OWNER_SEED_PASSWORD` no `.env` (mesmo
  padrão do seed do admin) — commits `0998437`..`fbaf89d`.
  Só gestão de tenant, sem billing (ver item de cobrança abaixo).
- **Bug real de isolamento corrigido, encontrado testando o provisionamento pela
  primeira vez**: o `search_path` era setado como `"<schema>", public` (fallback
  pro public). Como `public` é o schema de dados de verdade do tenant-zero (não um
  schema neutro de extensões), qualquer tabela ainda ausente no schema recém-criado
  — inclusive a própria `__EFMigrationsHistory`, antes da primeira migration rodar —
  resolvia silenciosamente pra `public` via busca de nome do Postgres. O EF achava
  "já migrado" e nunca criava nada no schema novo; o admin inicial da loja caía em
  `public.users` em vez do schema isolado. Corrigido em duas partes: (1) removido o
  fallback do search_path (só o schema do tenant), (2) `MigrationsHistoryTable`
  configurado com o schema explícito do tenant atual (a checagem interna do
  provider Npgsql pra saber se a tabela de histórico existe não era scoped pelo
  search_path da mesma forma que a leitura real, causando um segundo mismatch depois
  do fix nº1). Commits `276fb88`, `ffce231`. Validado em produção: tenant de teste
  isolado corretamente, só com seu próprio admin, sem vazar pra `public`.
- **Ressalva confirmada ao testar suspensão**: suspender um tenant bloqueia as
  chamadas de API (`/api/*` retorna 403 — validado com `product`, `announcements`,
  `site-config` todos bloqueados), mas a casca estática do frontend (HTML/JS do
  Next.js) continua carregando normalmente, porque o `TenantResolutionMiddleware`
  que checa `TenantStatus` vive só no backend (.NET) — o container do frontend não
  passa por ele. Resultado: o visitante vê a página carregar mas sem nenhum dado
  (produtos/config vazios por causa dos 403), em vez de uma tela clara de "loja
  suspensa". Melhoria futura: página dedicada de "loja suspensa" no frontend
  (checagem via alguma rota leve tipo `/api/tenant-status` antes de renderizar o
  resto), ou aceitar o comportamento atual como suficiente por ora.

## Concluído (sessão 2026-07-11, continuação)
- Billing ciclo 1: `Tenant` ganha `PlanName`/`PaymentStatus`
  (Pago/Atrasado/Isento)/`EnabledModules` no catálogo. Só o módulo **Fiscal**
  entra no gate técnico (`RequireModuleAttribute` no `FiscalController`, 403 se
  desabilitado; defesa em profundidade em `ComandaService`/`VendaAvulsaService`
  ignorando a flag de emissão se o módulo estiver desligado). Painel
  `/plataforma` ganhou edição de plano/pagamento/módulos por tenant. Sem gateway
  de pagamento — só rastreio manual, por decisão explícita.
- **Bug real de vazamento de tema corrigido**: o toggle claro/escuro do painel
  admin salva a preferência (classe `light` no `<html>`) em localStorage, que
  cascateia pra QUALQUER página do site, não só `/admin`. Os overrides
  `!important` de tema claro (classes Tailwind cruas E as variáveis CSS
  `--bg-card`/`--text-primary`/etc.) vazavam pra páginas com esquema de cor
  próprio (institucional, `/plataforma`) — texto branco sobre fundo virando
  branco também, ficando invisível. Corrigido escopando tudo numa classe nova
  `.admin-shell` (wrapper que envolve Sidebar + conteúdo no layout do admin,
  não só o `<main>`) — commits `ff71519` (primeira tentativa, incompleta),
  `00a3492` (fix completo).
- **Gap fechado**: tela de "esqueci minha senha" já existia pronta
  (`/reset-password`, backend com `forgot-password`/`reset-password` em
  `AuthController.cs`), mas só era alcançável a partir do login do cliente
  (`/entrar`) — o login do admin (`/login`) não linkava pra ela. Adicionado o
  link, com `?from=admin` pra voltar pro lugar certo depois do reset —
  commit `1b7d41b`.

## Concluído (sessão 2026-07-11, billing ciclo 2 — módulo Estoque)
- CRUD básico de produto/categoria e a venda em si (PDV, Comanda, vitrine
  pública) continuam **sempre livres** — travar isso quebraria a loja pra quem
  não pagasse (confirmado na exploração do ciclo 1). Só as features avançadas
  entraram no gate: pré-venda/lista de espera (`ProductWaitListController`/
  `ReservationController`, só nas ações admin — as de cliente continuam sempre
  livres), patrimônio + Curva ABC (dashboard/financeiro, gate 100% frontend, sem
  endpoint próprio pra cortar), relatórios PDF de estoque, e variantes de
  produto (só criar/editar/remover grade — a leitura na hora da venda continua
  sempre livre, senão quebraria o PDV pra produto com grade já configurada).
  Commit `bd641fe`.
- **2 bugs pré-existentes corrigidos** (achados na exploração, sem relação com
  billing, corrigidos antes de continuar): `GET /api/products/{id}/variants`
  exigia `AdminOnly`, bloqueando o autoatendimento do cliente ao escolher
  variante — virou `AllowAnonymous`. A tela `/admin/comanda` não suportava
  lançar produto com grade (`hasVariants`) — agora usa o mesmo `VariantPicker`
  já usado na Frente de Caixa.

## Backlog — billing ciclo 2 (gateway de pagamento)
- Integrar Inter (já usado no projeto pra Pix) e/ou Mercado Pago pra cobrança
  recorrente de verdade, e suspensão automática por inadimplência (hoje é
  manual, pelo painel `/plataforma`).
- Página pública de planos/preços e self-signup de tenant com pagamento (hoje
  só o dono da plataforma cadastra manualmente via `/plataforma`).

## Em andamento
- Nada em execução no momento — painel de tenants e billing ciclo 1 testados e
  validados (isolamento, suspender/reativar; billing ainda falta testar em
  produção depois do deploy).

## Bug conhecido, não corrigido
- Hidratação React (erros minificados #425/#418/#423 no console) aparece em toda
  navegação do painel admin, inclusive em páginas não tocadas nesta sessão (ex.
  `/admin/estoque`) — não é regressão do split do dashboard, é pré-existente e
  sistêmico. Suspeita: o script inline de FOUC (tema claro/escuro + cor do tenant)
  em `app/admin/layout.tsx` renderiza diferente no server vs. no primeiro paint do
  client. Precisa investigar isolado.
- VLibras (widget de Libras do governo, `vlibras.gov.br/app/vlibras-plugin.js`,
  embutido em `app/layout.tsx`) não é usável em mobile — limitação conhecida do
  próprio plugin oficial, não é bug nosso. Considerar esconder via CSS em telas
  pequenas (`components/VLibrasController.tsx` já tem o mecanismo de toggle, só
  falta a media query) já que hoje ele atrapalha mais do que ajuda no celular.

## Backlog — cobrança da plataforma (dono do SaaS)
- Sistema de cobrança para os tenants: o dono do Tenant-ERP (nós) cobra as lojas que
  usam a plataforma — provavelmente planos/assinatura + integração de pagamento
  recorrente + painel de faturamento para o super-admin da plataforma (diferente do
  admin de cada loja/tenant).
- Escopo ainda não detalhado: definir com o usuário modelo de cobrança (mensalidade
  fixa, por uso, por número de tenants/usuários), gateway de pagamento, e se entra
  antes ou depois do multi-tenant estar pronto.

## Backlog — retrabalho de UI/UX
- Revisão completa de interface e experiência do usuário do sistema.
- Escopo ainda não detalhado: quais telas (painel admin, área do cliente, ambos?),
  prioridade, se é redesign visual ou também reestruturação de fluxos — confirmar
  com o usuário antes de começar.
- Fase 5 do refatoramento do admin (adiada de propósito): responsividade mobile das
  6 telas de maior tráfego, as ~19 páginas de cauda longa que não entraram neste
  ciclo, migração de cores hardcoded (`PAY_COLORS`/`ABC_COLORS`/`#7C3AED`) pra tokens
  do tema.

## Backlog — assistente IA (Gemini) por tenant
- Hoje `GeminiChatService` usa UMA `GEMINI_API_KEY` global (env var única do app) pra
  TODOS os tenants, e `AiChatWidget` é montado incondicionalmente em
  `app/admin/layout.tsx` (todo admin de toda loja tem acesso, sem toggle por tenant
  nem contagem de uso). Isso não escala: é admin-only (não é exposto a clientes
  finais da loja), mas o free tier do Gemini é por chave de API, compartilhado entre
  todos os tenants — quanto mais lojas usarem, mais rápido estoura o limite grátis,
  e uma loja "barulhenta" pode consumir a cota de todas as outras.
- Antes de lançar pra mais tenants: (1) decidir se IA é feature de plano pago
  (amarra no sistema de cobrança da plataforma, ver item acima), (2) colocar
  rate-limit por tenant no `AiChatController` (já tem `[EnableRateLimiting("api")]`
  global, falta segmentar por tenant), (3) considerar migrar pra tier pago do Gemini
  com uso repassado no billing, ou usar chave própria por tenant se ele já tiver uma.

## Backlog — dados de teste / seed
- Gerar dados de teste (produtos, clientes, comandas, vendas) pra tenants novos
  explorarem o sistema sem estar vazio, e pra facilitar teste manual de fluxos
  (hoje o tenant de teste em `2esysten.com.br` não tem nenhum cliente cadastrado,
  o que impediu testar o fluxo completo de abrir/fechar comanda no navegador).
- Escopo a decidir: botão "gerar dados de exemplo" no onboarding do tenant, um
  script/seed de dev, ou os dois.

## Backlog — domínio próprio por tenant (BYO domain)
- Hoje só funciona subdomínio de `2esysten.com.br` (`loja.2esysten.com.br`) —
  `TenantResolutionMiddleware` só extrai slug de um subdomínio do `RootDomain`
  configurado, e o certificado SSL (Cloudflare Universal SSL) só cobre
  `2esysten.com.br`/`*.2esysten.com.br`. Um domínio de terceiro apontando pra nossa
  VPS hoje cairia em tenant-zero (sem match) e sem certificado válido.
- Pra suportar domínio próprio do lojista: campo `CustomDomain` no `Tenant`
  (nullable, único), `TenantResolutionMiddleware` passa a checar também por esse
  campo além do slug, e — o pedaço difícil — automação de certificado TLS por
  domínio novo (Let's Encrypt automático via HTTP-01/DNS-01 a cada domínio
  cadastrado, ou produto tipo Cloudflare for SaaS). Esforço bem maior que o resto
  do painel de tenants; não fazer de forma apressada.
- Fluxo esperado: lojista aponta o domínio dele (CNAME/A) pra nossa VPS, cadastra o
  domínio no painel, sistema verifica propagação de DNS e emite o certificado
  automaticamente antes de ativar.

## Concluído (sessão 2026-07-12, motor financeiro mais robusto)
- Fechamento formal de dia/semana/mês (`FechamentoPeriodo`, snapshot congelado
  por tenant) — job `FechamentoBackgroundService` fecha sozinho todo dia às
  00:10 BR (dia), toda segunda (semana) e todo dia 1 (mês); endpoint manual
  `POST /api/analytics/fechamentos/fechar-agora` serve de backfill e de
  "reabrir" (upsert). Commits `4d2fc83`, `73123a9`.
- Comparação período-a-período generalizada pra todos os presets (antes só
  existia pra "mês") — prefere o snapshot congelado quando existe, cai pro
  cálculo ao vivo quando o período ainda não foi fechado. Commit `4a2e2f9`.
- Previsão ponderada por dia da semana (histórico de fechamentos `Dia`),
  substituindo a projeção linear flat que vivia duplicada e inconsistente
  entre dashboard e financeiro — fonte única na API agora. Commit `87e0ce0`.
- Curva ABC e o layout do DRE ficaram de fora de propósito (fora de escopo,
  não pediam mudança).

## Backlog — migração de dados (import/export)
- Aceitar importação de dados de outros sistemas na hora do tenant migrar pro
  Tenant-ERP (produtos, clientes, saldo de crediário pelo menos).
- Permitir exportação/geração de migração pro tenant que quiser sair da plataforma
  — reduz o medo de lock-in na hora de fechar venda com lojista novo.
- Escopo a decidir: formato de import/export (CSV/Excel padronizado vs. adaptado por
  sistema de origem conhecido), se é self-service no painel ou processo assistido
  por nós no onboarding/offboarding.
