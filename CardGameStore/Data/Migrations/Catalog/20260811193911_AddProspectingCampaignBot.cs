using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddProspectingCampaignBot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "enrichment_confidence",
                table: "prospect_candidates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "enrichment_source",
                table: "prospect_candidates",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "enrichment_status",
                table: "prospect_candidates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_enriched_at",
                table: "prospect_candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suggested_approach",
                table: "prospect_candidates",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "prospecting_campaigns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    interval_hours = table.Column<int>(type: "integer", nullable: false),
                    max_candidates_per_run = table.Column<int>(type: "integer", nullable: false),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prospecting_campaigns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prospecting_campaign_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    search_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    discovered_count = table.Column<int>(type: "integer", nullable: false),
                    new_count = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prospecting_campaign_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_prospecting_campaign_runs_prospecting_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalTable: "prospecting_campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prospecting_campaign_runs_active_unique",
                table: "prospecting_campaign_runs",
                column: "campaign_id",
                unique: true,
                filter: "\"status\" IN ('Queued', 'Running')");

            migrationBuilder.CreateIndex(
                name: "ix_prospecting_campaign_runs_queue",
                table: "prospecting_campaign_runs",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_prospecting_campaigns_due",
                table: "prospecting_campaigns",
                columns: new[] { "status", "next_run_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prospecting_campaign_runs");

            migrationBuilder.DropTable(
                name: "prospecting_campaigns");

            migrationBuilder.DropColumn(
                name: "enrichment_confidence",
                table: "prospect_candidates");

            migrationBuilder.DropColumn(
                name: "enrichment_source",
                table: "prospect_candidates");

            migrationBuilder.DropColumn(
                name: "enrichment_status",
                table: "prospect_candidates");

            migrationBuilder.DropColumn(
                name: "last_enriched_at",
                table: "prospect_candidates");

            migrationBuilder.DropColumn(
                name: "suggested_approach",
                table: "prospect_candidates");
        }
    }
}
