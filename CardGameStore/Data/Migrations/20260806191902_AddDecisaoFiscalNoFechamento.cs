using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisaoFiscalNoFechamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "fiscal_decisao_em",
                table: "vendas_avulsas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fiscal_decisao_por_user_id",
                table: "vendas_avulsas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "fiscal_emissao_escolhida",
                table: "vendas_avulsas",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fiscal_decisao_em",
                table: "comandas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fiscal_decisao_por_user_id",
                table: "comandas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "fiscal_emissao_escolhida",
                table: "comandas",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fiscal_decisao_em",
                table: "vendas_avulsas");

            migrationBuilder.DropColumn(
                name: "fiscal_decisao_por_user_id",
                table: "vendas_avulsas");

            migrationBuilder.DropColumn(
                name: "fiscal_emissao_escolhida",
                table: "vendas_avulsas");

            migrationBuilder.DropColumn(
                name: "fiscal_decisao_em",
                table: "comandas");

            migrationBuilder.DropColumn(
                name: "fiscal_decisao_por_user_id",
                table: "comandas");

            migrationBuilder.DropColumn(
                name: "fiscal_emissao_escolhida",
                table: "comandas");
        }
    }
}
