using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddCondicoesComerciais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "billing_due_day",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "setup_first_due_date",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "setup_installments",
                table: "tenants",
                type: "integer",
                nullable: false,
                // 1, não o 0 que o EF gera: toda loja existente cobra a
                // implantação (quando tem) de uma vez só, como sempre cobrou.
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "auto_generated",
                table: "tenant_charges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "tenant_charges",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "discount_summary",
                table: "tenant_charges",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gross_amount",
                table: "tenant_charges",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "installment_count",
                table: "tenant_charges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "installment_number",
                table: "tenant_charges",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_billing_discounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    start_month = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_month = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_billing_discounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_billing_discounts_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_billing_discounts_tenant",
                table: "tenant_billing_discounts",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_billing_discounts");

            migrationBuilder.DropColumn(
                name: "billing_due_day",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "setup_first_due_date",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "setup_installments",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "auto_generated",
                table: "tenant_charges");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "tenant_charges");

            migrationBuilder.DropColumn(
                name: "discount_summary",
                table: "tenant_charges");

            migrationBuilder.DropColumn(
                name: "gross_amount",
                table: "tenant_charges");

            migrationBuilder.DropColumn(
                name: "installment_count",
                table: "tenant_charges");

            migrationBuilder.DropColumn(
                name: "installment_number",
                table: "tenant_charges");
        }
    }
}
