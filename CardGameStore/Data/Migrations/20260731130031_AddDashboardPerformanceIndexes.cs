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
            migrationBuilder.CreateIndex(
                name: "ix_users_active_role_created_at",
                table: "users",
                columns: new[] { "is_active", "role", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_products_marketplace_active_name",
                table: "products",
                columns: new[] { "is_active", "show_on_marketplace", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_comandas_status_closed_at",
                table: "comandas",
                columns: new[] { "status", "closed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_comandas_user_opened_at",
                table: "comandas",
                columns: new[] { "user_id", "opened_at" });

            migrationBuilder.CreateIndex(
                name: "ix_comanda_items_added_at",
                table: "comanda_items",
                column: "added_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_active_role_created_at",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_products_marketplace_active_name",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_comandas_status_closed_at",
                table: "comandas");

            migrationBuilder.DropIndex(
                name: "ix_comandas_user_opened_at",
                table: "comandas");

            migrationBuilder.DropIndex(
                name: "ix_comanda_items_added_at",
                table: "comanda_items");
        }
    }
}
