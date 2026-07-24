using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class EncryptCscToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "csc_token",
                table: "fiscal_config");

            migrationBuilder.AddColumn<string>(
                name: "csc_token_encrypted",
                table: "fiscal_config",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "csc_token_encrypted",
                table: "fiscal_config");

            migrationBuilder.AddColumn<string>(
                name: "csc_token",
                table: "fiscal_config",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
