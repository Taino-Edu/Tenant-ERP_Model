using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    public partial class AddMissingIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_crediarios_vencimento
                    ON crediarios(data_vencimento);

                CREATE INDEX IF NOT EXISTS idx_comandas_status_closed
                    ON comandas(status, closed_at);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS idx_crediarios_vencimento;
                DROP INDEX IF EXISTS idx_comandas_status_closed;
            ");
        }
    }
}
