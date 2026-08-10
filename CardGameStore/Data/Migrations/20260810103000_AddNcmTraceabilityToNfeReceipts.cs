using CardGameStore.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260810103000_AddNcmTraceabilityToNfeReceipts")]
    public partial class AddNcmTraceabilityToNfeReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_ncm",
                table: "nfe_receipt_items",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // Desativa novas operações de fidelidade sem apagar saldos ou
            // histórico, que permanecem necessários para auditoria e estorno.
            migrationBuilder.Sql("UPDATE site_config SET pontos_fidelidade_ativo = FALSE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_ncm",
                table: "nfe_receipt_items");
        }
    }
}
