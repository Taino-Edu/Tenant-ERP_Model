#!/bin/bash
# fix-schema.sh — Recria tabelas e colunas faltantes após pg_restore
# Idempotente: usa IF NOT EXISTS em tudo
# Uso: bash fix-schema.sh

set -e

PG="docker exec cardgamestore_postgres psql -U cardgame_user -d cardgamestore"

echo "=== 1. Estado atual do __EFMigrationsHistory ==="
$PG -c "SELECT \"MigrationId\", \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";" 2>/dev/null || \
  echo "Tabela __EFMigrationsHistory não existe (será criada)"

echo ""
echo "=== 2. Tabelas existentes no banco ==="
$PG -c "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename;"

echo ""
echo "=== 3. Criando __EFMigrationsHistory se não existir ==="
$PG -c "
CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (
  \"MigrationId\"    VARCHAR(150) NOT NULL,
  \"ProductVersion\" VARCHAR(32)  NOT NULL,
  CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY (\"MigrationId\")
);
"

echo ""
echo "=== 4. Marcando migrations como aplicadas ==="
$PG -c "
INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\")
VALUES
  ('20250101000000_InitialCreate',              '8.0.10'),
  ('20260526180036_AddPaymentMethodToComanda',  '8.0.10')
ON CONFLICT DO NOTHING;

SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";
"

echo ""
echo "=== 5. Adicionando colunas faltantes ==="
$PG -c "
ALTER TABLE users     ADD COLUMN IF NOT EXISTS balance_in_cents       INTEGER NOT NULL DEFAULT 0;
ALTER TABLE products  ADD COLUMN IF NOT EXISTS cost_price_in_cents    INTEGER NOT NULL DEFAULT 0;
ALTER TABLE crediarios ADD COLUMN IF NOT EXISTS valor_pago_em_centavos INTEGER NOT NULL DEFAULT 0;
ALTER TABLE comandas  ADD COLUMN IF NOT EXISTS payment_method         VARCHAR(30);
"
echo "Colunas OK"

echo ""
echo "=== 6. Tornando crediarios.comanda_id nullable (se necessário) ==="
$PG -c "
ALTER TABLE crediarios ALTER COLUMN comanda_id DROP NOT NULL;
" 2>/dev/null || echo "comanda_id já é nullable ou não existe essa constraint — OK"

echo ""
echo "=== 7. Criando tabela product_categories ==="
$PG -c "
CREATE TABLE IF NOT EXISTS product_categories (
  id            UUID        NOT NULL DEFAULT gen_random_uuid(),
  name          VARCHAR(100) NOT NULL,
  emoji         VARCHAR(10),
  display_order INTEGER     NOT NULL DEFAULT 0,
  is_active     BOOLEAN     NOT NULL DEFAULT true,
  created_at    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
  CONSTRAINT \"PK_product_categories\" PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_product_categories_name ON product_categories (name);
"
echo "product_categories OK"

echo ""
echo "=== 8. Criando tabela pagamentos_crediario ==="
$PG -c "
CREATE TABLE IF NOT EXISTS pagamentos_crediario (
  id                UUID        NOT NULL DEFAULT gen_random_uuid(),
  crediario_id      UUID        NOT NULL,
  valor_em_centavos INTEGER     NOT NULL,
  forma_pagamento   VARCHAR(50) NOT NULL,
  observacao        VARCHAR(500),
  admin_id          UUID        NOT NULL,
  created_at        TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
  CONSTRAINT \"PK_pagamentos_crediario\" PRIMARY KEY (id),
  CONSTRAINT \"FK_pagamentos_crediario_crediarios\" FOREIGN KEY (crediario_id)
    REFERENCES crediarios (id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_pagamentos_crediario_created_at ON pagamentos_crediario (created_at);
CREATE INDEX IF NOT EXISTS ix_pagamentos_crediario_crediario  ON pagamentos_crediario (crediario_id);
"
echo "pagamentos_crediario OK"

echo ""
echo "=== 9. Sincronizando valor_pago_em_centavos com pagamentos existentes ==="
$PG -c "
UPDATE crediarios c
SET valor_pago_em_centavos = COALESCE((
  SELECT SUM(p.valor_em_centavos)
  FROM pagamentos_crediario p
  WHERE p.crediario_id = c.id
), 0)
WHERE valor_pago_em_centavos = 0;
"
echo "Sincronização OK"

echo ""
echo "=== 10. Verificação final ==="
$PG -c "
SELECT 'users'               AS tabela, COUNT(*) FROM users
UNION ALL SELECT 'products',             COUNT(*) FROM products
UNION ALL SELECT 'comandas',             COUNT(*) FROM comandas
UNION ALL SELECT 'crediarios',           COUNT(*) FROM crediarios
UNION ALL SELECT 'product_categories',   COUNT(*) FROM product_categories
UNION ALL SELECT 'pagamentos_crediario', COUNT(*) FROM pagamentos_crediario
ORDER BY tabela;
"

echo ""
echo "=== 11. Reiniciando a API ==="
docker restart cardgamestore_api
sleep 8

echo ""
echo "=== 12. Verificando startup da API ==="
docker logs cardgamestore_api --tail=15 2>&1

echo ""
echo "=== 13. Testando endpoints ==="
sleep 3
echo -n "GET /api/category → "
curl -s -o /dev/null -w "HTTP %{http_code}\n" http://localhost/api/category

echo -n "GET /api/product  → "
curl -s -o /dev/null -w "HTTP %{http_code}\n" http://localhost/api/product

echo -n "GET /health       → "
curl -sf http://localhost/health | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['status'])" 2>/dev/null || echo "falhou"

echo ""
echo "=== CONCLUÍDO — schema restaurado com sucesso ==="
