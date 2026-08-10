using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """CREATE INDEX CONCURRENTLY IF NOT EXISTS "ix_users_active_role_created_at" ON "users" ("is_active", "role", "created_at");""",
                suppressTransaction: true);
            migrationBuilder.Sql(
                """CREATE INDEX CONCURRENTLY IF NOT EXISTS "ix_products_marketplace_active_name" ON "products" ("is_active", "show_on_marketplace", "name");""",
                suppressTransaction: true);
            migrationBuilder.Sql(
                """CREATE INDEX CONCURRENTLY IF NOT EXISTS "ix_comandas_status_closed_at" ON "comandas" ("status", "closed_at");""",
                suppressTransaction: true);
            migrationBuilder.Sql(
                """CREATE INDEX CONCURRENTLY IF NOT EXISTS "ix_comandas_user_opened_at" ON "comandas" ("user_id", "opened_at");""",
                suppressTransaction: true);
            migrationBuilder.Sql(
                """CREATE INDEX CONCURRENTLY IF NOT EXISTS "ix_comanda_items_added_at" ON "comanda_items" ("added_at");""",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS "ix_users_active_role_created_at";""",
                suppressTransaction: true);
            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS "ix_products_marketplace_active_name";""",
                suppressTransaction: true);
            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS "ix_comandas_status_closed_at";""",
                suppressTransaction: true);
            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS "ix_comandas_user_opened_at";""",
                suppressTransaction: true);
            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS "ix_comanda_items_added_at";""",
                suppressTransaction: true);
        }
    }
}
