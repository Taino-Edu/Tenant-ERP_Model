using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class MarcaSuspensaoPelaRegua : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "suspended_by_billing",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Quem já está suspenso e marcado como Atrasado foi derrubado pela
            // régua: sem este preenchimento, essas lojas nunca voltariam sozinhas
            // ao quitar — a coluna nasceria false e a régua as trataria como
            // suspensão manual. É a mesma condição que a régua usava antes.
            migrationBuilder.Sql("""
                UPDATE public.tenants
                   SET suspended_by_billing = true
                 WHERE status = 'Suspended' AND payment_status = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "suspended_by_billing",
                table: "tenants");
        }
    }
}
