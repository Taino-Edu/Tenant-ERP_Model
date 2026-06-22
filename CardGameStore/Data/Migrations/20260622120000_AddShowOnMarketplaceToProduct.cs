using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    public partial class AddShowOnMarketplaceToProduct : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE products
                    ADD COLUMN IF NOT EXISTS show_on_marketplace INTEGER NOT NULL DEFAULT 1;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE products
                    DROP COLUMN IF EXISTS show_on_marketplace;
            ");
        }
    }
}
