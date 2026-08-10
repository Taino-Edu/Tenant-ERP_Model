-- Papel sem privilégios administrativos usado pelo catálogo durante requests.
-- O usuário tenant_test continua sendo o administrador de migrations/testes.
DO $do$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'cardgame_catalog') THEN
    CREATE ROLE cardgame_catalog LOGIN PASSWORD 'cardgame_catalog_pw'
      NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  END IF;
END
$do$;

GRANT CONNECT ON DATABASE tenant_erp_test TO cardgame_catalog;
