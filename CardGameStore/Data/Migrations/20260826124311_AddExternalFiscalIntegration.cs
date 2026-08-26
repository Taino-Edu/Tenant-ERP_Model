using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalFiscalIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_document_id",
                table: "notas_fiscais_emitidas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_payload_json",
                table: "notas_fiscais_emitidas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_source",
                table: "notas_fiscais_emitidas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "notas_fiscais_emitidas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_external_document",
                table: "notas_fiscais_emitidas",
                columns: new[] { "external_source", "external_document_id" },
                unique: true,
                filter: "external_source IS NOT NULL AND external_document_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_idempotency_key",
                table: "notas_fiscais_emitidas",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notas_fiscais_external_document",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropIndex(
                name: "ix_notas_fiscais_idempotency_key",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "external_document_id",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "external_payload_json",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "external_source",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "notas_fiscais_emitidas");
        }
    }
}
