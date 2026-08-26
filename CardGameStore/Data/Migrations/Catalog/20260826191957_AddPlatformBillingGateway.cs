using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddPlatformBillingGateway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "billing_cnpj",
                table: "tenants",
                type: "character varying(18)",
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "billing_customer_id",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "billing_email",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_charge_id",
                table: "tenant_charges",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gateway",
                table: "tenant_charges",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_url",
                table: "tenant_charges",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_charges_gateway_external_id",
                table: "tenant_charges",
                columns: new[] { "gateway", "external_charge_id" },
                unique: true,
                filter: "external_charge_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenant_charges_gateway_external_id",
                table: "tenant_charges");

            migrationBuilder.DropColumn(
                name: "billing_cnpj",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "billing_customer_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "billing_email",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "external_charge_id",
                table: "tenant_charges");

            migrationBuilder.DropColumn(
                name: "gateway",
                table: "tenant_charges");

            migrationBuilder.DropColumn(
                name: "payment_url",
                table: "tenant_charges");
        }
    }
}
