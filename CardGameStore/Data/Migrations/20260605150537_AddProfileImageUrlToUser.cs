using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileImageUrlToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Usa SQL direto com IF NOT EXISTS para não falhar se as colunas
            // já existirem (criadas via EnsureCreated ou SQL manual anterior).
            migrationBuilder.Sql("ALTER TABLE users ADD COLUMN IF NOT EXISTS profile_image_url TEXT;");
            migrationBuilder.Sql("ALTER TABLE championships ADD COLUMN IF NOT EXISTS image_url TEXT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "profile_image_url",
                table: "users");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "championships");
        }
    }
}
