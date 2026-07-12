using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddContadorAvisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contador_avisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contador_tenant_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mensagem = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contador_avisos", x => x.id);
                    table.ForeignKey(
                        name: "FK_contador_avisos_contador_tenant_links_contador_tenant_link_~",
                        column: x => x.contador_tenant_link_id,
                        principalTable: "contador_tenant_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contador_avisos_link_id",
                table: "contador_avisos",
                column: "contador_tenant_link_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contador_avisos");
        }
    }
}
