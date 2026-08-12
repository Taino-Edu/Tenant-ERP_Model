using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddLeadAttributionAndPrivacyGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "campaign",
                table: "leads",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "data_origin_details",
                table: "leads",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "landing_page",
                table: "leads",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_basis",
                table: "leads",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "legitimate_interest_assessed_at",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "opposed_at",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "opposition_reason",
                table: "leads",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "privacy_notice_acknowledged_at",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "privacy_notice_version",
                table: "leads",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processing_purpose",
                table: "leads",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "referral_partner_id",
                table: "leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referrer_url",
                table: "leads",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "retention_review_at",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_campaign",
                table: "leads",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_content",
                table: "leads",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_medium",
                table: "leads",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_source",
                table: "leads",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_term",
                table: "leads",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE leads
                SET data_origin_details = CASE
                        WHEN origem = 'prospeccao' THEN 'Dados profissionais públicos localizados pelo módulo de prospecção'
                        ELSE 'Registro comercial anterior à política 2.1; origem detalhada pendente de revisão'
                    END,
                    processing_purpose = CASE
                        WHEN origem = 'prospeccao' THEN 'Qualificar potencial cliente empresarial e avaliar contato comercial pertinente'
                        ELSE 'Gerenciar relacionamento comercial e possível contratação da plataforma'
                    END,
                    legal_basis = CASE WHEN origem = 'prospeccao' THEN 'LegitimoInteresse' ELSE 'NaoDefinida' END,
                    retention_review_at = created_at + INTERVAL '180 days';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_leads_referral_partner_id",
                table: "leads",
                column: "referral_partner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_leads_referral_partners_referral_partner_id",
                table: "leads",
                column: "referral_partner_id",
                principalTable: "referral_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_leads_referral_partners_referral_partner_id",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "ix_leads_referral_partner_id",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "campaign",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "data_origin_details",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "landing_page",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "legal_basis",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "legitimate_interest_assessed_at",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "opposed_at",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "opposition_reason",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "privacy_notice_acknowledged_at",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "privacy_notice_version",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "processing_purpose",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "referral_partner_id",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "referrer_url",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "retention_review_at",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "utm_campaign",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "utm_content",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "utm_medium",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "utm_source",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "utm_term",
                table: "leads");
        }
    }
}
