using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddTenantBillingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default != vazio: tenants já existentes no catálogo não perdem acesso ao
            // módulo fiscal (que já usavam livremente) só por essa coluna passar a existir.
            migrationBuilder.AddColumn<string[]>(
                name: "enabled_modules",
                table: "tenants",
                type: "text[]",
                nullable: false,
                defaultValue: new[] { "fiscal" });

            migrationBuilder.AddColumn<int>(
                name: "payment_status",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "plan_name",
                table: "tenants",
                type: "character varying(63)",
                maxLength: 63,
                nullable: false,
                defaultValue: "Completo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "enabled_modules",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "plan_name",
                table: "tenants");
        }
    }
}
