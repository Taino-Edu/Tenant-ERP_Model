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
    read -rsp "  Access Key ID (32 caracteres): "   ACCESS_KEY;   echo
    read -rsp "  Secret Access Key: "               SECRET_KEY;   echo

    # Vazio primeiro: com o terminal não ecoando nada, dar Enter sem colar é
    # fácil, e "0 caracteres" precisa de uma mensagem própria — não da explicação
    # sobre o Token value, que não tem nada a ver.
    [ -n "$ENDPOINT_RAW" ] || die "Endpoint vazio. Nada foi gravado — rode de novo e cole o valor."
    [ -n "$ACCESS_KEY" ]   || die "Access Key ID vazio. O terminal não mostra o que você cola, mas precisa colar."
    [ -n "$SECRET_KEY" ]   || die "Secret Access Key vazio. Nada foi gravado — rode de novo."

    # 32 é o tamanho fixo do Access Key ID do R2. Conferir aqui, e não lá na
    # frente: o R2 só reclama disso na primeira chamada, dentro de um 400
    # genérico, e sem esta checagem a config já teria sido gravada.
    if [ "${#ACCESS_KEY}" -ne 32 ]; then
        die "O Access Key ID tem ${#ACCESS_KEY} caracteres — o do R2 tem exatamente 32.
   Com ~53 é o \"Token value\" do topo da tela, que não serve aqui.
   Volte na tela do token e pegue o campo \"Access Key ID\"."
    fi

    # Aceita as duas formas porque a Cloudflare mostra o endpoint pronto e pede
    # trabalho extrair o Account ID dele. E colar o endpoint é mais seguro do que
    # remontá-lo: uma conta com jurisdição (UE, por exemplo) tem host diferente
    # — `<id>.eu.r2.cloudflarestorage.com` — e remontar como `<id>.r2...`
    # produziria uma URL que não existe, falhando como se fosse credencial.
    ENDPOINT_RAW="${ENDPOINT_RAW%/}"
    case "$ENDPOINT_RAW" in
        *r2.cloudflarestorage.com*)
            # Fica SÓ o host. A tela de Settings do bucket mostra o endpoint com
            # o bucket no fim (".../octus-backup"), e gravar isso faz o rclone
            # acrescentar o bucket outra vez: a requisição vira
            # ".../octus-backup/octus-backup/arquivo" e o R2 responde 400
            # Bad Request no HeadObject — que não se parece nem com credencial
            # nem com permissão, e foi exatamente onde isto travou na prática.
            host="${ENDPOINT_RAW#http://}"; host="${host#https://}"; host="${host%%/*}"
            ENDPOINT="https://$host" ;;
        *.*|*/*)
            die "Não reconheci \"${ENDPOINT_RAW:0:12}...\" como endpoint do R2 nem como Account ID.
   Esperado: https://<id>.r2.cloudflarestorage.com  ou só o <id>." ;;
        *)
            ENDPOINT="https://$ENDPOINT_RAW.r2.cloudflarestorage.com" ;;
    esac

    # Só agora, porque depende do $ENDPOINT já resolvido: o Account ID também
    # tem 32 caracteres hex e passaria pela checagem de tamanho — e está bem à
    # mão, dentro do endpoint digitado um prompt antes.
    if [ "$ENDPOINT" = "https://$ACCESS_KEY.r2.cloudflarestorage.com" ]; then
        die "Esse Access Key ID é igual ao Account ID do endpoint.
   São coisas diferentes: o Account ID identifica a conta e aparece na URL do
   painel; o Access Key ID vem da tela do token, logo abaixo do \"Token value\"."
    fi

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

# Confere que o bucket existe ANTES de tentar escrever. Com `no_check_bucket`
# ligado o rclone não valida nada, então um nome errado — plural a mais, hífen
# no lugar de underscore — viraria um erro de escrita genérico, do mesmo formato
# de credencial inválida. Listar primeiro transforma isso em "não achei X, mas
# existe Y", que é a diferença entre corrigir em dez segundos e perder a tarde.
if BUCKETS=$(rclone lsd --config "$RCLONE_CONF" r2: 2>/dev/null); then
    if echo "$BUCKETS" | awk '{print $NF}' | grep -qx "$BUCKET"; then
        ok "bucket r2:$BUCKET encontrado"
    else
        echo -e "\n  ${RED}Não existe um bucket chamado \"$BUCKET\" nesta conta.${NC}"
        echo -e "  Buckets disponíveis:"
        echo "$BUCKETS" | awk '{print "    - " $NF}'
        die "Rode de novo apontando para o nome certo, por exemplo:
   BUCKET=$(echo "$BUCKETS" | awk '{print $NF}' | head -1) bash deploy/setup-r2.sh"
    fi
else
    # Token com escopo de bucket não tem permissão para listar a conta inteira,
    # e isso é o esperado — não é erro. Segue para a escrita, que é o teste que
    # realmente importa.
    warn "não consegui listar os buckets (normal em token com escopo de bucket) — indo direto ao teste de escrita"
fi

TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT
CANARIO="setup-r2-$(date +%s).txt"
echo "teste de escrita $(date -Iseconds)" > "$TMP_DIR/$CANARIO"

# Guarda a saída do rclone em vez de descartar: listar três causas possíveis
# quando o próprio rclone já disse qual foi é o oposto do que este script se
# propõe a fazer. O `-v` traz o status HTTP e o código de erro do S3.
if ! ERRO=$(rclone copy -v --config "$RCLONE_CONF" "$TMP_DIR/$CANARIO" "r2:$BUCKET" 2>&1); then
    causa="não identifiquei o motivo — a saída do rclone está abaixo"
    case "$ERRO" in
        # Antes do 400 genérico: este É um 400, mas com causa exata. A tela da
        # Cloudflare mostra o "Token value" (~53 caracteres) em cima e mais
        # destacado que o Access Key ID (32), e colar o de cima é o erro natural.
        *"access key has length"*|*InvalidArgument*Credential*)
            causa="você colou o \"Token value\" no lugar do Access Key ID.
     O Access Key ID tem 32 caracteres; o Token value tem ~53.
     Pegue o campo certo na tela do token, apague $RCLONE_CONF e rode de novo." ;;
        *403*|*AccessDenied*)      causa="o token é somente leitura. Crie outro com 'Object Read and Write' e rode de novo." ;;
        *401*|*InvalidAccessKey*)  causa="Access Key ID ou Secret Access Key incorretos. Apague $RCLONE_CONF e rode de novo." ;;
        *NoSuchBucket*)            causa="não existe bucket '$BUCKET' neste endpoint. Confira o nome e o Account ID." ;;
        *400*|*BadRequest*)        causa="o endpoint provavelmente tem o bucket no fim. Deve terminar em .r2.cloudflarestorage.com, sem caminho. Confira: grep ^endpoint $RCLONE_CONF" ;;
        *SignatureDoesNotMatch*)   causa="o Secret Access Key não confere com o Access Key ID." ;;
        *imeout*|*o\ such\ host*|*dial\ tcp*) causa="não alcancei o endpoint. O Account ID nele provavelmente está errado." ;;
    esac
    die "Não consegui ESCREVER em r2:$BUCKET.

   ➜ $causa

   Saída do rclone:
$(echo "$ERRO" | tail -8 | sed 's/^/     /')"
fi
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
