using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFechamentoPeriodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fechamentos_periodo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    receita_comandas = table.Column<long>(type: "bigint", nullable: false),
                    receita_avulsa = table.Column<long>(type: "bigint", nullable: false),
                    custo_comandas = table.Column<long>(type: "bigint", nullable: false),
                    custo_avulsa = table.Column<long>(type: "bigint", nullable: false),
                    margem = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fechamentos_periodo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fechamentos_periodo_janela",
                table: "fechamentos_periodo",
                columns: new[] { "tipo", "data_inicio", "data_fim" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fechamentos_periodo");
        }
    }
}
