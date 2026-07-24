using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    data_evento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    preco_entrada_in_cents = table.Column<int>(type: "integer", nullable: false),
                    capacidade_maxima = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evento_entradas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_cliente = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    forma_pagamento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor_pago_in_cents = table.Column<int>(type: "integer", nullable: false),
                    check_in_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    vendida_por_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendida_por_admin_nome = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evento_entradas", x => x.id);
                    table.ForeignKey(
                        name: "FK_evento_entradas_eventos_evento_id",
                        column: x => x.evento_id,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evento_entradas_evento_id",
                table: "evento_entradas",
                column: "evento_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_data_evento",
                table: "eventos",
                column: "data_evento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evento_entradas");

            migrationBuilder.DropTable(
                name: "eventos");
        }
    }
}
