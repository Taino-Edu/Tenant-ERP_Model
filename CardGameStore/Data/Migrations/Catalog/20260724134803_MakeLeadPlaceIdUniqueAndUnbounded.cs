using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class MakeLeadPlaceIdUniqueAndUnbounded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "place_id",
                table: "leads",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_leads_place_id_unique",
                table: "leads",
                column: "place_id",
                unique: true,
                filter: "place_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_leads_place_id_unique",
                table: "leads");

            migrationBuilder.AlterColumn<string>(
                name: "place_id",
                table: "leads",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
