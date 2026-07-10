#!/bin/bash
# =============================================================================
# setup.sh — Instalação automática Tenant-ERP no VPS
# Ubuntu 24.04 LTS
#
# USO (como root no VPS):
#   curl -fsSL https://raw.githubusercontent.com/Taino-Edu/Tenant-ERP_Model/main/deploy/setup.sh | bash
# =============================================================================

set -e

CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BOLD='\033[1m'
NC='\033[0m'

APP_DIR="/opt/tenant-erp"
REPO_URL="https://github.com/Taino-Edu/Tenant-ERP_Model.git"

banner() {
    echo -e "${CYAN}${BOLD}"
    echo "  ╔═══════════════════════════════════════════════╗"
    echo "  ║          TENANT-ERP  —  Setup VPS             ║"
    echo "  ╚═══════════════════════════════════════════════╝"
    echo -e "${NC}"
}

step() { echo -e "\n${YELLOW}${BOLD}[$1/$TOTAL] $2${NC}"; }
ok()   { echo -e "  ${GREEN}✅ $1${NC}"; }
warn() { echo -e "  ${RED}⚠️  $1${NC}"; }

TOTAL=7
banner

# =============================================================================
# 1. Atualizar sistema
# =============================================================================
step 1 "Atualizando sistema Ubuntu..."
apt-get update -y -qq
apt-get upgrade -y -qq
apt-get install -y -qq curl git openssl ufw
ok "Sistema atualizado"

# =============================================================================
# 2. Instalar Docker + Docker Compose
# =============================================================================
step 2 "Instalando Docker..."
if command -v docker &>/dev/null; then
    ok "Docker já instalado ($(docker --version))"
else
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
    ok "Docker instalado"
fi

if ! docker compose version &>/dev/null; then
    apt-get install -y -qq docker-compose-plugin
fi
ok "Docker Compose: $(docker compose version --short)"

# =============================================================================
# 3. Configurar firewall
# =============================================================================
step 3 "Configurando firewall UFW..."
ufw --force reset -y 2>/dev/null || true
ufw default deny incoming
ufw default allow outgoing
ufw allow ssh         comment 'SSH'
ufw allow 80/tcp      comment 'HTTP - Cloudflare'
ufw --force enable
ok "Firewall: SSH(22) e HTTP(80) liberados | Resto bloqueado"

# =============================================================================
# 4. Clonar repositório
# =============================================================================
step 4 "Clonando repositório..."
if [ -d "$APP_DIR/.git" ]; then
    cd "$APP_DIR"
    git pull origin main
    ok "Repositório atualizado (git pull)"
else
    git clone --depth=1 "$REPO_URL" "$APP_DIR"
    ok "Repositório clonado em $APP_DIR"
fi
cd "$APP_DIR"

# =============================================================================
# 5. Configurar variáveis de ambiente
# =============================================================================
step 5 "Configurando .env de produção..."
if [ ! -f "$APP_DIR/.env" ]; then
    # Gera segredos automaticamente
    POSTGRES_PASS=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 24)
    MONGO_PASS=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 24)
    JWT_SECRET=$(openssl rand -base64 64 | tr -d '\n')
    IP_SALT=$(openssl rand -hex 32)
    ENCRYPTION_KEY=$(openssl rand -base64 32)
    # -4 força IPv4 — em VPS com IPv6 configurado, ifconfig.me sem essa flag pode
    # devolver o endereço IPv6, que fica inacessível pro navegador (bug real já visto
    # em produção: NEXT_PUBLIC_API_URL/JwtSettings:Issuer gravados com IPv6 e o login
    # falhava silenciosamente, sem nenhuma requisição de rede sequer aparecer).
    PUBLIC_IP=$(curl -4 -fsSL ifconfig.me || hostname -I | tr ' ' '\n' | grep -E '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' | head -1)

    cat > "$APP_DIR/.env" <<EOF
# Gerado por setup.sh em $(date)
# Edite este arquivo e preencha GEMINI_API_KEY e SMTP_PASSWORD

# --- Acesso (sem domínio ainda — teste por IP direto, sem HTTPS) ---
# Quando configurar domínio + Cloudflare: trocar pra https://seu-dominio.com
# e mudar COOKIE_SECURE pra true.
APP_URL=http://${PUBLIC_IP}
COOKIE_SECURE=false

# --- PostgreSQL ---
POSTGRES_DB=cardgamestore
POSTGRES_USER=cardgame_user
POSTGRES_PASSWORD=${POSTGRES_PASS}

# --- MongoDB (cache de vendas avulsas) ---
MONGO_USER=mongo_admin
MONGO_PASSWORD=${MONGO_PASS}

# --- JWT (não altere após primeiro deploy) ---
JWT_SECRET=${JWT_SECRET}

# --- E-mail via Resend (resend.com — plano gratuito) ---
SMTP_HOST=smtp.resend.com
SMTP_PORT=587
SMTP_USERNAME=resend
SMTP_PASSWORD=PREENCHA_COM_API_KEY_DO_RESEND
SMTP_FROM_EMAIL=noreply@tenant-erp.local
SMTP_FROM_NAME=Tenant ERP

# --- Google Gemini IA ---
GEMINI_API_KEY=PREENCHA_COM_SUA_CHAVE_GEMINI

# --- Segurança ---
IP_HASH_SALT=${IP_SALT}

# --- Criptografia (senha de certificado, client secrets, tokens OAuth) ---
# NUNCA troque depois do primeiro deploy — dados já criptografados com a chave
# antiga viram ilegíveis pra sempre. Faça backup deste valor.
ENCRYPTION_KEY=${ENCRYPTION_KEY}
EOF
    ok ".env criado com senhas geradas automaticamente (APP_URL=http://${PUBLIC_IP})"
    warn "Edite o .env antes de continuar:"
    warn "  nano $APP_DIR/.env"
    warn "Preencha: SMTP_PASSWORD e GEMINI_API_KEY"
    echo ""
    echo -e "${BOLD}  Pressione ENTER após editar o .env para continuar...${NC}"
    read -r
else
    ok ".env já existe, mantendo"
fi

# =============================================================================
# 6. Build das imagens Docker (na própria máquina)
# =============================================================================
step 6 "Buildando imagens Docker (pode demorar ~5 min na primeira vez)..."
# Copia env para a pasta deploy ANTES do build
cp "$APP_DIR/.env" "$APP_DIR/deploy/.env"

cd "$APP_DIR/deploy"
docker compose -f docker-compose.prod.yml build --no-cache
ok "Imagens buildadas com sucesso"

# =============================================================================
# 7. Subir os containers
# =============================================================================
step 7 "Iniciando containers..."
cd "$APP_DIR/deploy"
docker compose -f docker-compose.prod.yml up -d

# Aguarda a API ficar saudável
echo -n "  Aguardando API inicializar"
for i in {1..30}; do
    if docker compose -f docker-compose.prod.yml exec -T api curl -sf http://localhost:5000/health &>/dev/null 2>&1; then
        echo ""
        ok "API respondendo"
        break
    fi
    echo -n "."
    sleep 3
done

APP_URL_LINE=$(grep '^APP_URL=' "$APP_DIR/.env" | cut -d= -f2-)
echo ""
echo -e "${GREEN}${BOLD}"
echo "  ╔══════════════════════════════════════════════════════╗"
echo "  ║      🎉  Tenant-ERP instalado com sucesso!            ║"
echo "  ╠══════════════════════════════════════════════════════╣"
echo "  ║  🌐 Site:     ${APP_URL_LINE}"
echo "  ║  📁 Arquivos: $APP_DIR/"
echo "  ╠══════════════════════════════════════════════════════╣"
echo "  ║  Comandos úteis:                                     ║"
echo "  ║  • Ver logs:    cd $APP_DIR/deploy                   ║"
echo "  ║                 docker compose logs -f               ║"
echo "  ║  • Atualizar:   bash $APP_DIR/deploy/update.sh        ║"
echo "  ║  • Reiniciar:   docker compose restart               ║"
echo "  ╚══════════════════════════════════════════════════════╝"
echo -e "${NC}"
