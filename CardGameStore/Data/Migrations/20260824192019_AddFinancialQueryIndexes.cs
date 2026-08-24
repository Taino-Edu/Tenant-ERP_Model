using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_ext_tx_dre_type_due_date",
                table: "external_transactions",
                columns: new[] { "dre_group", "type", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_ext_tx_status_due_date",
                table: "external_transactions",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_crediarios_status_vencimento",
                table: "crediarios",
                columns: new[] { "status", "data_vencimento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ext_tx_dre_type_due_date",
                table: "external_transactions");

            migrationBuilder.DropIndex(
                name: "ix_ext_tx_status_due_date",
                table: "external_transactions");

            migrationBuilder.DropIndex(
                name: "ix_crediarios_status_vencimento",
                table: "crediarios");
        }
    }
}
