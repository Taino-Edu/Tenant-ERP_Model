using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    public partial class AddProductGalleryAndFullDescription : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE products
                    ADD COLUMN IF NOT EXISTS image_urls text[] NOT NULL DEFAULT '{}',
                    ADD COLUMN IF NOT EXISTS full_description text;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE products
                    DROP COLUMN IF EXISTS image_urls,
                    DROP COLUMN IF EXISTS full_description;
            ");
        }
    }
}
