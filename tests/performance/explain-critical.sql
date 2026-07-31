\set ON_ERROR_STOP on
SET search_path TO :"schema";

\echo 'Q1 comandas fechadas 30 dias'
EXPLAIN (ANALYZE, BUFFERS, WAL, SETTINGS, FORMAT TEXT)
SELECT COALESCE(sum(total_in_cents), 0), count(*)
FROM comandas
WHERE status = 'Fechada' AND closed_at >= now() - interval '30 days';

\echo 'Q2 top produtos por itens 30 dias'
EXPLAIN (ANALYZE, BUFFERS, WAL, SETTINGS, FORMAT TEXT)
SELECT item_name_snapshot, sum(quantity), sum(unit_price_in_cents * quantity)
FROM comanda_items
WHERE added_at >= now() - interval '30 days'
GROUP BY item_name_snapshot
ORDER BY sum(quantity) DESC
LIMIT 5;

\echo 'Q3 comandas recentes de um cliente'
EXPLAIN (ANALYZE, BUFFERS, WAL, SETTINGS, FORMAT TEXT)
SELECT id, status, total_in_cents, closed_at
FROM comandas
WHERE user_id = md5('load-user-42')::uuid
ORDER BY opened_at DESC
LIMIT 20;

\echo 'Q4 catálogo público ordenado'
EXPLAIN (ANALYZE, BUFFERS, WAL, SETTINGS, FORMAT TEXT)
SELECT id, name, price_in_cents, stock_quantity
FROM products
WHERE is_active = true AND show_on_marketplace = true
ORDER BY name
LIMIT 50;
