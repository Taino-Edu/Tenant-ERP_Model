// =============================================================================
// CatalogDbContext.cs — Contexto leve e independente do AppDbContext.
// Sem interceptor de search_path — sempre opera no schema "public", porque
// resolver o schema de um tenant a partir do slug não pode depender do
// schema ainda não resolvido (chicken-and-egg).
// =============================================================================

using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

            entity.HasIndex(t => t.Slug)
                  .IsUnique()
                  .HasDatabaseName("ix_tenants_slug");

            entity.HasIndex(t => t.SchemaName)
                  .IsUnique()
                  .HasDatabaseName("ix_tenants_schema_name");
        });
    }
}
