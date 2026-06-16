#!/bin/bash
# =============================================================================
# update.sh — Atualiza o SantuárioNerd no VPS com a última versão do GitHub
#
# USO:
#   bash /opt/santuarionerd/deploy/update.sh
# =============================================================================

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

APP_DIR="/opt/santuarionerd"

echo -e "${YELLOW}🔄 Atualizando SantuárioNerd...${NC}"

# Puxa última versão do GitHub
cd "$APP_DIR"
git pull origin main

# Copia .env para pasta deploy
cp "$APP_DIR/.env" "$APP_DIR/deploy/.env"

# Rebuild e redeploy — CACHEBUST força o Docker a recompilar o Next.js
cd "$APP_DIR/deploy"
docker compose -f docker-compose.prod.yml build --build-arg CACHEBUST="$(date +%s)"
docker compose -f docker-compose.prod.yml up -d

# Limpa imagens antigas
docker image prune -f

echo -e "${GREEN}✅ Atualização concluída!${NC}"
docker compose -f docker-compose.prod.yml ps
