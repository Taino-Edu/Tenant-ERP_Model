using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddPersistentProspectingWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prospecting_searches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cache_key = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    result_count = table.Column<int>(type: "integer", nullable: false),
                    south = table.Column<double>(type: "double precision", nullable: false),
                    west = table.Column<double>(type: "double precision", nullable: false),
                    north = table.Column<double>(type: "double precision", nullable: false),
                    east = table.Column<double>(type: "double precision", nullable: false),
                    osm_area_id = table.Column<long>(type: "bigint", nullable: true),
                    warning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    refreshed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prospecting_searches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prospect_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    search_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    website = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    digital_presence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opportunity_score = table.Column<int>(type: "integer", nullable: false),
                    estimated_revenue_range = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prospect_candidates", x => x.id);
                    table.ForeignKey(
                        name: "FK_prospect_candidates_prospecting_searches_search_id",
                        column: x => x.search_id,
                        principalTable: "prospecting_searches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prospect_candidates_lead_id",
                table: "prospect_candidates",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_prospect_candidates_search_source_unique",
                table: "prospect_candidates",
                columns: new[] { "search_id", "source", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prospect_candidates_status",
                table: "prospect_candidates",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_prospecting_searches_cache_key",
                table: "prospecting_searches",
                column: "cache_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prospecting_searches_refreshed_at",
                table: "prospecting_searches",
                column: "refreshed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prospect_candidates");

            migrationBuilder.DropTable(
                name: "prospecting_searches");
        }
    }
}
