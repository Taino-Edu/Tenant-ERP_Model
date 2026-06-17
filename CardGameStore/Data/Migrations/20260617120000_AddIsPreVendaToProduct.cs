using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    public partial class AddIsPreVendaToProduct : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE products ADD COLUMN IF NOT EXISTS is_pre_venda boolean NOT NULL DEFAULT false;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE products DROP COLUMN IF EXISTS is_pre_venda;");
        }
    }
}
