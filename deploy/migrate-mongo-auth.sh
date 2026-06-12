#!/usr/bin/env bash
# migrate-mongo-auth.sh
# Habilita autenticação no MongoDB sem derrubar postgres ou frontend.
# Rode UMA VEZ na VPS após adicionar MONGO_PASSWORD no .env
#
# Uso: bash deploy/migrate-mongo-auth.sh
# ----------------------------------------------------------------------------

set -euo pipefail

COMPOSE="docker compose -f /opt/santuarionerd/deploy/docker-compose.prod.yml"
ENV_FILE="/opt/santuarionerd/.env"

# 1. Garante que MONGO_PASSWORD está no .env
if ! grep -q "MONGO_PASSWORD" "$ENV_FILE" 2>/dev/null; then
  echo ""
  read -rsp "Digite a senha que quer definir para o MongoDB: " MONGO_PASS
  echo ""
  echo "MONGO_PASSWORD=${MONGO_PASS}" >> "$ENV_FILE"
  echo "[ok] MONGO_PASSWORD adicionado ao .env"
else
  echo "[ok] MONGO_PASSWORD já existe no .env"
fi

MONGO_USER=$(grep "MONGO_USER" "$ENV_FILE" 2>/dev/null | cut -d= -f2 || echo "mongo_admin")
MONGO_USER="${MONGO_USER:-mongo_admin}"
MONGO_PASS=$(grep "MONGO_PASSWORD" "$ENV_FILE" | cut -d= -f2)

# 2. Cria o usuário root no mongo ATUAL (antes de habilitar auth)
echo "[...] Criando usuário root no MongoDB atual..."
docker exec santuarionerd_mongo mongosh --quiet --eval "
  use admin;
  const exists = db.getUser('${MONGO_USER}');
  if (exists) {
    print('Usuário já existe, atualizando senha...');
    db.updateUser('${MONGO_USER}', { pwd: '${MONGO_PASS}' });
  } else {
    db.createUser({ user: '${MONGO_USER}', pwd: '${MONGO_PASS}', roles: ['root'] });
    print('Usuário criado.');
  }
"
echo "[ok] Usuário MongoDB configurado"

# 3. Reinicia só o mongo (para pegar as novas env vars com auth)
echo "[...] Reiniciando mongo com autenticação..."
$COMPOSE stop mongo
$COMPOSE up -d mongo
sleep 5

# 4. Testa a conexão autenticada
echo "[...] Testando conexão autenticada..."
docker exec santuarionerd_mongo mongosh \
  "mongodb://${MONGO_USER}:${MONGO_PASS}@localhost:27017/?authSource=admin" \
  --quiet --eval "db.runCommand({ ping: 1 })" \
  && echo "[ok] Autenticação funcionando" \
  || { echo "[ERRO] Falha na autenticação — verifique a senha e tente novamente"; exit 1; }

# 5. Reinicia a API para pegar a nova connection string
echo "[...] Reiniciando API..."
$COMPOSE stop api
$COMPOSE up -d api

echo ""
echo "============================================"
echo " Migração concluída com sucesso!"
echo " MongoDB agora exige autenticação."
echo "============================================"
