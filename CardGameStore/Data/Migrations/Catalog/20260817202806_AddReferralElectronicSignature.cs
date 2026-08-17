using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddReferralElectronicSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "contract_email_verified_at",
                table: "referral_partners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_evidence_id",
                table: "referral_partners",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_evidence_sha256",
                table: "referral_partners",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "contract_pdf",
                table: "referral_partners",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_pdf_sha256",
                table: "referral_partners",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_acceptance_json",
                table: "referral_partner_invitations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "signature_code_attempts",
                table: "referral_partner_invitations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "signature_code_expires_at",
                table: "referral_partner_invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature_code_hash",
                table: "referral_partner_invitations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "signature_code_send_count",
                table: "referral_partner_invitations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "signature_code_sent_at",
                table: "referral_partner_invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE referral_partners
                SET partner_kind = 'Parceiro de indicação'
                WHERE partner_kind = 'Vendedor';

                UPDATE referral_partner_invitations
                SET partner_kind = 'Parceiro de indicação'
                WHERE partner_kind = 'Vendedor';

                ALTER TABLE referral_partners
                ALTER COLUMN partner_kind SET DEFAULT 'Parceiro de indicação';

                ALTER TABLE referral_partner_invitations
                ALTER COLUMN partner_kind SET DEFAULT 'Parceiro de indicação';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE referral_partners
                ALTER COLUMN partner_kind SET DEFAULT 'Vendedor';

                ALTER TABLE referral_partner_invitations
                ALTER COLUMN partner_kind SET DEFAULT 'Vendedor';
                """);

            migrationBuilder.DropColumn(
                name: "contract_email_verified_at",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "contract_evidence_id",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "contract_evidence_sha256",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "contract_pdf",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "contract_pdf_sha256",
                table: "referral_partners");

            migrationBuilder.DropColumn(
                name: "pending_acceptance_json",
                table: "referral_partner_invitations");

            migrationBuilder.DropColumn(
                name: "signature_code_attempts",
                table: "referral_partner_invitations");

            migrationBuilder.DropColumn(
                name: "signature_code_expires_at",
                table: "referral_partner_invitations");

            migrationBuilder.DropColumn(
                name: "signature_code_hash",
                table: "referral_partner_invitations");

            migrationBuilder.DropColumn(
                name: "signature_code_send_count",
                table: "referral_partner_invitations");

            migrationBuilder.DropColumn(
                name: "signature_code_sent_at",
                table: "referral_partner_invitations");
        }
    }
}
