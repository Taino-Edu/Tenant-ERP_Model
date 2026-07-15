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
    public DbSet<PlatformImpersonationTicket> PlatformImpersonationTickets { get; set; }
    public DbSet<LoginRedirectTicket> LoginRedirectTickets { get; set; }
    public DbSet<Lead> Leads { get; set; }
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<SupportTicketMessage> SupportTicketMessages { get; set; }

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

        modelBuilder.Entity<PlatformImpersonationTicket>(entity =>
        {
            entity.HasIndex(t => t.Ticket)
                  .IsUnique()
                  .HasDatabaseName("ix_platform_impersonation_tickets_ticket");
        });

        modelBuilder.Entity<LoginRedirectTicket>(entity =>
        {
            entity.HasIndex(t => t.Ticket)
                  .IsUnique()
                  .HasDatabaseName("ix_login_redirect_tickets_ticket");
        });

        modelBuilder.Entity<Lead>(entity =>
        {
            entity.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);

            entity.HasIndex(l => l.Status)
                  .HasDatabaseName("ix_leads_status");

            entity.HasIndex(l => l.CreatedAt)
                  .HasDatabaseName("ix_leads_created_at");
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

            // Sem FK pra Tenant, de propósito: o tenant-zero (admin do domínio raiz,
            // pré-multitenancy) tem TenantId = Guid.Empty e nunca teve linha no
            // catálogo — uma FK aqui quebraria "abrir chamado" pra ele com
            // violação de chave estrangeira. Mesmo padrão de
            // PlatformImpersonationTicket.TenantId (também sem FK).
            entity.HasIndex(t => t.TenantId)
                  .HasDatabaseName("ix_support_tickets_tenant_id");

            entity.HasIndex(t => t.Status)
                  .HasDatabaseName("ix_support_tickets_status");
        });

        modelBuilder.Entity<SupportTicketMessage>(entity =>
        {
            entity.Property(m => m.AuthorRole).HasConversion<string>().HasMaxLength(20);

            entity.HasOne<SupportTicket>()
                  .WithMany(t => t.Messages)
                  .HasForeignKey(m => m.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(m => m.TicketId)
                  .HasDatabaseName("ix_support_ticket_messages_ticket_id");
        });
    }
}
