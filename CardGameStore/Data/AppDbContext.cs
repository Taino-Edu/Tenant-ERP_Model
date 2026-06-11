// =============================================================================
// AppDbContext.cs — Contexto do Entity Framework Core (PostgreSQL)
// Configura mapeamentos, índices, conversões e seeds iniciais.
// =============================================================================

using CardGameStore.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Data;

/// <summary>
/// Contexto principal do EF Core para o banco PostgreSQL.
/// Gerencia todas as entidades relacionais da aplicação.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // -------------------------------------------------------------------------
    // DbSets — Mapeamento das entidades para tabelas
    // -------------------------------------------------------------------------

    public DbSet<User>                    Users                    { get; set; }
    public DbSet<Product>                 Products                 { get; set; }
    public DbSet<ProductCategory>         ProductCategories        { get; set; }
    public DbSet<Comanda>                 Comandas                 { get; set; }
    public DbSet<ComandaItem>             ComandaItems             { get; set; }
    public DbSet<Championship>            Championships            { get; set; }
    public DbSet<ChampionshipParticipant>  ChampionshipParticipants  { get; set; }
    public DbSet<ChampionshipPreInscricao> ChampionshipPreInscricoes { get; set; }
    public DbSet<Announcement>            Announcements            { get; set; }
    public DbSet<Crediario>               Crediarios               { get; set; }
    public DbSet<PagamentoCrediario>      PagamentosCrediario      { get; set; }

    // ── LGPD — Compliance e privacidade ──────────────────────────────────────
    public DbSet<LgpdRequest>   LgpdRequests   { get; set; }
    public DbSet<CookieConsent> CookieConsents { get; set; }
    public DbSet<AuditLog>      AuditLogs      { get; set; }

    // -------------------------------------------------------------------------
    // OnModelCreating — Fluent API para configurações avançadas
    // -------------------------------------------------------------------------

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================================================
        // USER
        // =====================================================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email)
                  .IsUnique()
                  .HasFilter("email IS NOT NULL")
                  .HasDatabaseName("ix_users_email");

            entity.HasIndex(u => u.Cpf)
                  .IsUnique()
                  .HasFilter("cpf IS NOT NULL")
                  .HasDatabaseName("ix_users_cpf");

            entity.HasIndex(u => u.WhatsApp)
                  .HasDatabaseName("ix_users_whatsapp");
        });

        // =====================================================================
        // PRODUCT
        // =====================================================================
        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasIndex(c => c.Name)
                  .IsUnique()
                  .HasDatabaseName("ix_product_categories_name");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.Category)
                  .HasDatabaseName("ix_products_category");

            entity.HasIndex(p => p.IsActive)
                  .HasDatabaseName("ix_products_is_active");

            entity.HasIndex(p => p.Barcode)
                  .IsUnique()
                  .HasFilter("barcode IS NOT NULL")
                  .HasDatabaseName("ix_products_barcode");
        });

        // =====================================================================
        // COMANDA
        // =====================================================================
        modelBuilder.Entity<Comanda>(entity =>
        {
            entity.Property(c => c.Status)
                  .HasConversion<string>();

            entity.HasIndex(c => new { c.UserId, c.Status })
                  .HasDatabaseName("ix_comandas_user_status");

            entity.HasIndex(c => c.Status)
                  .HasDatabaseName("ix_comandas_status");

            entity.HasIndex(c => c.ChampionshipId)
                  .HasDatabaseName("ix_comandas_championship");

            entity.HasOne(c => c.User)
                  .WithMany(u => u.Comandas)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Championship)
                  .WithMany(ch => ch.Comandas)
                  .HasForeignKey(c => c.ChampionshipId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // =====================================================================
        // COMANDA ITEM
        // =====================================================================
        modelBuilder.Entity<ComandaItem>(entity =>
        {
            entity.HasIndex(i => i.ComandaId)
                  .HasDatabaseName("ix_comanda_items_comanda");

            entity.HasOne(i => i.Comanda)
                  .WithMany(c => c.Items)
                  .HasForeignKey(i => i.ComandaId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Product)
                  .WithMany(p => p.ComandaItems)
                  .HasForeignKey(i => i.ProductId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired(false);
        });

        // =====================================================================
        // CHAMPIONSHIP
        // =====================================================================
        modelBuilder.Entity<Championship>(entity =>
        {
            entity.Property(c => c.Status)
                  .HasConversion<string>();

            entity.HasIndex(c => c.Status)
                  .HasDatabaseName("ix_championships_status");

            entity.HasIndex(c => c.StartDate)
                  .HasDatabaseName("ix_championships_start_date");
        });

        // =====================================================================
        // CHAMPIONSHIP PARTICIPANT
        // =====================================================================
        modelBuilder.Entity<ChampionshipParticipant>(entity =>
        {
            entity.HasIndex(p => new { p.ChampionshipId, p.UserId })
                  .IsUnique()
                  .HasDatabaseName("ix_championship_participants_unique");

            entity.HasOne(p => p.Championship)
                  .WithMany(c => c.Participants)
                  .HasForeignKey(p => p.ChampionshipId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.User)
                  .WithMany(u => u.ChampionshipParticipants)
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Comanda)
                  .WithMany()
                  .HasForeignKey(p => p.ComandaId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // =====================================================================
        // CREDIARIO
        // =====================================================================
        modelBuilder.Entity<Crediario>(entity =>
        {
            entity.Property(c => c.Status)
                  .HasConversion<string>();

            entity.HasIndex(c => new { c.UserId, c.Status })
                  .HasDatabaseName("ix_crediarios_user_status");

            entity.HasIndex(c => c.Status)
                  .HasDatabaseName("ix_crediarios_status");

            entity.HasOne(c => c.User)
                  .WithMany()
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Comanda)
                  .WithMany()
                  .HasForeignKey(c => c.ComandaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(c => c.Pagamentos)
                  .WithOne(p => p.Crediario)
                  .HasForeignKey(p => p.CrediarioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================================
        // PAGAMENTO CREDIARIO
        // =====================================================================
        modelBuilder.Entity<PagamentoCrediario>(entity =>
        {
            entity.HasIndex(p => p.CrediarioId)
                  .HasDatabaseName("ix_pagamentos_crediario_crediario");

            entity.HasIndex(p => p.CreatedAt)
                  .HasDatabaseName("ix_pagamentos_crediario_created_at");
        });

        // =====================================================================
        // LGPD REQUEST
        // =====================================================================
        modelBuilder.Entity<LgpdRequest>(entity =>
        {
            // Busca por status (lista de requisições abertas)
            entity.HasIndex(r => r.Status)
                  .HasDatabaseName("ix_lgpd_requests_status");

            // Busca por email do solicitante
            entity.HasIndex(r => r.RequesterEmail)
                  .HasDatabaseName("ix_lgpd_requests_email");

            // Relacionamento opcional com User (CPF pode não existir no cadastro)
            entity.HasOne(r => r.User)
                  .WithMany()
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // =====================================================================
        // COOKIE CONSENT
        // =====================================================================
        modelBuilder.Entity<CookieConsent>(entity =>
        {
            entity.HasIndex(c => c.ConsentAt)
                  .HasDatabaseName("ix_cookie_consents_consent_at");

            entity.HasOne(c => c.User)
                  .WithMany()
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // =====================================================================
        // AUDIT LOG
        // =====================================================================
        modelBuilder.Entity<AuditLog>(entity =>
        {
            // Busca por entidade afetada
            entity.HasIndex(a => new { a.EntityType, a.EntityId })
                  .HasDatabaseName("ix_audit_logs_entity");

            // Busca por ator
            entity.HasIndex(a => a.ActorUserId)
                  .HasDatabaseName("ix_audit_logs_actor");

            // Ordenação por data (query mais frequente)
            entity.HasIndex(a => a.CreatedAt)
                  .HasDatabaseName("ix_audit_logs_created_at");
        });

    }
}
