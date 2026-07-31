\set ON_ERROR_STOP on

\if :{?expected_database}
\else
\set expected_database qa_erp
\endif

SELECT current_database() = :'expected_database' AS is_expected_database \gset
\if :is_expected_database
\else
\echo 'RECUSADO: seed-load.sql so pode executar no banco QA configurado.'
DO $$ BEGIN
    RAISE EXCEPTION 'Banco atual nao corresponde ao QA configurado.';
END $$;
\endif

-- Massa sintética determinística. Todos os registros usam prefixo LOAD_ ou
-- UUID derivado de "load-*", permitindo repetir a carga sem duplicar dados.
SET search_path TO :"schema";
SET statement_timeout = 0;

\echo 'Populando usuários...'
INSERT INTO users (
    id, name, email, password_hash, whatsapp, cpf, profile_image_url, role,
    refresh_token, refresh_token_expiry, password_reset_token,
    password_reset_token_expiry, points_balance, points_expires_at,
    balance_in_cents, preferences_json, created_at, updated_at, is_active,
    deleted_at, consent_at, perfil_id, last_login_at)
SELECT
    md5('load-user-' || g)::uuid,
    'LOAD Usuário ' || g,
    'load-user-' || g || '@example.invalid',
    NULL, NULL, NULL, NULL, 'Customer',
    NULL, NULL, NULL, NULL,
    g % 5000, now() + interval '1 year', g % 100000, NULL,
    now() - ((g % 730) || ' days')::interval,
    now(), true, NULL, now(), NULL,
    now() - ((g % 120) || ' days')::interval
FROM generate_series(1, :users) AS g
ON CONFLICT (id) DO NOTHING;

\echo 'Populando produtos...'
INSERT INTO products (
    id, name, description, category, barcode, cost_price_in_cents,
    price_in_cents, stock_quantity, minimum_stock, ncm,
    natureza_operacao_id, image_url, image_urls, full_description,
    is_active, is_featured, show_on_site, show_on_marketplace,
    discount_price_in_cents, is_pre_venda, has_variants, created_at,
    updated_at, cest, fonte_tributos, percentual_tributos_estaduais,
    percentual_tributos_federais, percentual_tributos_municipais,
    ibpt_chave, ibpt_versao, tributos_atualizados_em,
    tributos_preenchidos_automaticamente, tributos_vigencia_fim,
    tributos_vigencia_inicio)
SELECT
    md5('load-product-' || g)::uuid,
    'LOAD Produto ' || g,
    'Produto sintético da auditoria de carga',
    'LOAD Categoria ' || (g % 50),
    'LOAD' || lpad(g::text, 14, '0'),
    500 + (g % 5000),
    1000 + (g % 15000),
    g % 500,
    10,
    lpad((g % 99999999)::text, 8, '0'),
    NULL, NULL, ARRAY[]::text[], NULL,
    true, (g % 20 = 0), true, true,
    CASE WHEN g % 10 = 0 THEN 900 + (g % 12000) ELSE NULL END,
    false, false,
    now() - ((g % 730) || ' days')::interval,
    now(), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, NULL, NULL
FROM generate_series(1, :products) AS g
ON CONFLICT (id) DO NOTHING;

\echo 'Populando comandas...'
INSERT INTO comandas (
    id, user_id, table_identifier, status, opened_at, closed_at,
    payment_method, second_payment_method, second_payment_amount_in_cents,
    total_in_cents, points_applied, discount_in_cents, notes,
    fiscal_effects_captured_at, points_debited_at_sale,
    cashback_debited_at_sale, points_awarded_at_sale,
    crediario_id_at_sale, crediario_amount_at_sale)
SELECT
    md5('load-comanda-' || g)::uuid,
    md5('load-user-' || (1 + (g % :users)))::uuid,
    'LOAD-' || (g % 200),
    CASE WHEN g % 100 < 95 THEN 'Fechada'
         WHEN g % 100 < 98 THEN 'Aberta'
         ELSE 'EmAndamento' END,
    now() - ((g % 120) || ' days')::interval - ((g % 86400) || ' seconds')::interval,
    CASE WHEN g % 100 < 95
         THEN now() - ((g % 120) || ' days')::interval - ((g % 86400) || ' seconds')::interval
         ELSE NULL END,
    CASE g % 4 WHEN 0 THEN 'Pix' WHEN 1 THEN 'Dinheiro'
         WHEN 2 THEN 'CartaoCredito' ELSE 'CartaoDebito' END,
    NULL, 0, 1000 + (g % 30000), 0, g % 500, NULL,
    now(), 0, 0, g % 100, NULL, 0
FROM generate_series(1, :orders) AS g
ON CONFLICT (id) DO NOTHING;

\echo 'Populando itens de comandas...'
INSERT INTO comanda_items (
    id, comanda_id, product_id, variant_id, item_name_snapshot,
    unit_price_in_cents, cost_price_snapshot_in_cents, quantity,
    subtotal_in_cents, added_at, added_by_user_id)
SELECT
    md5('load-item-' || g)::uuid,
    md5('load-comanda-' || (1 + ((g - 1) / :items_per_order)))::uuid,
    md5('load-product-' || (1 + (g % :products)))::uuid,
    NULL,
    'LOAD Produto ' || (1 + (g % :products)),
    1000 + (g % 15000),
    500 + (g % 5000),
    1 + (g % 4),
    (1000 + (g % 15000)) * (1 + (g % 4)),
    now() - ((g % 120) || ' days')::interval - ((g % 86400) || ' seconds')::interval,
    md5('load-user-' || (1 + (g % :users)))::uuid
FROM generate_series(1, :orders * :items_per_order) AS g
ON CONFLICT (id) DO NOTHING;

\echo 'Populando vendas avulsas...'
INSERT INTO vendas_avulsas (
    id, items_json, total_in_cents, payment_method, second_payment_method,
    second_payment_amount_in_cents, client_name, sold_at, sold_by_admin_id,
    sold_by_admin_name, user_id, user_name, discount_percent,
    discount_in_cents, fiscal_effects_captured_at, points_debited_at_sale,
    cashback_debited_at_sale, points_awarded_at_sale, crediario_id_at_sale,
    crediario_amount_at_sale, cancelado_em)
SELECT
    md5('load-sale-' || g)::uuid,
    jsonb_build_array(jsonb_build_object(
        'ProductId', md5('load-product-' || (1 + (g % :products)))::uuid,
        'ProductName', 'LOAD Produto ' || (1 + (g % :products)),
        'ProductCategory', 'LOAD Categoria ' || (g % 50),
        'Quantity', 1 + (g % 4),
        'UnitPriceInCents', 1000 + (g % 15000),
        'SubtotalInCents', (1000 + (g % 15000)) * (1 + (g % 4)),
        'UnitCostInCents', 500 + (g % 5000),
        'VariantId', NULL,
        'VariantLabel', NULL)),
    1000 + (g % 30000),
    CASE g % 4 WHEN 0 THEN 'Pix' WHEN 1 THEN 'Dinheiro'
         WHEN 2 THEN 'CartaoCredito' ELSE 'CartaoDebito' END,
    NULL, 0, 'LOAD Cliente ' || g,
    now() - ((g % 120) || ' days')::interval - ((g % 86400) || ' seconds')::interval,
    COALESCE((SELECT id FROM users WHERE role = 'Admin' LIMIT 1), md5('load-user-1')::uuid),
    'LOAD Admin',
    md5('load-user-' || (1 + (g % :users)))::uuid,
    'LOAD Usuário ' || (1 + (g % :users)),
    0, 0, now(), 0, 0, g % 100, NULL, 0, NULL
FROM generate_series(1, :sales) AS g
ON CONFLICT (id) DO NOTHING;

ANALYZE users;
ANALYZE products;
ANALYZE comandas;
ANALYZE comanda_items;
ANALYZE vendas_avulsas;

\echo 'Massa final por tabela:'
SELECT 'users' tabela, count(*) linhas FROM users WHERE name LIKE 'LOAD %'
UNION ALL SELECT 'products', count(*) FROM products WHERE name LIKE 'LOAD %'
UNION ALL SELECT 'comandas', count(*) FROM comandas WHERE table_identifier LIKE 'LOAD-%'
UNION ALL SELECT 'comanda_items', count(*) FROM comanda_items WHERE item_name_snapshot LIKE 'LOAD %'
UNION ALL SELECT 'vendas_avulsas', count(*) FROM vendas_avulsas WHERE client_name LIKE 'LOAD %';
