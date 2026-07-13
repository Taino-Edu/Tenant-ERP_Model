using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnrichAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "channel",
                table: "audit_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // defaultValue: "Info" (não "") de propósito — audit_logs já tem
            // linhas reais em produção; um NOT NULL com default "" faria o EF
            // apanhar tentando converter "" de volta pro enum AuditSeverity ao
            // ler essas linhas antigas. "Info" é o valor neutro correto pra
            // tudo que foi logado antes de existir gravidade.
            migrationBuilder.AddColumn<string>(
                name: "severity",
                table: "audit_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Info");

            migrationBuilder.AddColumn<string>(
                name: "target_user_id",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trace_id",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_trace_id",
                table: "audit_logs",
                column: "trace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_trace_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "channel",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "target_user_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "trace_id",
                table: "audit_logs");
        }
    }
}
