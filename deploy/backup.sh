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
#   BACKUP_REMOTE_CMD   (opcional) comando de cópia off-site. O caminho do arquivo
#                       é passado como último argumento, uma vez por dump. Ex.:
#                         BACKUP_REMOTE_CMD="rclone copy --drive-shared-with-me"
#                       ⚠️  Backup só na própria VPS não protege contra perda do
#                       disco/instância. Configure isto (ou uma cópia off-site
#                       equivalente) assim que houver dado de loja que doa perder.
#                       Se definido e o envio falhar, este script FALHA (exit 1) —
#                       ver comentário na seção de envio.
#   BACKUP_ENCRYPT_PASSPHRASE
#                       (opcional, recomendado) cifra cada dump com GPG/AES-256
#                       ANTES de sair do servidor. Só o arquivo cifrado é enviado;
#                       a cópia local segue em texto puro para restauração rápida.
#                       ⚠️  Guarde a frase-secreta FORA do destino off-site. Ela no
#                       mesmo Drive do backup não protege de nada, e perdê-la
#                       significa perder todos os backups enviados.
#   POSTGRES_DB / POSTGRES_USER — lidos do .env
#
# RESTAURAÇÃO: ver deploy/BACKUP.md
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

# Configuração do off-site também sai do .env, e não só do ambiente.
#
# O cron instalado pelo setup.sh é `cd /opt/tenant-erp && bash deploy/backup.sh`,
# sem `export` nenhum: cron roda com ambiente praticamente vazio. Se estas duas
# só viessem do ambiente, o cenário seria — configura, testa na mão com as
# variáveis exportadas, funciona, e às 03:00 o backup roda todo dia SEM enviar
# nada, sem erro, porque o `if` de envio simplesmente não dispara. Silencioso é
# o pior modo de falhar num backup.
#
# Guardar no .env também evita a frase-secreta no crontab, que é legível por
# `crontab -l` e fica em texto puro em /var/spool/cron.
if [ -z "${BACKUP_REMOTE_CMD:-}" ]; then
  BACKUP_REMOTE_CMD=$(env_get BACKUP_REMOTE_CMD "$PROJECT_DIR/.env")
fi
if [ -z "${BACKUP_ENCRYPT_PASSPHRASE:-}" ]; then
  BACKUP_ENCRYPT_PASSPHRASE=$(env_get BACKUP_ENCRYPT_PASSPHRASE "$PROJECT_DIR/.env")
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

# Tudo que precisa sair daqui. A lista existe porque o envio off-site mandava
# só o dump do ERP: o banco da Evolution ficava exclusivamente no disco do VPS,
# que é justamente o disco do qual o off-site deveria proteger.
DUMPS=("$PG_FILE")

# ── Banco da Evolution API (WhatsApp), se existir ──────────────────────────────
# Fica num banco separado do ERP, então o pg_dump acima NÃO o cobre. É aqui que
# moram as credenciais de sessão do WhatsApp de cada tenant: perder este banco
# significa que todo tenant premium precisa reler o QR Code do zero — ou seja,
# ligar pra cada cliente pedindo pra escanear de novo.
# Só roda se o banco existir (a feature é opcional, atrás do profile "whatsapp"),
# então instalações sem WhatsApp seguem sem nenhuma mudança de comportamento.
EVOLUTION_DB="${EVOLUTION_DB:-evolution}"
if docker exec cardgamestore_postgres \
     psql -U "$POSTGRES_USER" -lqt 2>/dev/null | cut -d'|' -f1 | grep -qw "$EVOLUTION_DB"; then

  EVO_FILE="$BACKUP_DIR/evolution_${TIMESTAMP}.sql.gz"
  echo "[$(date '+%H:%M:%S')] Evolution (WhatsApp) → $EVO_FILE"

  docker exec cardgamestore_postgres \
    pg_dump -U "$POSTGRES_USER" "$EVOLUTION_DB" \
    | gzip > "$EVO_FILE"

  # Mesma checagem de integridade do dump principal — um backup de sessões
  # corrompido só é descoberto no dia do desastre.
  if ! gzip -t "$EVO_FILE" 2>/dev/null; then
    echo "[$(date '+%H:%M:%S')] ❌ ERRO: $EVO_FILE corrompido (gzip -t falhou) — removendo." >&2
    rm -f "$EVO_FILE"
    exit 1
  fi

  EVO_SIZE=$(du -sh "$EVO_FILE" | cut -f1)
  echo "[$(date '+%H:%M:%S')] Evolution OK ($EVO_SIZE, integridade verificada)"
  DUMPS+=("$EVO_FILE")
else
  echo "[$(date '+%H:%M:%S')] Evolution: banco '$EVOLUTION_DB' não existe — pulando (feature desligada)"
fi

# ── Cópia off-site (opcional) ──────────────────────────────────────────────────
# Sem isto, o backup vive no MESMO disco do banco — uma falha de VPS/disco leva
# banco e backup juntos. Só roda se BACKUP_REMOTE_CMD estiver definido.
#
# Uma falha aqui derruba o script inteiro (exit 1), e isso é deliberado. Antes
# era só um aviso: como este script roda por cron às 03:00 escrevendo num log
# que ninguém abre, um envio quebrado ficava meses invisível e só aparecia no
# dia em que o backup off-site fosse necessário — o pior momento possível para
# descobrir que ele não existe. Falhando, o cron passa a mandar e-mail de erro e
# o `setup.sh`/execução manual devolvem status diferente de zero.
if [ -n "${BACKUP_REMOTE_CMD:-}" ]; then
  CIFRAR="${BACKUP_ENCRYPT_PASSPHRASE:-}"

  if [ -n "$CIFRAR" ] && ! command -v gpg >/dev/null 2>&1; then
    echo "[$(date '+%H:%M:%S')] ❌ ERRO: BACKUP_ENCRYPT_PASSPHRASE definido mas 'gpg' não está instalado. Instale (apt-get install -y gnupg) ou remova a variável." >&2
    exit 1
  fi

  ENVIO_FALHOU=0
  for DUMP in "${DUMPS[@]}"; do
    ARQUIVO="$DUMP"

    if [ -n "$CIFRAR" ]; then
      ARQUIVO="${DUMP}.gpg"
      echo "[$(date '+%H:%M:%S')] Cifrando $(basename "$DUMP") ..."
      # --passphrase-fd 3 em vez de --passphrase: o valor não aparece na linha de
      # comando, que qualquer usuário do host lê via `ps`.
      if ! gpg --batch --yes --quiet --symmetric --cipher-algo AES256 \
               --passphrase-fd 3 --output "$ARQUIVO" "$DUMP" 3<<<"$CIFRAR"; then
        echo "[$(date '+%H:%M:%S')] ❌ ERRO: falha ao cifrar $DUMP — NÃO enviando em texto puro." >&2
        rm -f "$ARQUIVO"
        ENVIO_FALHOU=1
        continue
      fi
    fi

    echo "[$(date '+%H:%M:%S')] Enviando off-site: $(basename "$ARQUIVO")"
    # shellcheck disable=SC2086
    if $BACKUP_REMOTE_CMD "$ARQUIVO"; then
      echo "[$(date '+%H:%M:%S')] Enviado OK: $(basename "$ARQUIVO")"
    else
      echo "[$(date '+%H:%M:%S')] ❌ ERRO: envio off-site falhou para $(basename "$ARQUIVO")." >&2
      ENVIO_FALHOU=1
    fi

    # O cifrado é material de trânsito: o que fica no VPS é o .sql.gz em texto
    # puro, que torna a restauração local imediata. Manter os dois dobraria o
    # espaço ocupado sem proteger nada — quem alcança este disco alcança o banco.
    [ -n "$CIFRAR" ] && rm -f "$ARQUIVO"
  done

  if [ "$ENVIO_FALHOU" -ne 0 ]; then
    echo "[$(date '+%H:%M:%S')] ❌ Backup local existe, mas SEM redundância off-site. Verifique o destino." >&2
    exit 1
  fi
  echo "[$(date '+%H:%M:%S')] Cópia off-site OK (${#DUMPS[@]} arquivo(s))"
fi

# ── Limpeza de backups antigos ─────────────────────────────────────────────────
# `.gpg` também: o fluxo normal apaga o cifrado logo após o envio, mas uma queda
# no meio do laço (ou um `kill`) deixa o arquivo para trás. Sem esta extensão na
# busca, esse resto acumularia para sempre no disco.
REMOVED=$(find "$BACKUP_DIR" \( -name "*.sql.gz" -o -name "*.sql.gz.gpg" \) \
  -mtime +"$MAX_DAYS" -print -delete | wc -l)
echo "[$(date '+%H:%M:%S')] $REMOVED arquivo(s) com mais de $MAX_DAYS dias removidos"

echo "[$(date '+%Y-%m-%d %H:%M:%S')] === Backup concluído. Arquivos em: $BACKUP_DIR ==="
