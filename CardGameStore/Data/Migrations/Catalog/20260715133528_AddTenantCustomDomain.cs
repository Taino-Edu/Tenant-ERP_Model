using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddTenantCustomDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "custom_domain",
                table: "tenants",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_custom_domain",
                table: "tenants",
                column: "custom_domain",
                unique: true,
                filter: "custom_domain IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_custom_domain",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "custom_domain",
                table: "tenants");
        }
    }
}
