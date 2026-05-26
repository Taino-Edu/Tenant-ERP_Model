using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodToComanda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<int>(
                name: "balance_in_cents",
                table: "users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cost_price_in_cents",
                table: "products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "comanda_id",
                table: "crediarios",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "valor_pago_em_centavos",
                table: "crediarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "comandas",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pagamentos_crediario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    crediario_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    valor_em_centavos = table.Column<int>(type: "INTEGER", nullable: false),
                    forma_pagamento = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    observacao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    admin_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamentos_crediario", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagamentos_crediario_crediarios_crediario_id",
                        column: x => x.crediario_id,
                        principalTable: "crediarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    emoji = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    display_order = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_crediario_created_at",
                table: "pagamentos_crediario",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_crediario_crediario",
                table: "pagamentos_crediario",
                column: "crediario_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_name",
                table: "product_categories",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagamentos_crediario");

            migrationBuilder.DropTable(
                name: "product_categories");

            migrationBuilder.DropColumn(
                name: "balance_in_cents",
                table: "users");

            migrationBuilder.DropColumn(
                name: "cost_price_in_cents",
                table: "products");

            migrationBuilder.DropColumn(
                name: "valor_pago_em_centavos",
                table: "crediarios");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "comandas");

            migrationBuilder.AlterColumn<Guid>(
                name: "comanda_id",
                table: "crediarios",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "consent_at", "cpf", "created_at", "deleted_at", "email", "is_active", "name", "password_hash", "password_reset_token", "password_reset_token_expiry", "points_balance", "points_expires_at", "refresh_token", "refresh_token_expiry", "role", "updated_at", "whatsapp" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "admin@cardgamestore.com.br", true, "Maikon", "$2b$12$2FtwJhHP3JiQZjXb.f19B.ulmS5t2jdIQbKAPhPYP22AT.0ptsVPC", null, null, 0, null, null, null, "Admin", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null });
        }
    }
}
