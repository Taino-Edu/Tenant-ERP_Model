#!/bin/bash
# fix-db.sh — Aplica migrations pendentes manualmente no PostgreSQL
# Uso: bash fix-db.sh

set -e

PSQL="docker exec cardgamestore_postgres psql -U cardgame_user -d cardgamestore"

echo "=== 1. Marcando migrations como aplicadas ==="
$PSQL -c "
INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\")
VALUES ('20260526180036_AddPaymentMethodToComanda', '8.0.10')
ON CONFLICT DO NOTHING;
"

echo "=== 2. Adicionando colunas faltantes ==="
$PSQL -c "
ALTER TABLE comandas      ADD COLUMN IF NOT EXISTS payment_method         TEXT;
ALTER TABLE users         ADD COLUMN IF NOT EXISTS balance_in_cents       INTEGER NOT NULL DEFAULT 0;
ALTER TABLE products      ADD COLUMN IF NOT EXISTS cost_price_in_cents    INTEGER NOT NULL DEFAULT 0;
ALTER TABLE crediarios    ADD COLUMN IF NOT EXISTS valor_pago_em_centavos INTEGER NOT NULL DEFAULT 0;
"

echo "=== 3. Criando tabelas faltantes ==="
$PSQL -c "
CREATE TABLE IF NOT EXISTS product_categories (
  id             TEXT NOT NULL,
  name           TEXT NOT NULL,
  emoji          TEXT,
  display_order  INTEGER NOT NULL DEFAULT 0,
  is_active      BOOLEAN NOT NULL DEFAULT true,
  created_at     TEXT NOT NULL,
  CONSTRAINT \"PK_product_categories\" PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_product_categories_name ON product_categories (name);

CREATE TABLE IF NOT EXISTS pagamentos_crediario (
  id                TEXT NOT NULL,
  crediario_id      TEXT NOT NULL,
  valor_em_centavos INTEGER NOT NULL,
  forma_pagamento   TEXT NOT NULL,
  observacao        TEXT,
  admin_id          TEXT NOT NULL,
  created_at        TEXT NOT NULL,
  CONSTRAINT \"PK_pagamentos_crediario\" PRIMARY KEY (id),
  CONSTRAINT \"FK_pagamentos_crediario_crediarios\" FOREIGN KEY (crediario_id)
    REFERENCES crediarios (id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_pagamentos_crediario_created_at ON pagamentos_crediario (created_at);
CREATE INDEX IF NOT EXISTS ix_pagamentos_crediario_crediario  ON pagamentos_crediario (crediario_id);
"

echo "=== 4. Reiniciando a API ==="
docker restart cardgamestore_api
sleep 6

echo "=== 5. Logs da API ==="
docker logs cardgamestore_api --tail=20

echo ""
echo "=== DONE — verifique se a API subiu sem erros ==="
