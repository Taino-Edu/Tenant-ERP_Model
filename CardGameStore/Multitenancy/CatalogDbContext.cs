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
    public DbSet<ContadorAccount> ContadorAccounts { get; set; }
    public DbSet<ContadorTenantLink> ContadorTenantLinks { get; set; }
    public DbSet<ContadorAviso> ContadorAvisos { get; set; }
    public DbSet<ContadorConviteEmail> ContadorConvitesEmail { get; set; }

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

        modelBuilder.Entity<ContadorAccount>(entity =>
        {
            entity.HasIndex(c => c.Email)
                  .IsUnique()
                  .HasDatabaseName("ix_contador_accounts_email");
        });

        modelBuilder.Entity<ContadorTenantLink>(entity =>
        {
            entity.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);

            entity.HasOne<Tenant>()
                  .WithMany()
                  .HasForeignKey(l => l.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<ContadorAccount>()
                  .WithMany()
                  .HasForeignKey(l => l.ContadorAccountId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(l => new { l.ContadorAccountId, l.TenantId })
                  .IsUnique()
                  .HasDatabaseName("ix_contador_tenant_links_pair");
        });

        modelBuilder.Entity<ContadorAviso>(entity =>
        {
            entity.HasOne<ContadorTenantLink>()
                  .WithMany()
                  .HasForeignKey(a => a.ContadorTenantLinkId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => a.ContadorTenantLinkId)
                  .HasDatabaseName("ix_contador_avisos_link_id");
        });

        modelBuilder.Entity<ContadorConviteEmail>(entity =>
        {
            entity.HasOne<Tenant>()
                  .WithMany()
                  .HasForeignKey(c => c.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => new { c.Email, c.TenantId })
                  .IsUnique()
                  .HasDatabaseName("ix_contador_convites_email_pair");
        });
    }
}
