using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CardGameStore.Multitenancy;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog;

/// <summary>
/// Comandas existiam antes de virarem um módulo. Mantém o acesso dos tenants
/// atuais no deploy; tenants novos recebem o módulo conforme o plano escolhido.
/// </summary>
[DbContext(typeof(CatalogDbContext))]
[Migration("20260802120000_EnableRestauranteForExistingTenants")]
public partial class EnableRestauranteForExistingTenants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE tenants
               SET enabled_modules = array_append(enabled_modules, 'restaurante')
             WHERE NOT ('restaurante' = ANY(enabled_modules));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE tenants
               SET enabled_modules = array_remove(enabled_modules, 'restaurante');
            """);
    }
}
