using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddCrmAnalyticsAndPrivacyAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "anonymized_at",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "converted_at",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "retention_review_flagged_at",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "stage_entered_at",
                table: "crm_opportunities",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("""
                UPDATE leads SET converted_at = updated_at
                WHERE converted_tenant_id IS NOT NULL OR status = 'Convertido';
                UPDATE crm_opportunities SET stage_entered_at = updated_at;
                """);

            migrationBuilder.CreateTable(
                name: "lead_privacy_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    actor_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details_json = table.Column<string>(type: "text", nullable: false),
                    previous_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    event_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_privacy_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_lead_privacy_events_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lead_privacy_events_hash_unique",
                table: "lead_privacy_events",
                column: "event_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lead_privacy_events_lead_date",
                table: "lead_privacy_events",
                columns: new[] { "lead_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lead_privacy_events");

            migrationBuilder.DropColumn(
                name: "anonymized_at",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "converted_at",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "retention_review_flagged_at",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "stage_entered_at",
                table: "crm_opportunities");
        }
    }
}
