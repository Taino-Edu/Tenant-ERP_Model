using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerfilIbsCbsNaFiscalConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "excedeu_sublimite_simples",
                table: "fiscal_config",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "optou_regime_regular_ibs_cbs",
                table: "fiscal_config",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "excedeu_sublimite_simples",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "optou_regime_regular_ibs_cbs",
                table: "fiscal_config");
        }
    }
}
