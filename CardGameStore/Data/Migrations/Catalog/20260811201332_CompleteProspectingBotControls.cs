using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class CompleteProspectingBotControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "daily_run_budget",
                table: "prospecting_campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "max_retry_attempts",
                table: "prospecting_campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "prospecting_campaign_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_attempt_at",
                table: "prospecting_campaign_runs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.CreateTable(
                name: "prospect_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    previous_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    observed_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    confidence = table.Column<int>(type: "integer", nullable: false),
                    observed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prospect_observations", x => x.id);
                    table.ForeignKey(
                        name: "FK_prospect_observations_prospect_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "prospect_candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prospect_suppressions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    normalized_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prospect_suppressions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prospect_observations_candidate_date",
                table: "prospect_observations",
                columns: new[] { "candidate_id", "observed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_prospect_suppressions_key_unique",
                table: "prospect_suppressions",
                columns: new[] { "key_type", "normalized_value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prospect_observations");

            migrationBuilder.DropTable(
                name: "prospect_suppressions");

            migrationBuilder.DropColumn(
                name: "daily_run_budget",
                table: "prospecting_campaigns");

            migrationBuilder.DropColumn(
                name: "max_retry_attempts",
                table: "prospecting_campaigns");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "prospecting_campaign_runs");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "prospecting_campaign_runs");
        }
    }
}
