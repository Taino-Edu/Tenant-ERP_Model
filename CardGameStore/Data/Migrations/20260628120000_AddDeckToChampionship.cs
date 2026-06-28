using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    public partial class AddDeckToChampionship : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "deck_id",
                table: "championship_pre_inscricoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deck_name",
                table: "championship_pre_inscricoes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deck_id",
                table: "championship_participants",
                type: "uuid",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "deck_id",   table: "championship_pre_inscricoes");
            migrationBuilder.DropColumn(name: "deck_name", table: "championship_pre_inscricoes");
            migrationBuilder.DropColumn(name: "deck_id",   table: "championship_participants");
        }
    }
}
