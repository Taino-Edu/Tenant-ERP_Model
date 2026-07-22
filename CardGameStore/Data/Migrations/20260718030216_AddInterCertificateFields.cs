using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterCertificateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "certificate_crt_encrypted",
                table: "integration_configs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_key_encrypted",
                table: "integration_configs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "certificate_crt_encrypted",
                table: "integration_configs");

            migrationBuilder.DropColumn(
                name: "certificate_key_encrypted",
                table: "integration_configs");
        }
    }
}
