using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNfeReceivingAndStockLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "estoque_recebido_em",
                table: "notas_destinadas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "itens_estoque_recebidos",
                table: "notas_destinadas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "nfe_receipt_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nota_destinada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_number = table.Column<int>(type: "integer", nullable: false),
                    supplier_product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_cost_in_cents = table.Column<int>(type: "integer", nullable: false),
                    ignored = table.Column<bool>(type: "boolean", nullable: false),
                    ignore_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nfe_receipt_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_nfe_receipt_items_notas_destinadas_nota_destinada_id",
                        column: x => x.nota_destinada_id,
                        principalTable: "notas_destinadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nfe_receipt_items_product_variants_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nfe_receipt_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    movement_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity_delta = table.Column<int>(type: "integer", nullable: false),
                    stock_before = table.Column<int>(type: "integer", nullable: false),
                    stock_after = table.Column<int>(type: "integer", nullable: false),
                    unit_cost_in_cents = table.Column<int>(type: "integer", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nfe_key = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    source_item_number = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_movements_product_variants_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_product_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    supplier_product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    supplier_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gtin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_unit_cost_in_cents = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_product_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_product_links_product_variants_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_supplier_product_links_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nfe_receipt_items_note_number",
                table: "nfe_receipt_items",
                columns: new[] { "nota_destinada_id", "item_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nfe_receipt_items_product_id",
                table: "nfe_receipt_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_nfe_receipt_items_product_variant_id",
                table: "nfe_receipt_items",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_nfe_item",
                table: "stock_movements",
                columns: new[] { "nfe_key", "source_item_number" },
                unique: true,
                filter: "nfe_key IS NOT NULL AND source_item_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_product_date",
                table: "stock_movements",
                columns: new[] { "product_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_product_variant_id",
                table: "stock_movements",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_product_links_product_id",
                table: "supplier_product_links",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_product_links_product_variant_id",
                table: "supplier_product_links",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_product_links_supplier_code",
                table: "supplier_product_links",
                columns: new[] { "supplier_cnpj", "supplier_product_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nfe_receipt_items");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "supplier_product_links");

            migrationBuilder.DropColumn(
                name: "estoque_recebido_em",
                table: "notas_destinadas");

            migrationBuilder.DropColumn(
                name: "itens_estoque_recebidos",
                table: "notas_destinadas");
        }
    }
}
