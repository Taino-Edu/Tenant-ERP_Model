using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddReferralPartnerInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "contract_accepted_at",
                table: "referral_partners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_accepted_ip_hash",
                table: "referral_partners",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_accepted_user_agent",
                table: "referral_partners",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_text",
                table: "referral_partners",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_version",
                table: "referral_partners",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fiscal_document_type",
                table: "referral_partners",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "RPA");

            migrationBuilder.AddColumn<string>(
                name: "partner_kind",
                table: "referral_partners",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Vendedor");

            migrationBuilder.AddColumn<int>(
                name: "payment_grace_days",
                table: "referral_partners",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "person_type",
                table: "referral_partners",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "PF");

            migrationBuilder.AddColumn<string>(
                name: "professional_registration",
                table: "referral_partners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fiscal_document_reference",
                table: "referral_commissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "referral_partner_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    partner_kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    setup_commission_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    monthly_commission_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    payment_grace_days = table.Column<int>(type: "integer", nullable: false),
                    contract_version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    contract_text = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_partner_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_referral_partner_invitations_referral_partners_accepted_par~",
                        column: x => x.accepted_partner_id,
                        principalTable: "referral_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_referral_partner_invitations_accepted_partner_id",
                table: "referral_partner_invitations",
                column: "accepted_partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_referral_partner_invitations_email",
                table: "referral_partner_invitations",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_referral_partner_invitations_token_unique",
                table: "referral_partner_invitations",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referral_partner_invitations");

            migrationBuilder.DropColumn(
                name: "contract_accepted_at",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "contract_accepted_ip_hash",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "contract_accepted_user_agent",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "contract_text",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "contract_version",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "fiscal_document_type",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "partner_kind",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "payment_grace_days",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "person_type",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "professional_registration",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "fiscal_document_reference",
                table: "referral_commissions");
        }
    }
}
