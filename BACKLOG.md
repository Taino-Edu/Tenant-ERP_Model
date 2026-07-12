# Backlog — Tenant-ERP

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
- **Pendente de teste**: prova ao vivo do isolamento do SignalR (item acima)
  ficou faltando por não haver cliente cadastrado nos tenants de teste — SQL
  de seed preparado, falta só rodar (ver mensagem da sessão de 2026-07-12
  madrugada pro comando exato).

## Backlog — achados de menor urgência (mesma análise, não corrigidos ainda)
- Sem lock de concorrência na criação de tenant (`TenantProvisioningService`)
  — real, mas ação rara/admin-only, baixo risco prático.
- `Program.cs` (570 linhas) e `CrediariosController.cs` (700 linhas) — god
  classes de verdade, valeria quebrar em serviços/extension methods menores.
- `ComandaHub.JoinComandaGroup`/hub sem rate limiting dedicado (a validação
  de dono da comanda já existe, mas não impede spam de conexões).
- Middleware do Next.js (`middleware.ts`) não valida tenant do JWT vs
  subdomínio sozinho — mitigado hoje pelo `TenantClaimGuardMiddleware` no
  backend, mas seria mais robusto ter as duas camadas.
- Não verificado ainda: se o fechamento de comanda (baixa de estoque + pontos
  + contas a receber) usa transação explícita ou depende só da atomicidade de
  um único `SaveChangesAsync` — precisa investigação própria antes de agir.

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
