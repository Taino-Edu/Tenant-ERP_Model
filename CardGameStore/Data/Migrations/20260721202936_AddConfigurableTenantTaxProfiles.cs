using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurableTenantTaxProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "aliquota_fcp_st",
                table: "naturezas_operacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "aliquota_icms_proprio",
                table: "naturezas_operacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "aliquota_icms_st",
                table: "naturezas_operacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "base_st_fixa_centavos",
                table: "naturezas_operacao",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ibs_cbs_class_trib",
                table: "naturezas_operacao",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "000001");

            migrationBuilder.AddColumn<string>(
                name: "ibs_cbs_cst",
                table: "naturezas_operacao",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "000");

            migrationBuilder.AddColumn<int>(
                name: "modalidade_bc_st",
                table: "naturezas_operacao",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "origem_mercadoria",
                table: "naturezas_operacao",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_mva_st",
                table: "naturezas_operacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_reducao_bc_st",
                table: "naturezas_operacao",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aliquota_fcp_st",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "aliquota_icms_proprio",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "aliquota_icms_st",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "base_st_fixa_centavos",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "ibs_cbs_class_trib",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "ibs_cbs_cst",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "modalidade_bc_st",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "origem_mercadoria",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "percentual_mva_st",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "percentual_reducao_bc_st",
                table: "naturezas_operacao");
        }
    }
}
