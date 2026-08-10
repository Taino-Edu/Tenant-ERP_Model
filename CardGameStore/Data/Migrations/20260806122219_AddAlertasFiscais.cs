using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertasFiscais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alertas_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    severidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    chave = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    detalhe = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    nota_fiscal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    detectado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ocorrencias = table.Column<int>(type: "integer", nullable: false),
                    responsavel_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsavel_definido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_por_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolucao_observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resolvido_automaticamente = table.Column<bool>(type: "boolean", nullable: false),
                    reaberto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reaberturas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas_fiscais", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_fiscais_chave",
                table: "alertas_fiscais",
                column: "chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alertas_fiscais_nota",
                table: "alertas_fiscais",
                column: "nota_fiscal_id");

            migrationBuilder.CreateIndex(
                name: "ix_alertas_fiscais_painel",
                table: "alertas_fiscais",
                columns: new[] { "resolvido_em", "severidade", "ocorrido_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas_fiscais");
        }
    }
}
