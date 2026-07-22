using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIbptAutomaticTaxFill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ibpt_chave",
                table: "products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ibpt_versao",
                table: "products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "tributos_atualizados_em",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "tributos_preenchidos_automaticamente",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "tributos_vigencia_fim",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "tributos_vigencia_inicio",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ibpt_auto_sync_enabled",
                table: "fiscal_config",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ibpt_token_encrypted",
                table: "fiscal_config",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ibpt_ultima_sincronizacao",
                table: "fiscal_config",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ibpt_ultima_versao",
                table: "fiscal_config",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ibpt_ultimo_erro",
                table: "fiscal_config",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ibpt_vigencia_fim",
                table: "fiscal_config",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ibpt_vigencia_inicio",
                table: "fiscal_config",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ibpt_chave",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ibpt_versao",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tributos_atualizados_em",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tributos_preenchidos_automaticamente",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tributos_vigencia_fim",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tributos_vigencia_inicio",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ibpt_auto_sync_enabled",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "ibpt_token_encrypted",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "ibpt_ultima_sincronizacao",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "ibpt_ultima_versao",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "ibpt_ultimo_erro",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "ibpt_vigencia_fim",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "ibpt_vigencia_inicio",
                table: "fiscal_config");
        }
    }
}
