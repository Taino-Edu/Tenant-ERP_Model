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
    public DbSet<Comanda>                 Comandas                 { get; set; }
    public DbSet<ComandaItem>             ComandaItems             { get; set; }
    public DbSet<Championship>            Championships            { get; set; }
    public DbSet<ChampionshipParticipant> ChampionshipParticipants { get; set; }

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
            // Índice único no e-mail (quando preenchido)
            entity.HasIndex(u => u.Email)
                  .IsUnique()
                  .HasFilter("email IS NOT NULL")
                  .HasDatabaseName("ix_users_email");

            // Índice único no CPF (quando preenchido)
            entity.HasIndex(u => u.Cpf)
                  .IsUnique()
                  .HasFilter("cpf IS NOT NULL")
                  .HasDatabaseName("ix_users_cpf");

            // Índice no WhatsApp para consultas de login rápido
            entity.HasIndex(u => u.WhatsApp)
                  .HasDatabaseName("ix_users_whatsapp");
        });

        // =====================================================================
        // PRODUCT
        // =====================================================================
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.Category)
                  .HasDatabaseName("ix_products_category");

            entity.HasIndex(p => p.IsActive)
                  .HasDatabaseName("ix_products_is_active");
        });

        // =====================================================================
        // COMANDA
        // =====================================================================
        modelBuilder.Entity<Comanda>(entity =>
        {
            // Converte o enum ComandaStatus para string no banco (legibilidade)
            entity.Property(c => c.Status)
                  .HasConversion<string>();

            // Índice para buscar comandas abertas de um usuário (query frequente)
            entity.HasIndex(c => new { c.UserId, c.Status })
                  .HasDatabaseName("ix_comandas_user_status");

            // Índice para o dashboard do Admin (todas as abertas/em andamento)
            entity.HasIndex(c => c.Status)
                  .HasDatabaseName("ix_comandas_status");

            // Índice para vincular a campeonatos
            entity.HasIndex(c => c.ChampionshipId)
                  .HasDatabaseName("ix_comandas_championship");

            // Relacionamento: uma Comanda pertence a um User
            entity.HasOne(c => c.User)
                  .WithMany(u => u.Comandas)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict); // Não apaga comanda ao deletar usuário

            // Relacionamento: uma Comanda pode estar num Campeonato
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
            // Índice para carregar todos os itens de uma comanda
            entity.HasIndex(i => i.ComandaId)
                  .HasDatabaseName("ix_comanda_items_comanda");

            entity.HasOne(i => i.Comanda)
                  .WithMany(c => c.Items)
                  .HasForeignKey(i => i.ComandaId)
                  .OnDelete(DeleteBehavior.Cascade); // Remove itens ao remover comanda

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
            // Garante que um usuário só se inscreve uma vez por campeonato
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
        // SEED — Dados iniciais (usuário Admin do Maikon)
        // =====================================================================
        // ATENÇÃO: Em produção, substitua o hash pelo hash real gerado via BCrypt.
        // Exemplo: BCrypt.Net.BCrypt.HashPassword("SenhaForte@123")
        modelBuilder.Entity<User>().HasData(new User
        {
            Id           = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name         = "Maikon",
            Email        = "admin@cardgamestore.com.br",
            // Hash BCrypt da senha: SenhaForte@123  (gerado com rounds=12)
            PasswordHash = "$2b$12$2FtwJhHP3JiQZjXb.f19B.ulmS5t2jdIQbKAPhPYP22AT.0ptsVPC",
            Role         = UserRole.Admin,
            IsActive     = true,
            CreatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
