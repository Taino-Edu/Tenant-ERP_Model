using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIbptTabelaLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ibpt_tabela",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ncm = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    importado = table.Column<bool>(type: "boolean", nullable: false),
                    percentual_federal = table.Column<decimal>(type: "numeric", nullable: false),
                    percentual_estadual = table.Column<decimal>(type: "numeric", nullable: false),
                    percentual_municipal = table.Column<decimal>(type: "numeric", nullable: false),
                    fonte = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    versao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    chave = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    vigencia_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    vigencia_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ibpt_tabela", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ibpt_tabela_ncm_uf_origem",
                table: "ibpt_tabela",
                columns: new[] { "ncm", "uf", "importado" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ibpt_tabela");
        }
    }
}
