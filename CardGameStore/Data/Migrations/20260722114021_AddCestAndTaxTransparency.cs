using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCestAndTaxTransparency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cest",
                table: "products",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fonte_tributos",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_tributos_estaduais",
                table: "products",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_tributos_federais",
                table: "products",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_tributos_municipais",
                table: "products",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fontes_tributos",
                table: "notas_fiscais_emitidas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tributos_estaduais_em_centavos",
                table: "notas_fiscais_emitidas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tributos_federais_em_centavos",
                table: "notas_fiscais_emitidas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tributos_itens_json",
                table: "notas_fiscais_emitidas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tributos_municipais_em_centavos",
                table: "notas_fiscais_emitidas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_cest_formato",
                table: "products",
                sql: "cest IS NULL OR cest ~ '^[0-9]{7}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_tributos_federais_percentual",
                table: "products",
                sql: "percentual_tributos_federais IS NULL OR percentual_tributos_federais BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_tributos_estaduais_percentual",
                table: "products",
                sql: "percentual_tributos_estaduais IS NULL OR percentual_tributos_estaduais BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_tributos_municipais_percentual",
                table: "products",
                sql: "percentual_tributos_municipais IS NULL OR percentual_tributos_municipais BETWEEN 0 AND 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_products_cest_formato",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_tributos_federais_percentual",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_tributos_estaduais_percentual",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_tributos_municipais_percentual",
                table: "products");

            migrationBuilder.DropColumn(
                name: "cest",
                table: "products");

            migrationBuilder.DropColumn(
                name: "fonte_tributos",
                table: "products");

            migrationBuilder.DropColumn(
                name: "percentual_tributos_estaduais",
                table: "products");

            migrationBuilder.DropColumn(
                name: "percentual_tributos_federais",
                table: "products");

            migrationBuilder.DropColumn(
                name: "percentual_tributos_municipais",
                table: "products");

            migrationBuilder.DropColumn(
                name: "fontes_tributos",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "tributos_estaduais_em_centavos",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "tributos_federais_em_centavos",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "tributos_itens_json",
                table: "notas_fiscais_emitidas");

            migrationBuilder.DropColumn(
                name: "tributos_municipais_em_centavos",
                table: "notas_fiscais_emitidas");
        }
    }
}
