#!/bin/bash
# =============================================================================
# deploy-oracle.sh — Sobe o CardGameStore na Oracle VM (Ubuntu/Oracle Linux)
# USO: ./deploy-oracle.sh <IP_PUBLICO_DA_VM>
# =============================================================================

set -e

IP="${1:?Informe o IP público da VM: ./deploy-oracle.sh 150.230.xx.xx}"

echo ""
echo "=================================================="
echo "  softNerd CardGameStore — Deploy Oracle Cloud"
echo "  VM: $IP"
echo "=================================================="
echo ""

# --------------------------------------------------------------------------
# 1. Instala Docker na VM (pula se já instalado)
# --------------------------------------------------------------------------
echo "→ Instalando Docker na VM..."
ssh -o StrictHostKeyChecking=no ubuntu@$IP '
  if ! command -v docker &>/dev/null; then
    curl -fsSL https://get.docker.com | sudo sh
    sudo usermod -aG docker $USER
    echo "Docker instalado."
  else
    echo "Docker já instalado: $(docker --version)"
  fi
'

# --------------------------------------------------------------------------
# 2. Copia o projeto para a VM
# --------------------------------------------------------------------------
echo ""
echo "→ Enviando arquivos para a VM..."
# Cria pasta no servidor
ssh ubuntu@$IP "mkdir -p ~/softnerd"

# Usa rsync se disponível, senão usa scp
if command -v rsync &>/dev/null; then
  rsync -az --exclude='.git' --exclude='node_modules' --exclude='.next' \
    --exclude='bin' --exclude='obj' \
    "$(dirname "$0")/" "ubuntu@$IP:~/softnerd/"
else
  scp -r "$(dirname "$0")/" "ubuntu@$IP:~/softnerd/"
fi

# --------------------------------------------------------------------------
# 3. Cria o .env de produção na VM
# --------------------------------------------------------------------------
echo ""
echo "→ Criando .env de produção..."
ssh ubuntu@$IP "cat > ~/softnerd/.env" << EOF
API_URL=http://$IP:5000
APP_URL=http://$IP:3000
JWT_SECRET=GBwFBhH/mRjHaWOc899q7me8iOK1wDFlZ1hiFM406QizpT2X39qp5eKQWVf/0xRJ
POSTGRES_USER=cardgame_user
POSTGRES_PASSWORD=ytLzPO6xKhkgBVZOsGlQMMJExqxcktBw
POSTGRES_DB=cardgamestore
POKEMON_API_KEY=
EMAIL_HOST=
EMAIL_PORT=587
EMAIL_USER=
EMAIL_PASSWORD=
EMAIL_FROM=noreply@softnerd.com.br
PGADMIN_EMAIL=admin@cardgame.com
PGADMIN_PASSWORD=admin123
EOF

# --------------------------------------------------------------------------
# 4. Abre as portas no firewall local da VM (iptables/firewalld)
# --------------------------------------------------------------------------
echo ""
echo "→ Abrindo portas no firewall da VM..."
ssh ubuntu@$IP '
  # Ubuntu usa ufw
  if command -v ufw &>/dev/null; then
    sudo ufw allow 3000/tcp
    sudo ufw allow 5000/tcp
    sudo ufw allow 22/tcp
    sudo ufw --force enable
    echo "ufw: portas 3000 e 5000 abertas."
  fi
  # Oracle Linux usa firewalld
  if command -v firewall-cmd &>/dev/null; then
    sudo firewall-cmd --permanent --add-port=3000/tcp
    sudo firewall-cmd --permanent --add-port=5000/tcp
    sudo firewall-cmd --reload
    echo "firewalld: portas 3000 e 5000 abertas."
  fi
'

# --------------------------------------------------------------------------
# 5. Sobe os containers
# --------------------------------------------------------------------------
echo ""
echo "→ Subindo containers (isso leva ~3 min no primeiro build)..."
ssh ubuntu@$IP '
  cd ~/softnerd
  # Garante que o docker compose está disponível
  if ! docker compose version &>/dev/null; then
    sudo apt-get install -y docker-compose-plugin 2>/dev/null || \
    sudo yum install -y docker-compose-plugin 2>/dev/null || true
  fi
  docker compose down --remove-orphans 2>/dev/null || true
  docker compose up -d --build
  echo ""
  echo "Aguardando containers ficarem saudáveis..."
  sleep 20
  docker compose ps
'

# --------------------------------------------------------------------------
# 6. Verifica
# --------------------------------------------------------------------------
echo ""
echo "→ Verificando health da API..."
sleep 5
STATUS=$(curl -sf "http://$IP:5000/health" 2>/dev/null | python3 -c "import json,sys; d=json.load(sys.stdin); print(d['status'])" 2>/dev/null || echo "aguardando...")
echo "  API health: $STATUS"

echo ""
echo "=================================================="
echo "  ✅ Deploy concluído!"
echo ""
echo "  Frontend : http://$IP:3000"
echo "  API/Docs : http://$IP:5000/swagger"
echo "  Health   : http://$IP:5000/health"
echo ""
echo "  Login admin:"
echo "  Email : admin@cardgamestore.com.br"
echo "  Senha : SenhaForte@123"
echo "=================================================="
echo ""
echo "⚠️  IMPORTANTE: Lembre de abrir as portas 3000 e 5000"
echo "   no Security List da Oracle Cloud Console também!"
echo "   (VCN → Subnets → Security List → Add Ingress Rules)"
echo ""
