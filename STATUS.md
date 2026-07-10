# Status — Tenant-ERP (2026-07-10, madrugada)

## Onde paramos

Branding genericizado e dois bugs de login corrigidos (IPv4 no `setup.sh`, cookie
`Secure` no `AuthController`). VPS de teste (`179.197.67.64`) rodando com login
funcional (`admin@tenant-erp.local` / `SenhaForte@123` — trocar assim que possível,
é senha padrão visível no código-fonte público).

DNS configurado pra multi-tenant:
- `2esysten.com.br` → Cloudflare (conta do usuário), registros A + wildcard
  (`*.2esysten.com.br`) apontando pro VPS `179.197.67.64`, SSL/TLS em modo
  "Flexible" (origem só tem porta 80/HTTP aberta).
- Domínio do Maikon (`santuarionerd.tech`) migrado pra Cloudflare própria dele
  (estava incorretamente na conta do usuário) — assunto à parte, não é do Tenant-ERP.

Tentativa de implementar o multi-tenant via Ultraplan (sessão remota) **travou**
("Plan flow interrupted") e está perto do limite de uso da sessão — decidido fazer
a implementação manualmente aqui na próxima sessão, em vez de depender dela.

## Próxima sessão — começar por aqui

Seguir o plano já desenhado (arquitetura discutida em detalhe, ver `BACKLOG.md` e
histórico da conversa), na ordem:

1. **Fase 0a** — eliminar o MongoDB, migrar `VendaAvulsa` pro Postgres (coluna JSONB
   pros itens, mesmo padrão de `Crediario.ItensJson`).
2. **Fase 0b** — squash das migrations (as atuais foram geradas contra SQLite,
   inutilizáveis contra Postgres real — descartar e reger).
3. Catálogo de tenants (`Tenant`, `CatalogDbContext`, sem schema ainda).
4. Interceptor de conexão (`SET search_path`) + middleware de resolução por
   subdomínio, com tenant-zero fixo primeiro (sem mover dado ainda).
5. Fix dos background services (`FiscalAlertBackgroundService` etc.) e `DbHealthCheck`
   pra não quebrar quando o interceptor exigir `ITenantContext`.
6. Migração de dados do tenant-zero pra schema dedicado.
7. Claim `tenant_id` no JWT + guard middleware + CORS wildcard.
8. nginx `server_name` pra aceitar `*.2esysten.com.br` (DNS já pronto).

## Backlog fora do multi-tenant
Ver `BACKLOG.md` — cobrança da plataforma (dono do SaaS cobra os tenants) e
retrabalho completo de UI/UX, ambos ainda sem escopo detalhado.
