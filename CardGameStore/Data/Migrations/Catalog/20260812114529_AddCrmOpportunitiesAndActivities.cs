using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddCrmOpportunitiesAndActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crm_opportunities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    probability = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    expected_close_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_user_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    lost_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_opportunities", x => x.id);
                    table.ForeignKey(
                        name: "FK_crm_opportunities_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crm_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activities", x => x.id);
                    table.ForeignKey(
                        name: "FK_crm_activities_crm_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalTable: "crm_opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_crm_activities_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_crm_activities_lead_date",
                table: "crm_activities",
                columns: new[] { "lead_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_crm_activities_open_due",
                table: "crm_activities",
                columns: new[] { "completed_at", "due_at" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_activities_opportunity_id",
                table: "crm_activities",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_crm_opportunities_expected_close",
                table: "crm_opportunities",
                column: "expected_close_date");

            migrationBuilder.CreateIndex(
                name: "ix_crm_opportunities_lead_unique",
                table: "crm_opportunities",
                column: "lead_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_crm_opportunities_stage_owner",
                table: "crm_opportunities",
                columns: new[] { "stage", "assigned_user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crm_activities");

            migrationBuilder.DropTable(
                name: "crm_opportunities");
        }
    }
}
