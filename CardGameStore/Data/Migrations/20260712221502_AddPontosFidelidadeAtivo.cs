using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPontosFidelidadeAtivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true — lojas que já existem e já usam pontos precisam
            // continuar exatamente como estão após o deploy (o default do modelo
            // C# não vira DEFAULT de coluna sozinho; sem isso a coluna nasceria
            // "false" pra toda linha existente, desligando pontos de todo mundo).
            migrationBuilder.AddColumn<bool>(
                name: "pontos_fidelidade_ativo",
                table: "site_config",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pontos_fidelidade_ativo",
                table: "site_config");
        }
    }
}
