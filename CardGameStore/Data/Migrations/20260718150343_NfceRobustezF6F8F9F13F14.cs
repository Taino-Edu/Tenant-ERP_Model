using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class NfceRobustezF6F8F9F13F14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notas_fiscais_comanda",
                table: "notas_fiscais_emitidas");

            migrationBuilder.AddColumn<DateTime>(
                name: "autorizado_em",
                table: "notas_fiscais_emitidas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protocolo_cancelamento",
                table: "notas_fiscais_emitidas",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "xml_evento_cancelamento",
                table: "notas_fiscais_emitidas",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_comanda_unica",
                table: "notas_fiscais_emitidas",
                column: "comanda_id",
                unique: true,
                filter: "comanda_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_venda_avulsa_unica",
                table: "notas_fiscais_emitidas",
                column: "venda_avulsa_id",
                unique: true,
                filter: "venda_avulsa_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notas_fiscais_comanda_unica",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropIndex(
                name: "ix_notas_fiscais_venda_avulsa_unica",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "autorizado_em",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "protocolo_cancelamento",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "xml_evento_cancelamento",
                table: "notas_fiscais_emitidas");

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_comanda",
                table: "notas_fiscais_emitidas",
                column: "comanda_id");
        }
    }
}
