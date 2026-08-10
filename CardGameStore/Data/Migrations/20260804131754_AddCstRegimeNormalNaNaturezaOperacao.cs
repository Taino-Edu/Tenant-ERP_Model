using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCstRegimeNormalNaNaturezaOperacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "aliquota_cofins",
                table: "naturezas_operacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "aliquota_fcp",
                table: "naturezas_operacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "aliquota_pis",
                table: "naturezas_operacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "base_st_retida_centavos",
                table: "naturezas_operacao",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cst",
                table: "naturezas_operacao",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cst_cofins",
                table: "naturezas_operacao",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cst_pis",
                table: "naturezas_operacao",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_reducao_bc",
                table: "naturezas_operacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "valor_st_retido_centavos",
                table: "naturezas_operacao",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aliquota_cofins",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "aliquota_fcp",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "aliquota_pis",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "base_st_retida_centavos",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "cst",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "cst_cofins",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "cst_pis",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "percentual_reducao_bc",
                table: "naturezas_operacao");

            migrationBuilder.DropColumn(
                name: "valor_st_retido_centavos",
                table: "naturezas_operacao");
        }
    }
}
