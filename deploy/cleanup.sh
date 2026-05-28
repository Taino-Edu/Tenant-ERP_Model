#!/bin/bash
# =============================================================================
# cleanup.sh — Limpeza segura da VPS SantuárioNerd
#
# O que faz:
#   1. Remove imagens Docker sem tag (dangling) de builds anteriores
#   2. Remove build cache do Docker (maior economia de espaço)
#   3. Remove containers parados (não afeta containers em execução)
#   4. Remove cache do APT
#   5. Limita logs do systemd a 200 MB
#   6. Executa git gc para compactar histórico
#
# O que NÃO toca:
#   - Volumes Docker (postgres_data, mongo_data, api_uploads) — dados da loja
#   - Containers em execução (nginx, frontend, api, postgres, mongo)
#   - Código em /opt/santuarionerd
#
# USO:
#   bash /opt/santuarionerd/deploy/cleanup.sh
# =============================================================================

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

step() { echo -e "\n${YELLOW}${BOLD}▶ $1${NC}"; }
ok()   { echo -e "  ${GREEN}✅ $1${NC}"; }

echo -e "${CYAN}${BOLD}"
echo "  ╔══════════════════════════════════════════════╗"
echo "  ║      🧹  SantuárioNerd — Limpeza VPS         ║"
echo "  ╚══════════════════════════════════════════════╝"
echo -e "${NC}"

# Espaço antes
BEFORE=$(df -h / | awk 'NR==2{print $3}')
echo -e "  💾 Espaço em uso agora: ${BOLD}$BEFORE${NC}"

# =============================================================================
# 1. Imagens Docker sem tag (sobras de builds antigos)
# =============================================================================
step "Removendo imagens Docker sem tag (dangling)..."
docker image prune -f
ok "Imagens dangling removidas"

# =============================================================================
# 2. Build cache do Docker (maior economia — pode liberar 3-6 GB)
# =============================================================================
step "Limpando build cache do Docker..."
docker builder prune -af
ok "Build cache removido"

# =============================================================================
# 3. Containers parados (não remove os que estão rodando)
# =============================================================================
step "Removendo containers parados..."
docker container prune -f
ok "Containers parados removidos"

# =============================================================================
# 4. Redes Docker não utilizadas
# =============================================================================
step "Removendo redes Docker não utilizadas..."
docker network prune -f
ok "Redes não utilizadas removidas"

# =============================================================================
# 5. Cache do APT
# =============================================================================
step "Limpando cache do APT..."
apt-get clean -y
apt-get autoremove -y --purge 2>/dev/null || true
ok "Cache APT limpo"

# =============================================================================
# 6. Logs do systemd — limita a 200 MB
# =============================================================================
step "Compactando logs do systemd (máx 200 MB)..."
journalctl --vacuum-size=200M
ok "Logs systemd compactados"

# =============================================================================
# 7. Git gc — compacta histórico do repositório
# =============================================================================
step "Compactando histórico git..."
cd /opt/santuarionerd
git gc --prune=now --quiet
ok "Histórico git compactado"

# =============================================================================
# Resumo
# =============================================================================
AFTER=$(df -h / | awk 'NR==2{print $3}')

echo ""
echo -e "${GREEN}${BOLD}"
echo "  ╔══════════════════════════════════════════════╗"
echo "  ║             🎉  Limpeza concluída!            ║"
echo "  ╠══════════════════════════════════════════════╣"
printf "  ║  Antes:  %-35s║\n" "$BEFORE usado"
printf "  ║  Depois: %-35s║\n" "$AFTER usado"
echo "  ╚══════════════════════════════════════════════╝"
echo -e "${NC}"
echo -e "  ${YELLOW}Containers ainda rodando:${NC}"
docker compose -f /opt/santuarionerd/deploy/docker-compose.prod.yml ps
