using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNfeReceivingAndProfessionalDre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dre_group",
                table: "external_transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "unclassified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dre_group",
                table: "external_transactions");
        }
    }
}
