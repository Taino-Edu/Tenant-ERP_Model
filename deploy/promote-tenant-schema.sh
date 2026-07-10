#!/bin/bash
# =============================================================================
# promote-tenant-schema.sh — Fase 5 do multi-tenant: move os dados do
# tenant-zero (schema "public") pra um schema dedicado, registrando o tenant
# no catálogo.
#
# QUANDO RODAR: só depois que as Fases 0-4 e 6 já estiverem em produção (schema
# ainda "public", interceptor já ativo e pinando tenant-zero == "public").
# Esta é a primeira vez que um tenant real sai do schema compartilhado.
#
# JANELA DE MANUTENÇÃO OBRIGATÓRIA:
#   O ALTER TABLE ... SET SCHEMA é só metadado (instantâneo, não copia linha
#   nenhuma) mas exige exclusividade — qualquer escrita concorrente durante a
#   transação pode falhar ou (pior) ficar presa no schema errado. Pare a API
#   antes de rodar isto:
#     docker compose -f deploy/docker-compose.prod.yml stop api
#   E só suba de novo depois que o script confirmar sucesso.
#
# USO:
#   bash deploy/promote-tenant-schema.sh <novo_schema> <slug_do_tenant>
#   Exemplo: bash deploy/promote-tenant-schema.sh loja_maikon maikon
#
# VARIÁVEIS DE AMBIENTE (lidas do .env, mesmo padrão de backup.sh):
#   POSTGRES_DB / POSTGRES_USER — lidos do .env
# =============================================================================

set -euo pipefail

if [ $# -ne 2 ]; then
  echo "Uso: bash deploy/promote-tenant-schema.sh <novo_schema> <slug_do_tenant>" >&2
  exit 1
fi

NEW_SCHEMA="$1"
TENANT_SLUG="$2"

# Nome de schema Postgres: só letras minúsculas, dígitos e underscore, não
# pode começar com dígito — mesma validação do TenantConnectionInterceptor.
if ! [[ "$NEW_SCHEMA" =~ ^[a-z_][a-z0-9_]{0,62}$ ]]; then
  echo "Nome de schema inválido: '$NEW_SCHEMA' (só a-z, 0-9, _; não pode começar com dígito; máx 63 chars)." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

if [ -f "$PROJECT_DIR/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  source "$PROJECT_DIR/.env"
  set +a
fi

POSTGRES_DB="${POSTGRES_DB:-cardgamestore}"
POSTGRES_USER="${POSTGRES_USER:-cardgame_user}"

echo "=== Fase 5: promovendo tenant-zero pro schema '$NEW_SCHEMA' (slug '$TENANT_SLUG') ==="
echo ""
echo "ATENÇÃO: confirme que a API está parada antes de continuar."
echo "  docker compose -f deploy/docker-compose.prod.yml stop api"
read -rp "API já está parada? Digite 'sim' para confirmar e continuar: " CONFIRM
if [ "$CONFIRM" != "sim" ]; then
  echo "Abortado — pare a API primeiro." >&2
  exit 1
fi

# ── 1. Backup completo antes de qualquer coisa ──────────────────────────────
BACKUP_DIR="${BACKUP_DIR:-$PROJECT_DIR/backups}"
mkdir -p "$BACKUP_DIR"
BACKUP_FILE="$BACKUP_DIR/pre_promote_${NEW_SCHEMA}_$(date +%Y%m%d_%H%M%S).sql.gz"
echo "[1/3] Backup completo → $BACKUP_FILE"
docker exec cardgamestore_postgres pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" | gzip > "$BACKUP_FILE"
echo "      OK ($(du -sh "$BACKUP_FILE" | cut -f1))"

# ── 2. Lista de tabelas do AppDbContext (schema "public") ───────────────────
# Mantida em sincronia manual com Data/Migrations/*_InitialSquash.cs — se uma
# tabela nova for adicionada ao AppDbContext depois desta fase rodar, ela já
# nasce direto no schema do tenant (não precisa entrar nesta lista).
TABLES=(
  users products product_categories comandas comanda_items announcements
  crediarios pagamentos_crediario pix_cobrancas perfis product_waitlist
  lgpd_requests cookie_consents audit_logs timers product_variants
  product_reservations external_transactions integration_configs
  notifications push_subscriptions fiscal_config naturezas_operacao
  notas_fiscais_emitidas notas_destinadas site_config vendas_avulsas
)
echo "[2/3] ${#TABLES[@]} tabelas serão movidas para '$NEW_SCHEMA'."

# ── 3. Transação única: CREATE SCHEMA + ALTER TABLE ... SET SCHEMA + catálogo
# Tudo dentro de uma transação — ou tudo aplica, ou nada (ROLLBACK automático
# se qualquer ALTER falhar, sem custo, sem estado intermediário).
SQL_FILE=$(mktemp)
{
  echo "BEGIN;"
  echo "CREATE SCHEMA \"$NEW_SCHEMA\";"
  for t in "${TABLES[@]}"; do
    echo "ALTER TABLE public.\"$t\" SET SCHEMA \"$NEW_SCHEMA\";"
  done
  # __EFMigrationsHistory do AppDbContext também precisa ir junto — o schema
  # novo precisa da própria história de migrations pra MigrateAsync() futuro
  # funcionar contra ele isoladamente.
  echo "ALTER TABLE public.\"__EFMigrationsHistory\" SET SCHEMA \"$NEW_SCHEMA\";"
  echo "INSERT INTO public.tenants (id, slug, schema_name, status, created_at)"
  echo "VALUES (gen_random_uuid(), '$TENANT_SLUG', '$NEW_SCHEMA', 'Active', now());"
  echo "COMMIT;"
} > "$SQL_FILE"

echo "[3/3] Aplicando em uma única transação..."
docker exec -i cardgamestore_postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 < "$SQL_FILE"
rm -f "$SQL_FILE"

echo ""
echo "=== Concluído. Schema '$NEW_SCHEMA' criado, tenant '$TENANT_SLUG' registrado no catálogo. ==="
echo ""
echo "PRÓXIMOS PASSOS (manuais, não automatizados por este script):"
echo "  1. Suba a API de novo: docker compose -f deploy/docker-compose.prod.yml start api"
echo "  2. Valide contra o subdomínio do tenant (login, PDV, comanda, emissão de NFC-e —"
echo "     conferir especialmente que o certificado A1 encriptado do FiscalConfig ainda"
echo "     decripta certo; SET SCHEMA não deveria afetar isso, mas é o item mais crítico"
echo "     de verificar antes de considerar a migração encerrada)."
echo "  3. Se algo estiver errado DEPOIS do commit (schema já promovido): restaurar o"
echo "     backup ($BACKUP_FILE) é o caminho — não existe 'desfazer' parcial seguro."
