\set product_no random(1, 20000)
\set user_no random(1, 10000)

BEGIN;
SET LOCAL search_path TO tenant_santuario_nerd;

SELECT id, name, category, price_in_cents, stock_quantity
FROM products
WHERE id = md5('load-product-' || :product_no)::uuid;

SELECT id, name, price_in_cents, stock_quantity
FROM products
WHERE is_active = true AND show_on_marketplace = true
ORDER BY name
LIMIT 50;

SELECT id, status, total_in_cents, closed_at
FROM comandas
WHERE user_id = md5('load-user-' || :user_no)::uuid
ORDER BY opened_at DESC
LIMIT 20;

COMMIT;
