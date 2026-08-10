using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResultadoIncertoNfce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "resultado_incerto_em",
                table: "notas_fiscais_emitidas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tentativa_id",
                table: "notas_fiscais_emitidas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "xml_tentativa",
                table: "notas_fiscais_emitidas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "resultado_incerto_em",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "tentativa_id",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "xml_tentativa",
                table: "notas_fiscais_emitidas");
        }
    }
}
