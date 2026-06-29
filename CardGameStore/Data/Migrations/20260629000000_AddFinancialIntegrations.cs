using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    public partial class AddFinancialIntegrations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS external_transactions (
                    id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
                    source      VARCHAR(30)     NOT NULL DEFAULT 'manual',
                    external_id VARCHAR(200)    NULL,
                    type        VARCHAR(10)     NOT NULL DEFAULT 'expense',
                    amount      NUMERIC(10,2)   NOT NULL DEFAULT 0,
                    description VARCHAR(500)    NOT NULL DEFAULT '',
                    due_date    TIMESTAMPTZ     NULL,
                    paid_at     TIMESTAMPTZ     NULL,
                    status      VARCHAR(20)     NOT NULL DEFAULT 'pending',
                    category    VARCHAR(100)    NULL,
                    supplier    VARCHAR(200)    NULL,
                    nfe_key     VARCHAR(44)     NULL,
                    notes       VARCHAR(2000)   NULL,
                    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
                    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_ext_tx_source_external_id
                    ON external_transactions (source, external_id)
                    WHERE external_id IS NOT NULL;

                CREATE TABLE IF NOT EXISTS integration_configs (
                    id             UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                    source         VARCHAR(30)  NOT NULL UNIQUE,
                    access_token   VARCHAR(2000) NULL,
                    refresh_token  VARCHAR(2000) NULL,
                    client_id      VARCHAR(200) NULL,
                    client_secret  VARCHAR(200) NULL,
                    expires_at     TIMESTAMPTZ  NULL,
                    is_active      BOOLEAN      NOT NULL DEFAULT TRUE,
                    cnpj           VARCHAR(18)  NULL,
                    last_sync_at   TIMESTAMPTZ  NULL,
                    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    updated_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS external_transactions;
                DROP TABLE IF EXISTS integration_configs;
            ");
        }
    }
}
