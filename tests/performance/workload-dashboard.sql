BEGIN;
SET LOCAL search_path TO tenant_santuario_nerd;

SELECT COALESCE(sum(total_in_cents), 0), count(*)
FROM comandas
WHERE status = 'Fechada' AND closed_at >= now() - interval '30 days';

SELECT item_name_snapshot, sum(quantity), sum(unit_price_in_cents * quantity)
FROM comanda_items
WHERE added_at >= now() - interval '30 days'
GROUP BY item_name_snapshot
ORDER BY sum(quantity) DESC
LIMIT 5;

SELECT count(*)
FROM users
WHERE is_active = true AND role = 'Customer';

SELECT COALESCE(sum(total_in_cents), 0), count(*)
FROM vendas_avulsas
WHERE sold_at >= now() - interval '60 days';

COMMIT;
