#!/bin/bash
# =============================================================================
# setup-r2.sh — Liga a cópia off-site do backup no Cloudflare R2.
#
# USO (no VPS, como root):
#   cd /opt/tenant-erp && bash deploy/setup-r2.sh
#
# Antes de rodar, tenha em mãos (painel da Cloudflare → R2):
#   - Account ID
#   - Access Key ID e Secret Access Key de um token de CONTA com
#     "Object Read and Write" no bucket
#   - O bucket já criado
# O passo a passo de como obter cada um está em deploy/BACKUP.md.
#
# Por que este script existe: o procedimento manual tem dez passos e pelo menos
# três armadilhas que falham de um jeito que não parece com a causa — rclone do
# apt é velho demais e o R2 responde 401 como se a credencial estivesse errada;
# token com escopo de bucket precisa de `no_check_bucket` ou toma 403 na
# primeira escrita; e esquecer a frase-secreta fazia o dump sair legível. Aqui
# cada uma dessas vira uma verificação com mensagem própria.
#
# As credenciais são lidas do teclado, nunca por argumento: argumento aparece
# no `ps` de qualquer usuário da máquina e fica no histórico do shell.
#
# É idempotente — rodar de novo relê o que já está configurado e só refaz o que
# falta. A frase-secreta existente NUNCA é substituída (trocar significa perder
# o acesso a tudo que já foi enviado).
# =============================================================================

set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BOLD='\033[1m'
NC='\033[0m'

TOTAL=6
step() { echo -e "\n${YELLOW}${BOLD}[$1/$TOTAL] $2${NC}"; }
ok()   { echo -e "  ${GREEN}✅ $1${NC}"; }
warn() { echo -e "  ${RED}⚠️  $1${NC}"; }
die()  { echo -e "\n${RED}${BOLD}❌ $1${NC}\n" >&2; exit 1; }

APP_DIR="${APP_DIR:-/opt/tenant-erp}"
ENV_FILE="$APP_DIR/.env"
RCLONE_CONF="/root/.config/rclone/rclone.conf"
BUCKET="${BUCKET:-octus-backups}"

[ "$(id -u)" -eq 0 ] || die "Rode como root: o rclone.conf vive em /root e o cron do backup é do root."
[ -f "$ENV_FILE" ]   || die "$ENV_FILE não existe. Este script é para um VPS já provisionado pelo setup.sh."

# ── 1. rclone numa versão que o R2 aceita ────────────────────────────────────
step 1 "Verificando o rclone..."

# O apt do Ubuntu 22.04 traz o 1.53. O R2 exige 1.59+, e abaixo disso a falha é
# um HTTP 401 — indistinguível de credencial errada, que é onde se perde a tarde.
precisa_instalar=1
if command -v rclone >/dev/null 2>&1; then
    versao=$(rclone version | head -1 | grep -oE '[0-9]+\.[0-9]+' | head -1)
    maior=${versao%%.*}; menor=${versao##*.}
    if [ "$maior" -gt 1 ] || { [ "$maior" -eq 1 ] && [ "$menor" -ge 59 ]; }; then
        ok "rclone $versao (>= 1.59)"
        precisa_instalar=0
    else
        warn "rclone $versao é antigo demais para o R2 (mínimo 1.59) — reinstalando"
    fi
fi

if [ "$precisa_instalar" -eq 1 ]; then
    curl -fsSL https://rclone.org/install.sh | bash >/dev/null 2>&1 || die "Falha ao instalar o rclone."
    ok "rclone $(rclone version | head -1 | grep -oE '[0-9]+\.[0-9]+' | head -1) instalado"
fi

command -v gpg >/dev/null 2>&1 || {
    apt-get update -qq && apt-get install -y -qq gnupg >/dev/null 2>&1 || die "Falha ao instalar o gnupg."
}
ok "gpg presente (a cifra do dump depende dele)"

# ── 2. Credenciais ───────────────────────────────────────────────────────────
step 2 "Credenciais do R2"

if [ -f "$RCLONE_CONF" ] && grep -q '^\[r2\]' "$RCLONE_CONF"; then
    ok "rclone.conf já tem o remote [r2] — mantendo"
else
    echo "  Na tela do token, use a seção \"Use the S3 API\" — o \"Token value\""
    echo "  longo do topo é da API da Cloudflare e NÃO serve aqui."
    echo "  (nada é exibido na tela enquanto você digita ou cola)"
    read -rsp "  Endpoint S3 (ou só o Account ID): " ENDPOINT_RAW; echo
    read -rsp "  Access Key ID: "                   ACCESS_KEY;   echo
    read -rsp "  Secret Access Key: "               SECRET_KEY;   echo

    [ -n "$ENDPOINT_RAW" ] && [ -n "$ACCESS_KEY" ] && [ -n "$SECRET_KEY" ] || die "Credencial vazia — nada foi gravado."

    # Aceita as duas formas porque a Cloudflare mostra o endpoint pronto e pede
    # trabalho extrair o Account ID dele. E colar o endpoint é mais seguro do que
    # remontá-lo: uma conta com jurisdição (UE, por exemplo) tem host diferente
    # — `<id>.eu.r2.cloudflarestorage.com` — e remontar como `<id>.r2...`
    # produziria uma URL que não existe, falhando como se fosse credencial.
    ENDPOINT_RAW="${ENDPOINT_RAW%/}"
    case "$ENDPOINT_RAW" in
        *r2.cloudflarestorage.com*)
            ENDPOINT="https://${ENDPOINT_RAW#https://}" ;;
        *.*|*/*)
            die "Não reconheci \"${ENDPOINT_RAW:0:12}...\" como endpoint do R2 nem como Account ID.
   Esperado: https://<id>.r2.cloudflarestorage.com  ou só o <id>." ;;
        *)
            ENDPOINT="https://$ENDPOINT_RAW.r2.cloudflarestorage.com" ;;
    esac

    mkdir -p "$(dirname "$RCLONE_CONF")"
    # umask antes de escrever: o arquivo não pode existir nem por um instante
    # com permissão de leitura para outros usuários.
    ( umask 077; cat > "$RCLONE_CONF" <<EOF
[r2]
type = s3
provider = Cloudflare
access_key_id = $ACCESS_KEY
secret_access_key = $SECRET_KEY
endpoint = $ENDPOINT
region = auto
acl = private
no_check_bucket = true
EOF
    )
    unset ENDPOINT_RAW ENDPOINT ACCESS_KEY SECRET_KEY
    ok "rclone.conf gravado com permissão 600"
fi

# `no_check_bucket = true` é obrigatório com token de escopo de bucket: sem ele
# o rclone tenta verificar/criar o bucket na primeira escrita e leva 403.
grep -q '^no_check_bucket' "$RCLONE_CONF" || {
    printf 'no_check_bucket = true\n' >> "$RCLONE_CONF"
    ok "no_check_bucket adicionado (token com escopo de bucket exige)"
}

# ── 3. Round-trip de verdade ─────────────────────────────────────────────────
step 3 "Testando escrita, leitura e remoção no bucket..."

TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT
CANARIO="setup-r2-$(date +%s).txt"
echo "teste de escrita $(date -Iseconds)" > "$TMP_DIR/$CANARIO"

rclone copy --config "$RCLONE_CONF" "$TMP_DIR/$CANARIO" "r2:$BUCKET" 2>/dev/null \
    || die "Não consegui ESCREVER em r2:$BUCKET.
   401 → credencial errada, ou rclone < 1.59
   403 → o token não tem 'Object Read and Write', ou falta no_check_bucket
   Sem resposta → confira o Account ID no endpoint de $RCLONE_CONF"
ok "escrita"

rclone cat --config "$RCLONE_CONF" "r2:$BUCKET/$CANARIO" >/dev/null 2>&1 \
    || die "Escreveu mas não consegui LER de volta — o token tem escrita e não tem leitura."
ok "leitura"

rclone delete --config "$RCLONE_CONF" "r2:$BUCKET/$CANARIO" 2>/dev/null \
    && ok "remoção (arquivo de teste apagado)" \
    || warn "não consegui apagar $CANARIO — remova pelo painel; não impede o backup"

# ── 4. Frase-secreta ─────────────────────────────────────────────────────────
step 4 "Frase-secreta da cifra"

env_get() { grep -E "^$1=" "$ENV_FILE" 2>/dev/null | tail -1 | cut -d= -f2-; }
env_set() {
    # Substitui a linha existente ou acrescenta — nunca duplica a chave, porque
    # o backup.sh lê a ÚLTIMA ocorrência e duas linhas divergentes seriam um
    # bug invisível.
    if grep -qE "^$1=" "$ENV_FILE"; then
        sed -i "s|^$1=.*|$1=$2|" "$ENV_FILE"
    else
        printf '%s=%s\n' "$1" "$2" >> "$ENV_FILE"
    fi
}

FRASE_ATUAL=$(env_get BACKUP_ENCRYPT_PASSPHRASE)
if [ -n "$FRASE_ATUAL" ]; then
    ok "já existe uma frase-secreta no .env — preservada"
    echo -e "  ${YELLOW}Trocá-la tornaria ilegível tudo o que já foi enviado, então este script nunca troca.${NC}"
else
    NOVA=$(openssl rand -base64 32)
    env_set BACKUP_ENCRYPT_PASSPHRASE "$NOVA"
    ok "frase-secreta gerada e gravada no .env"
    echo
    echo -e "  ${RED}${BOLD}════════ ANOTE AGORA, APARECE UMA VEZ SÓ ════════${NC}"
    echo -e "  ${BOLD}$NOVA${NC}"
    echo -e "  ${RED}${BOLD}═════════════════════════════════════════════════${NC}"
    echo -e "  Guarde ${BOLD}fora do R2${NC} — no gerenciador de senhas da empresa ou em cofre."
    echo -e "  No mesmo lugar do backup ela não protege de nada, e sem ela os"
    echo -e "  arquivos enviados são irrecuperáveis."
    echo
    read -rp "  Digite ANOTEI para continuar: " conf
    [ "$conf" = "ANOTEI" ] || die "Interrompido. A frase JÁ está no .env — recupere com: grep BACKUP_ENCRYPT_PASSPHRASE $ENV_FILE"
    unset NOVA
fi

# ── 5. Ligar o envio ─────────────────────────────────────────────────────────
step 5 "Ligando o envio off-site no .env..."

env_set BACKUP_REMOTE_CMD "rclone copy --config $RCLONE_CONF r2:$BUCKET"
ok "BACKUP_REMOTE_CMD configurado"

if crontab -l 2>/dev/null | grep -Fq "deploy/backup.sh"; then
    ok "cron diário já agendado"
else
    (crontab -l 2>/dev/null; echo "0 3 * * * cd $APP_DIR && bash deploy/backup.sh >> /var/log/tenant-erp-backup.log 2>&1") | crontab -
    ok "cron diário agendado (03:00)"
fi

# ── 6. Backup real, ponta a ponta ────────────────────────────────────────────
step 6 "Rodando um backup de verdade..."

cd "$APP_DIR"
if bash deploy/backup.sh; then
    echo
    ok "backup concluído"
    echo -e "\n  ${BOLD}No bucket agora:${NC}"
    rclone ls --config "$RCLONE_CONF" "r2:$BUCKET" | tail -5 | sed 's/^/    /'
    echo
    echo -e "  ${GREEN}${BOLD}Pronto.${NC} Os arquivos sobem cifrados (.gpg); a cópia local fica em texto"
    echo -e "  puro para restauração rápida. Restauração: deploy/BACKUP.md"
    echo -e "\n  ${YELLOW}Falta um passo que só se faz no painel:${NC} R2 → $BUCKET → Settings →"
    echo -e "  Object lifecycle rules → expiração (30 ou 90 dias). Sem ela os dumps"
    echo -e "  acumulam para sempre — o script só limpa a cópia local."
else
    die "O backup falhou. O erro acima diz onde; o .env já está configurado, então
   corrija e rode de novo: cd $APP_DIR && bash deploy/backup.sh"
fi
