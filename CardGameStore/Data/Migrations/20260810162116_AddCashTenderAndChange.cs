using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashTenderAndChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cash_received_in_cents",
                table: "vendas_avulsas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cash_rounding_discount_in_cents",
                table: "vendas_avulsas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "change_in_cents",
                table: "vendas_avulsas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cash_received_in_cents",
                table: "pagamentos_crediario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cash_rounding_discount_in_cents",
                table: "pagamentos_crediario",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "change_in_cents",
                table: "pagamentos_crediario",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cash_received_in_cents",
                table: "comandas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cash_rounding_discount_in_cents",
                table: "comandas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "change_in_cents",
                table: "comandas",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cash_received_in_cents",
                table: "vendas_avulsas");

            migrationBuilder.DropColumn(
                name: "cash_rounding_discount_in_cents",
                table: "vendas_avulsas");

            migrationBuilder.DropColumn(
                name: "change_in_cents",
                table: "vendas_avulsas");

            migrationBuilder.DropColumn(
                name: "cash_received_in_cents",
                table: "pagamentos_crediario");

            migrationBuilder.DropColumn(
                name: "cash_rounding_discount_in_cents",
                table: "pagamentos_crediario");

            migrationBuilder.DropColumn(
                name: "change_in_cents",
                table: "pagamentos_crediario");

            migrationBuilder.DropColumn(
                name: "cash_received_in_cents",
                table: "comandas");

            migrationBuilder.DropColumn(
                name: "cash_rounding_discount_in_cents",
                table: "comandas");

            migrationBuilder.DropColumn(
                name: "change_in_cents",
                table: "comandas");
        }
    }
}
