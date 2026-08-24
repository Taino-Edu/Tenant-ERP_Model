using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_fee_percent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    commission_percent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    freight_percent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    expected_daily_net_cash = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    minimum_cash_reserve = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_config", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_config");
        }
    }
}
