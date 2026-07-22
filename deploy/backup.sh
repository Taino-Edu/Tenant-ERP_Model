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
#   BACKUP_REMOTE_CMD   (opcional) comando de cópia off-site. O caminho do dump é
#                       passado como último argumento. Ex.:
#                         BACKUP_REMOTE_CMD="rclone copy"        → rclone copy <arquivo> ...
#                       ⚠️  Backup só na própria VPS não protege contra perda do
#                       disco/instância. Configure isto (ou uma cópia off-site
#                       equivalente) assim que houver dado de loja que doa perder.
#   POSTGRES_DB / POSTGRES_USER — lidos do .env
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

BACKUP_DIR="${BACKUP_DIR:-$PROJECT_DIR/backups}"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
MAX_DAYS="${BACKUP_RETAIN_DAYS:-7}"

mkdir -p "$BACKUP_DIR"

# Lê só as chaves que este script precisa do .env, em vez de dar `source` no
# arquivo inteiro — o .env é escrito no formato docker-compose (não exige aspas
# em valores com espaço, ex: SMTP_FROM_NAME=Tenant ERP), mas um `source` bash
# quebra nesse mesmo caso ("ERP: command not found") porque interpreta a segunda
# palavra como um comando. Extrai só a chave pedida, tolerando aspas opcionais.
env_get() {
  local key="$1" file="$2"
  [ -f "$file" ] || return 0
  grep -E "^${key}=" "$file" | tail -1 | cut -d= -f2- | sed -E 's/^"(.*)"$/\1/; s/^'"'"'(.*)'"'"'$/\1/' || true
}

if [ -z "${POSTGRES_DB:-}" ];   then POSTGRES_DB=$(env_get POSTGRES_DB "$PROJECT_DIR/.env"); fi
if [ -z "${POSTGRES_USER:-}" ]; then POSTGRES_USER=$(env_get POSTGRES_USER "$PROJECT_DIR/.env"); fi

POSTGRES_DB="${POSTGRES_DB:-cardgamestore}"
POSTGRES_USER="${POSTGRES_USER:-cardgame_user}"

echo "[$(date '+%Y-%m-%d %H:%M:%S')] === Iniciando backup Tenant-ERP ==="

# ── PostgreSQL ─────────────────────────────────────────────────────────────────
PG_FILE="$BACKUP_DIR/postgres_${TIMESTAMP}.sql.gz"
echo "[$(date '+%H:%M:%S')] PostgreSQL → $PG_FILE"

docker exec cardgamestore_postgres \
  pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" \
  | gzip > "$PG_FILE"

# ── Verificação de integridade ─────────────────────────────────────────────────
# set -o pipefail já aborta se o pg_dump falhar no meio, mas um arquivo gzip
# truncado (disco cheio no meio da escrita) ou um dump vazio passariam batido
# sem estas duas checagens — e um backup corrompido só é descoberto na hora do
# desastre, quando já é tarde. Falhar aqui é MUITO melhor que "achar" que tem backup.
if ! gzip -t "$PG_FILE" 2>/dev/null; then
  echo "[$(date '+%H:%M:%S')] ❌ ERRO: $PG_FILE está corrompido (gzip -t falhou) — removendo." >&2
  rm -f "$PG_FILE"
  exit 1
fi

# Um dump válido tem o header do pg_dump + DDL — bem mais que alguns bytes.
# Threshold conservador (1 KB comprimido) só para pegar dump vazio/degenerado.
PG_BYTES=$(stat -c%s "$PG_FILE" 2>/dev/null || stat -f%z "$PG_FILE")
if [ "${PG_BYTES:-0}" -lt 1024 ]; then
  echo "[$(date '+%H:%M:%S')] ❌ ERRO: $PG_FILE tem só ${PG_BYTES} bytes — dump provavelmente vazio. Removendo." >&2
  rm -f "$PG_FILE"
  exit 1
fi

PG_SIZE=$(du -sh "$PG_FILE" | cut -f1)
echo "[$(date '+%H:%M:%S')] PostgreSQL OK ($PG_SIZE, integridade verificada)"

# ── Cópia off-site (opcional) ──────────────────────────────────────────────────
# Sem isto, o backup vive no MESMO disco do banco — uma falha de VPS/disco leva
# banco e backup juntos. Só roda se BACKUP_REMOTE_CMD estiver definido.
if [ -n "${BACKUP_REMOTE_CMD:-}" ]; then
  echo "[$(date '+%H:%M:%S')] Enviando off-site: $BACKUP_REMOTE_CMD ... $PG_FILE"
  # shellcheck disable=SC2086
  if $BACKUP_REMOTE_CMD "$PG_FILE"; then
    echo "[$(date '+%H:%M:%S')] Cópia off-site OK"
  else
    echo "[$(date '+%H:%M:%S')] ⚠️  Cópia off-site FALHOU — backup local existe, mas sem redundância off-site." >&2
  fi
fi

# ── Limpeza de backups antigos ─────────────────────────────────────────────────
REMOVED=$(find "$BACKUP_DIR" -name "*.sql.gz" \
  -mtime +"$MAX_DAYS" -print -delete | wc -l)
echo "[$(date '+%H:%M:%S')] $REMOVED arquivo(s) com mais de $MAX_DAYS dias removidos"

echo "[$(date '+%Y-%m-%d %H:%M:%S')] === Backup concluído. Arquivos em: $BACKUP_DIR ==="
