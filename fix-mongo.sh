#!/bin/bash
# fix-mongo.sh — Migra dados do MongoDB antigo para o novo container
# Uso: bash fix-mongo.sh

set -e

NEW_MONGO="cardgamestore_mongo"
DB_NAME="cardgamestore_cache"

echo "=== 1. Containers MongoDB em execução ==="
docker ps --format "table {{.Names}}\t{{.Image}}\t{{.Status}}" | grep -i mongo || echo "(nenhum encontrado)"

echo ""
echo "=== 2. Detectando container MongoDB antigo ==="
OLD_MONGO=$(docker ps --format "{{.Names}}" | grep -i mongo | grep -v "$NEW_MONGO" | head -1)

if [ -z "$OLD_MONGO" ]; then
  echo "AVISO: Nenhum MongoDB antigo encontrado em execução."
  echo "Verificando containers parados..."
  OLD_MONGO=$(docker ps -a --format "{{.Names}}" | grep -i mongo | grep -v "$NEW_MONGO" | head -1)
  if [ -z "$OLD_MONGO" ]; then
    echo "ERRO: Nenhum MongoDB antigo encontrado. Listando todos os containers:"
    docker ps -a --format "table {{.Names}}\t{{.Image}}\t{{.Status}}"
    exit 1
  fi
  echo "Iniciando container parado: $OLD_MONGO"
  docker start "$OLD_MONGO"
  sleep 3
fi

echo "MongoDB antigo: $OLD_MONGO"
echo "MongoDB novo:   $NEW_MONGO"

echo ""
echo "=== 3. Verificando dados no MongoDB antigo ==="
docker exec "$OLD_MONGO" mongosh "$DB_NAME" --quiet --eval "
  const count = db.vendas_avulsas.countDocuments();
  print('vendas_avulsas no DB antigo: ' + count);
"

echo ""
echo "=== 4. Verificando dados no MongoDB novo (antes da migração) ==="
docker exec "$NEW_MONGO" mongosh "$DB_NAME" --quiet --eval "
  const count = db.vendas_avulsas.countDocuments();
  print('vendas_avulsas no DB novo: ' + count);
"

echo ""
echo "=== 5. Fazendo dump do MongoDB antigo ==="
docker exec "$OLD_MONGO" mongodump \
  --db "$DB_NAME" \
  --out /tmp/mongodump_migrate \
  --quiet

echo ""
echo "=== 6. Copiando dump para o container novo ==="
# Copia via tar: dump do old → host → new
docker exec "$OLD_MONGO" tar -czf /tmp/mongodump.tar.gz -C /tmp/mongodump_migrate .
docker cp "$OLD_MONGO":/tmp/mongodump.tar.gz /tmp/mongodump_migrate.tar.gz
docker cp /tmp/mongodump_migrate.tar.gz "$NEW_MONGO":/tmp/mongodump_migrate.tar.gz
docker exec "$NEW_MONGO" bash -c "mkdir -p /tmp/mongodump_restore && tar -xzf /tmp/mongodump_migrate.tar.gz -C /tmp/mongodump_restore"

echo ""
echo "=== 7. Restaurando no MongoDB novo ==="
docker exec "$NEW_MONGO" mongorestore \
  --db "$DB_NAME" \
  --dir "/tmp/mongodump_restore/$DB_NAME" \
  --drop \
  --quiet

echo ""
echo "=== 8. Verificando dados após migração ==="
docker exec "$NEW_MONGO" mongosh "$DB_NAME" --quiet --eval "
  const count = db.vendas_avulsas.countDocuments();
  print('vendas_avulsas no DB novo (após migração): ' + count);
  if (count > 0) {
    const ultima = db.vendas_avulsas.find().sort({soldAt:-1}).limit(1).toArray()[0];
    print('Última venda: ' + ultima.soldAt + ' — R\$ ' + (ultima.totalInCents/100).toFixed(2));
  }
"

echo ""
echo "=== 9. Limpando arquivos temporários ==="
docker exec "$OLD_MONGO" rm -rf /tmp/mongodump_migrate /tmp/mongodump.tar.gz 2>/dev/null || true
docker exec "$NEW_MONGO" rm -rf /tmp/mongodump_restore /tmp/mongodump_migrate.tar.gz 2>/dev/null || true
rm -f /tmp/mongodump_migrate.tar.gz 2>/dev/null || true

echo ""
echo "=== CONCLUÍDO — histórico de frente de caixa restaurado ==="
