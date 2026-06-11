using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    public partial class AddChampionshipPodioAndPreInscricoes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "podio_json",
                table: "championships",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "championship_preinscricoes",
                columns: table => new
                {
                    id             = table.Column<Guid>   (type: "uuid",                   nullable: false),
                    championship_id= table.Column<Guid>   (type: "uuid",                   nullable: false),
                    nome           = table.Column<string> (type: "character varying(200)",  maxLength: 200,  nullable: false),
                    whatsapp       = table.Column<string> (type: "character varying(30)",   maxLength: 30,   nullable: false),
                    created_at     = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_championship_preinscricoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_championship_preinscricoes_championships_championship_id",
                        column: x => x.championship_id,
                        principalTable: "championships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_championship_preinscricoes_championship_id",
                table: "championship_preinscricoes",
                column: "championship_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "championship_preinscricoes");
            migrationBuilder.DropColumn(name: "podio_json", table: "championships");
        }
    }
}
