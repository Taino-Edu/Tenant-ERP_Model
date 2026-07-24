#!/bin/bash
# =============================================================================
# update.sh — Atualiza o Tenant-ERP no VPS com a última versão do GitHub
#
# USO:
#   bash /opt/tenant-erp/deploy/update.sh
#
# FLUXO SEGURO:
#   1. Backup do banco ANTES de qualquer mudança (ponto de restauração).
#   2. Taga as imagens atuais como :rollback.
#   3. git pull + build + up -d (migrations rodam no boot da API).
#   4. Health check em /health. Se a API não subir, reverte pras imagens
#      :rollback automaticamente e aborta — o site volta pro estado anterior.
#
# ⚠️  LIMITE: o rollback reverte o CÓDIGO (imagens), não o SCHEMA. As migrations
#     rodam no boot e não são desfeitas aqui. Se uma migration destrutiva
#     corromper dados, a recuperação é restaurar o dump do passo 1:
#       gunzip -c /opt/tenant-erp/backups/postgres_<TS>.sql.gz \
#         | docker exec -i cardgamestore_postgres psql -U <user> <db>
# =============================================================================

set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

APP_DIR="/opt/tenant-erp"
COMPOSE_DIR="$APP_DIR/deploy"
COMPOSE="docker compose -f docker-compose.prod.yml"
IMAGES=(cardgamestore_api cardgamestore_frontend)

echo -e "${YELLOW}🔄 Atualizando Tenant-ERP...${NC}"

# ── 1. Backup pré-deploy (ponto de restauração) ─────────────────────────────
echo -e "${YELLOW}💾 Backup do banco antes de atualizar...${NC}"
if ! bash "$APP_DIR/deploy/backup.sh"; then
    echo -e "${RED}❌ Backup pré-deploy falhou — abortando a atualização por segurança.${NC}"
    echo -e "${RED}   (Não faz sentido migrar o schema sem um ponto de restauração.)${NC}"
    exit 1
fi

# ── 2. Taga as imagens atuais como :rollback ────────────────────────────────
cd "$COMPOSE_DIR"
for img in "${IMAGES[@]}"; do
    if docker image inspect "$img:latest" &>/dev/null; then
        docker tag "$img:latest" "$img:rollback"
    fi
done

# ── 3. Pull + build + up ────────────────────────────────────────────────────
cd "$APP_DIR"
git pull origin main
cp "$APP_DIR/.env" "$APP_DIR/deploy/.env"

cd "$COMPOSE_DIR"
# CACHEBUST força o Docker a recompilar o Next.js
$COMPOSE build --build-arg CACHEBUST="$(date +%s)"
$COMPOSE up -d

# nginx.conf entra por bind mount, não pela imagem: o `up -d` acima só recria
# containers cuja DEFINIÇÃO de serviço mudou, então alterar o nginx.conf no
# repositório deixava o nginx servindo a config antiga indefinidamente — sem
# erro nenhum, o deploy passava verde e a mudança simplesmente não valia (bug
# real: a rota /mcp caiu no frontend por isso). Recarrega de forma graciosa,
# sem derrubar conexão. Se a config estiver inválida, o `nginx -t` falha e o
# nginx segue com a anterior em vez de morrer.
if $COMPOSE ps --status running --services 2>/dev/null | grep -qx nginx; then
    if $COMPOSE exec -T nginx nginx -t >/dev/null 2>&1; then
        $COMPOSE exec -T nginx nginx -s reload >/dev/null 2>&1 \
            && echo -e "${GREEN}✅ nginx recarregado (config atualizada).${NC}"
    else
        echo -e "${YELLOW}⚠️  nginx -t reprovou a config nova — mantendo a anterior. Rode '$COMPOSE exec nginx nginx -t' pra ver o erro.${NC}"
    fi
fi

# ── 4. Health check + auto-rollback ─────────────────────────────────────────
echo -n "  Aguardando API responder /health"
healthy=false
# Até 3 minutos: em instalações com vários tenants, as migrations de todos os
# schemas rodam antes de o servidor começar a responder.
for _ in {1..60}; do
    if $COMPOSE exec -T api curl -sf http://localhost:5000/health &>/dev/null; then
        healthy=true
        break
    fi
    echo -n "."
    sleep 3
done
echo ""

if [ "$healthy" != true ]; then
    echo -e "${RED}❌ A API não respondeu /health após o deploy. Revertendo pras imagens anteriores...${NC}"
    echo -e "${YELLOW}📋 Últimos logs da API que falhou:${NC}"
    # O --force-recreate do rollback remove o container defeituoso. Exiba os
    # logs antes disso para a causa não desaparecer junto com ele.
    $COMPOSE logs --no-color --tail=200 api >&2 || true
    reverted=false
    for img in "${IMAGES[@]}"; do
        if docker image inspect "$img:rollback" &>/dev/null; then
            docker tag "$img:rollback" "$img:latest"
            reverted=true
        fi
    done
    if [ "$reverted" = true ]; then
        # Retaguear :latest não troca a imagem de um container já criado.
        # --force-recreate garante que api/frontend voltem de fato aos IDs
        # preservados em :rollback.
        $COMPOSE up -d --force-recreate api frontend
        echo -e "${YELLOW}↩️  Rollback de código aplicado (imagens :rollback restauradas).${NC}"
        echo -e "${YELLOW}   Se o problema foi de schema/migration, restaure o dump do backup (ver cabeçalho deste script).${NC}"
    else
        echo -e "${RED}   Sem imagem :rollback disponível (primeira atualização?). Contêineres novos mantidos — investigue os logs:${NC}"
        echo -e "${RED}   cd $COMPOSE_DIR && $COMPOSE logs --tail=100 api${NC}"
    fi
    exit 1
fi

# ── Sucesso: limpa imagens dangling (preserva :rollback) ────────────────────
echo -e "${GREEN}✅ API saudável.${NC}"
docker image prune -f

echo -e "${GREEN}✅ Atualização concluída!${NC}"
$COMPOSE ps
