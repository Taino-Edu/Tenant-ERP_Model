# Backlog — Tenant-ERP

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

## Em andamento
- Nada em execução remota no momento.

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

## Backlog — migração de dados (import/export)
- Aceitar importação de dados de outros sistemas na hora do tenant migrar pro
  Tenant-ERP (produtos, clientes, saldo de crediário pelo menos).
- Permitir exportação/geração de migração pro tenant que quiser sair da plataforma
  — reduz o medo de lock-in na hora de fechar venda com lojista novo.
- Escopo a decidir: formato de import/export (CSV/Excel padronizado vs. adaptado por
  sistema de origem conhecido), se é self-service no painel ou processo assistido
  por nós no onboarding/offboarding.
