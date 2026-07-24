using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddLeadDigitalPresenceScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "digital_presence",
                table: "leads",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "opportunity_score",
                table: "leads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "place_id",
                table: "leads",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "digital_presence",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "opportunity_score",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "place_id",
                table: "leads");
        }
    }
}
