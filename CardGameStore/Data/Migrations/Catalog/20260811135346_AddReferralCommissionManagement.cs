using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddReferralCommissionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "referral_partners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    document = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    pix_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    setup_commission_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    monthly_commission_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    payment_day = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_partners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_referrals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_lead_id = table.Column<Guid>(type: "uuid", nullable: true),
                    setup_commission_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    monthly_commission_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    monthly_commission_cycles = table.Column<int>(type: "integer", nullable: true),
                    started_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_referrals", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_referrals_leads_source_lead_id",
                        column: x => x.source_lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_referrals_referral_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "referral_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_referrals_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "referral_commissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    commission_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    reference_month = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    earned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_commissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_referral_commissions_tenant_charges_tenant_charge_id",
                        column: x => x.tenant_charge_id,
                        principalTable: "tenant_charges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_referral_commissions_tenant_referrals_referral_id",
                        column: x => x.referral_id,
                        principalTable: "tenant_referrals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_referral_commissions_charge_unique",
                table: "referral_commissions",
                column: "tenant_charge_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_referral_commissions_due_date",
                table: "referral_commissions",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_referral_commissions_reference_month",
                table: "referral_commissions",
                column: "reference_month");

            migrationBuilder.CreateIndex(
                name: "IX_referral_commissions_referral_id",
                table: "referral_commissions",
                column: "referral_id");

            migrationBuilder.CreateIndex(
                name: "ix_referral_partners_document_unique",
                table: "referral_partners",
                column: "document",
                unique: true,
                filter: "document IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_referral_partners_name",
                table: "referral_partners",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_referrals_partner_id",
                table: "tenant_referrals",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_referrals_source_lead_id",
                table: "tenant_referrals",
                column: "source_lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_referrals_tenant_unique",
                table: "tenant_referrals",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referral_commissions");

            migrationBuilder.DropTable(
                name: "tenant_referrals");

            migrationBuilder.DropTable(
                name: "referral_partners");
        }
    }
}
