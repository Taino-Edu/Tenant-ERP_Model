#!/bin/bash
# diag.sh — Diagnóstico rápido do stack em produção
# Uso: bash diag.sh

echo "========================================"
echo " DIAGNÓSTICO softNerd — $(date)"
echo "========================================"

echo ""
echo "=== CONTAINERS ==="
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

echo ""
echo "=== LOGS API (últimas 30 linhas) ==="
docker logs cardgamestore_api --tail=30 2>&1

echo ""
echo "=== HEALTH CHECK ==="
curl -sf http://localhost/health | python3 -m json.tool 2>/dev/null || \
  curl -sf http://localhost:5000/health | python3 -m json.tool 2>/dev/null || \
  echo "Health check falhou"

echo ""
echo "=== TESTE ENDPOINTS ==="
echo "--- GET /api/category (público) ---"
HTTP=$(curl -s -o /dev/null -w "%{http_code}" http://localhost/api/category)
echo "HTTP Status: $HTTP"
if [ "$HTTP" = "200" ]; then
  COUNT=$(curl -s http://localhost/api/category | python3 -c "import sys,json; d=json.load(sys.stdin); print(f'Categorias: {len(d)}')" 2>/dev/null || echo "erro ao parsear JSON")
  echo "$COUNT"
fi

echo ""
echo "--- GET /api/products (público) ---"
HTTP=$(curl -s -o /dev/null -w "%{http_code}" http://localhost/api/products)
echo "HTTP Status: $HTTP"
if [ "$HTTP" = "200" ]; then
  COUNT=$(curl -s http://localhost/api/products | python3 -c "import sys,json; d=json.load(sys.stdin); print(f'Produtos: {len(d)}')" 2>/dev/null || echo "erro ao parsear JSON")
  echo "$COUNT"
fi

echo ""
echo "=== BANCO POSTGRES ==="
docker exec cardgamestore_postgres psql -U cardgame_user -d cardgamestore -c "
SELECT 'users' as tabela, COUNT(*) FROM users
UNION ALL SELECT 'products', COUNT(*) FROM products
UNION ALL SELECT 'comandas', COUNT(*) FROM comandas
UNION ALL SELECT 'crediarios', COUNT(*) FROM crediarios
UNION ALL SELECT 'product_categories', COUNT(*) FROM product_categories
UNION ALL SELECT 'pagamentos_crediario', COUNT(*) FROM pagamentos_crediario;
"

echo ""
echo "=== BANCO MONGO ==="
docker exec cardgamestore_mongo mongosh cardgamestore_cache --quiet --eval "
  print('vendas_avulsas: ' + db.vendas_avulsas.countDocuments());
" 2>/dev/null || echo "MongoDB não disponível"

echo ""
echo "=== VARIÁVEL API_URL (usada no build do frontend) ==="
cat /opt/santuarionerd/.env | grep -E "API_URL|APP_URL" || echo "não encontrado"

echo ""
echo "=== CONCLUÍDO ==="
