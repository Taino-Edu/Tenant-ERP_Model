// =============================================================================
// CatalogDbContext.cs — Contexto leve e independente do AppDbContext.
// Sem interceptor de search_path — sempre opera no schema "public", porque
// resolver o schema de um tenant a partir do slug não pode depender do
// schema ainda não resolvido (chicken-and-egg).
// =============================================================================

using CardGameStore.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<ApiIntegrationClient> ApiIntegrationClients { get; set; }
    public DbSet<ContadorAccount> ContadorAccounts { get; set; }
    public DbSet<ContadorTenantLink> ContadorTenantLinks { get; set; }
    public DbSet<ContadorAviso> ContadorAvisos { get; set; }
    public DbSet<ContadorConviteEmail> ContadorConvitesEmail { get; set; }
    public DbSet<PlatformImpersonationTicket> PlatformImpersonationTickets { get; set; }
    public DbSet<LoginRedirectTicket> LoginRedirectTickets { get; set; }
    public DbSet<Lead> Leads { get; set; }
    public DbSet<CrmOpportunity> CrmOpportunities { get; set; }
    public DbSet<CrmActivity> CrmActivities { get; set; }
    public DbSet<LeadPrivacyEvent> LeadPrivacyEvents { get; set; }
    public DbSet<ProspectingSearch> ProspectingSearches { get; set; }
    public DbSet<ProspectCandidate> ProspectCandidates { get; set; }
    public DbSet<ProspectingCampaign> ProspectingCampaigns { get; set; }
    public DbSet<ProspectingCampaignRun> ProspectingCampaignRuns { get; set; }
    public DbSet<ProspectSuppression> ProspectSuppressions { get; set; }
    public DbSet<ProspectObservation> ProspectObservations { get; set; }
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<SupportTicketMessage> SupportTicketMessages { get; set; }
    public DbSet<TenantCharge> TenantCharges { get; set; }
    public DbSet<ReferralPartner> ReferralPartners { get; set; }
    public DbSet<ReferralPartnerInvitation> ReferralPartnerInvitations { get; set; }
    public DbSet<TenantReferral> TenantReferrals { get; set; }
    public DbSet<ReferralCommission> ReferralCommissions { get; set; }
    /// <summary>Tabela IBPT compartilhada por todos os tenants.</summary>
    public DbSet<IbptTabelaEntry> IbptTabela { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IbptTabelaEntry>(entity =>
        {
            entity.HasIndex(e => new { e.Ncm, e.Uf, e.Importado })
                  .IsUnique()
                  .HasDatabaseName("ix_ibpt_tabela_ncm_uf_origem");
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Kind).HasConversion<string>().HasMaxLength(30);

            entity.HasIndex(t => t.Slug)
                  .IsUnique()
                  .HasDatabaseName("ix_tenants_slug");

            entity.HasIndex(t => t.SchemaName)
                  .IsUnique()
                  .HasDatabaseName("ix_tenants_schema_name");

            entity.HasIndex(t => t.CustomDomain)
                  .IsUnique()
                  .HasFilter("custom_domain IS NOT NULL")
                  .HasDatabaseName("ix_tenants_custom_domain");
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
            entity.Property(l => l.LegalBasis).HasConversion<string>().HasMaxLength(40);

            entity.HasOne<ReferralPartner>().WithMany().HasForeignKey(l => l.ReferralPartnerId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(l => l.ReferralPartnerId)
                  .HasDatabaseName("ix_leads_referral_partner_id");

            entity.HasIndex(l => l.Status)
                  .HasDatabaseName("ix_leads_status");

            entity.HasIndex(l => l.CreatedAt)
                  .HasDatabaseName("ix_leads_created_at");

            // Filtrado (só quando PlaceId não é nulo) pra não impedir múltiplos
            // leads sem PlaceId (form da landing nunca tem PlaceId) — impede o
            // mesmo negócio do OpenStreetMap virar lead duplicado por busca
            // repetida/duplo-clique/retry (achado de review, PR #14).
            entity.HasIndex(l => l.PlaceId)
                  .IsUnique()
                  .HasFilter("place_id IS NOT NULL")
                  .HasDatabaseName("ix_leads_place_id_unique");
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

        modelBuilder.Entity<TenantCharge>(entity =>
        {
            entity.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);

            // FK com Cascade: excluir um tenant leva as cobranças dele junto.
            // Coerente com a exclusão permanente de tenant que já existe no
            // painel (PlatformController faz DROP SCHEMA) — deixar cobrança
            // órfã apontando pra tenant inexistente só sujaria os relatórios.
            entity.HasOne<Tenant>()
                  .WithMany()
                  .HasForeignKey(c => c.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            // A garantia central de integridade do billing: um tenant não pode
            // ter duas cobranças do mesmo tipo pra mesma competência. É isso
            // que torna o gerador de mensalidades idempotente — rodar duas
            // vezes no mesmo mês (ou clicar duas vezes no botão) não duplica a
            // cobrança, o banco recusa. Sem essa index, cobrança em duplicidade
            // seria só uma questão de tempo, e é o tipo de erro que o cliente
            // descobre antes da gente.
            entity.HasIndex(c => new { c.TenantId, c.Kind, c.ReferenceMonth })
                  .IsUnique()
                  .HasDatabaseName("ix_tenant_charges_tenant_kind_competencia");

            // Relatório mensal (quanto entrou em X) varre por competência;
            // cobranças em aberto/vencidas varrem por vencimento.
            entity.HasIndex(c => c.ReferenceMonth)
                  .HasDatabaseName("ix_tenant_charges_reference_month");

            // O webhook chega sabendo só (gateway, id externo) e precisa achar a
            // cobrança. Única e filtrada: única porque duas linhas apontando pro
            // mesmo id do gateway fariam a baixa cair na cobrança errada, e
            // filtrada porque a esmagadora maioria das linhas tem os dois campos
            // nulos (baixa manual, histórico) — sem o filtro, o segundo null
            // colidiria com o primeiro.
            entity.HasIndex(c => new { c.Gateway, c.ExternalChargeId })
                  .IsUnique()
                  .HasFilter("external_charge_id IS NOT NULL")
                  .HasDatabaseName("ix_tenant_charges_gateway_external_id");

            entity.HasIndex(c => c.DueDate)
                  .HasDatabaseName("ix_tenant_charges_due_date");
        });

        modelBuilder.Entity<ApiIntegrationClient>(entity =>
        {
            entity.HasOne<Tenant>()
                  .WithMany()
                  .HasForeignKey(item => item.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(item => item.ClientId)
                  .IsUnique()
                  .HasDatabaseName("ix_api_integration_clients_client_id");

            entity.HasIndex(item => new { item.TenantId, item.IsActive })
                  .HasDatabaseName("ix_api_integration_clients_tenant_active");
        });

        modelBuilder.Entity<LeadPrivacyEvent>(entity =>
        {
            entity.HasOne(e => e.Lead).WithMany(l => l.PrivacyEvents).HasForeignKey(e => e.LeadId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.LeadId, e.CreatedAt })
                  .HasDatabaseName("ix_lead_privacy_events_lead_date");
            entity.HasIndex(e => e.EventHash).IsUnique()
                  .HasDatabaseName("ix_lead_privacy_events_hash_unique");
        });

        modelBuilder.Entity<CrmOpportunity>(entity =>
        {
            entity.Property(o => o.Stage).HasConversion<string>().HasMaxLength(30);
            entity.HasOne(o => o.Lead).WithOne(l => l.Opportunity)
                  .HasForeignKey<CrmOpportunity>(o => o.LeadId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(o => o.LeadId).IsUnique()
                  .HasDatabaseName("ix_crm_opportunities_lead_unique");
            entity.HasIndex(o => new { o.Stage, o.AssignedUserId })
                  .HasDatabaseName("ix_crm_opportunities_stage_owner");
            entity.HasIndex(o => o.ExpectedCloseDate)
                  .HasDatabaseName("ix_crm_opportunities_expected_close");
        });

        modelBuilder.Entity<CrmActivity>(entity =>
        {
            entity.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
            entity.HasOne(a => a.Lead).WithMany(l => l.Activities)
                  .HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Opportunity).WithMany(o => o.Activities)
                  .HasForeignKey(a => a.OpportunityId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(a => new { a.LeadId, a.CreatedAt })
                  .HasDatabaseName("ix_crm_activities_lead_date");
            entity.HasIndex(a => new { a.CompletedAt, a.DueAt })
                  .HasDatabaseName("ix_crm_activities_open_due");
        });

        modelBuilder.Entity<ProspectingSearch>(entity =>
        {
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(s => s.CacheKey).IsUnique().HasDatabaseName("ix_prospecting_searches_cache_key");
            entity.HasIndex(s => s.RefreshedAt).HasDatabaseName("ix_prospecting_searches_refreshed_at");
        });

        modelBuilder.Entity<ProspectCandidate>(entity =>
        {
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(c => c.EnrichmentStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(c => c.Search).WithMany(s => s.Candidates).HasForeignKey(c => c.SearchId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(c => new { c.SearchId, c.Source, c.SourceId }).IsUnique()
                  .HasDatabaseName("ix_prospect_candidates_search_source_unique");
            entity.HasIndex(c => c.Status).HasDatabaseName("ix_prospect_candidates_status");
            entity.HasIndex(c => c.LeadId).HasDatabaseName("ix_prospect_candidates_lead_id");
        });

        modelBuilder.Entity<ProspectingCampaign>(entity =>
        {
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(c => new { c.Status, c.NextRunAt })
                  .HasDatabaseName("ix_prospecting_campaigns_due");
        });

        modelBuilder.Entity<ProspectingCampaignRun>(entity =>
        {
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(r => r.Campaign).WithMany(c => c.Runs).HasForeignKey(r => r.CampaignId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(r => new { r.Status, r.CreatedAt })
                  .HasDatabaseName("ix_prospecting_campaign_runs_queue");
            entity.HasIndex(r => r.CampaignId).IsUnique()
                  .HasFilter("\"status\" IN ('Queued', 'Running')")
                  .HasDatabaseName("ix_prospecting_campaign_runs_active_unique");
        });

        modelBuilder.Entity<ProspectSuppression>(entity =>
        {
            entity.Property(s => s.KeyType).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(s => new { s.KeyType, s.NormalizedValue }).IsUnique()
                  .HasDatabaseName("ix_prospect_suppressions_key_unique");
        });

        modelBuilder.Entity<ProspectObservation>(entity =>
        {
            entity.HasOne(o => o.Candidate).WithMany(c => c.Observations).HasForeignKey(o => o.CandidateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(o => new { o.CandidateId, o.ObservedAt })
                  .HasDatabaseName("ix_prospect_observations_candidate_date");
        });

        modelBuilder.Entity<ReferralPartner>(entity =>
        {
            entity.HasIndex(p => p.Name).HasDatabaseName("ix_referral_partners_name");
            entity.HasIndex(p => p.Document).IsUnique().HasFilter("document IS NOT NULL")
                  .HasDatabaseName("ix_referral_partners_document_unique");
        });

        modelBuilder.Entity<ReferralPartnerInvitation>(entity =>
        {
            entity.HasIndex(i => i.TokenHash).IsUnique()
                  .HasDatabaseName("ix_referral_partner_invitations_token_unique");
            entity.HasIndex(i => i.Email).HasDatabaseName("ix_referral_partner_invitations_email");
            entity.HasOne<ReferralPartner>().WithMany().HasForeignKey(i => i.AcceptedPartnerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TenantReferral>(entity =>
        {
            entity.HasOne<ReferralPartner>().WithMany().HasForeignKey(r => r.PartnerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Lead>().WithMany().HasForeignKey(r => r.SourceLeadId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(r => r.TenantId).IsUnique()
                  .HasDatabaseName("ix_tenant_referrals_tenant_unique");
            entity.HasIndex(r => r.PartnerId).HasDatabaseName("ix_tenant_referrals_partner_id");
        });

        modelBuilder.Entity<ReferralCommission>(entity =>
        {
            entity.Property(c => c.ChargeKind).HasConversion<string>().HasMaxLength(20);
            entity.HasOne<TenantReferral>().WithMany().HasForeignKey(c => c.ReferralId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<TenantCharge>().WithMany().HasForeignKey(c => c.TenantChargeId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(c => c.TenantChargeId).IsUnique()
                  .HasDatabaseName("ix_referral_commissions_charge_unique");
            entity.HasIndex(c => c.DueDate).HasDatabaseName("ix_referral_commissions_due_date");
            entity.HasIndex(c => c.ReferenceMonth).HasDatabaseName("ix_referral_commissions_reference_month");
        });
    }
}
