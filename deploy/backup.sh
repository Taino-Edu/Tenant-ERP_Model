#!/bin/bash
# =============================================================================
# backup.sh — Backup diário de PostgreSQL
#
# USO MANUAL:
#   cd /opt/tenant-erp && bash deploy/backup.sh
#
# CONFIGURAR CRON (uma vez no VPS):
#   crontab -e
#   # Backup às 03:00 todos os dias:
#   0 3 * * * cd /opt/tenant-erp && bash deploy/backup.sh >> /var/log/tenant-erp-backup.log 2>&1
#
# VARIÁVEIS DE AMBIENTE (lidas do .env ou exportadas antes de chamar):
#   BACKUP_DIR          Diretório de destino (default: /opt/tenant-erp/backups)
#   BACKUP_RETAIN_DAYS  Dias de retenção (default: 7)
#   POSTGRES_DB / POSTGRES_USER — lidos do .env
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

BACKUP_DIR="${BACKUP_DIR:-$PROJECT_DIR/backups}"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
MAX_DAYS="${BACKUP_RETAIN_DAYS:-7}"

mkdir -p "$BACKUP_DIR"

# Carrega variáveis do .env
if [ -f "$PROJECT_DIR/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  source "$PROJECT_DIR/.env"
  set +a
fi

POSTGRES_DB="${POSTGRES_DB:-cardgamestore}"
POSTGRES_USER="${POSTGRES_USER:-cardgame_user}"

echo "[$(date '+%Y-%m-%d %H:%M:%S')] === Iniciando backup Tenant-ERP ==="

# ── PostgreSQL ─────────────────────────────────────────────────────────────────
PG_FILE="$BACKUP_DIR/postgres_${TIMESTAMP}.sql.gz"
echo "[$(date '+%H:%M:%S')] PostgreSQL → $PG_FILE"

docker exec cardgamestore_postgres \
  pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" \
  | gzip > "$PG_FILE"

PG_SIZE=$(du -sh "$PG_FILE" | cut -f1)
echo "[$(date '+%H:%M:%S')] PostgreSQL OK ($PG_SIZE)"

# ── Limpeza de backups antigos ─────────────────────────────────────────────────
REMOVED=$(find "$BACKUP_DIR" -name "*.sql.gz" \
  -mtime +"$MAX_DAYS" -print -delete | wc -l)
echo "[$(date '+%H:%M:%S')] $REMOVED arquivo(s) com mais de $MAX_DAYS dias removidos"

echo "[$(date '+%Y-%m-%d %H:%M:%S')] === Backup concluído. Arquivos em: $BACKUP_DIR ==="
