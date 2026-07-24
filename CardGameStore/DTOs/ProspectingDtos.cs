// =============================================================================
// ProspectingDtos.cs — Busca de possíveis clientes (lojas físicas) via Google
// Places API, com classificação heurística (sem IA) e enriquecimento opcional
// via Gemini. Ver CardGameStore/Services/Implementations/ProspectingService.cs.
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class ProspectingSearchRequest
{
    /// <summary>Categoria/tipo de negócio, em linguagem natural (ex: "loja de roupas").</summary>
    [Required, MaxLength(100)]
    public string Categoria { get; set; } = string.Empty;

    /// <summary>Cidade (ou "cidade, UF") onde buscar.</summary>
    [Required, MaxLength(100)]
    public string Cidade { get; set; } = string.Empty;
}

/// <summary>Um candidato a lead encontrado na busca — nunca é salvo sozinho,
/// só vira Lead de verdade quando o dono da plataforma confirma via
/// POST /api/platform/leads/prospeccao.</summary>
public class ProspectCandidateDto
{
    public string  PlaceId               { get; set; } = string.Empty;
    public string  Nome                  { get; set; } = string.Empty;
    public string? Endereco              { get; set; }
    public string? Telefone              { get; set; }
    public string? Website               { get; set; }
    public double? Rating                { get; set; }
    public int?    ReviewCount           { get; set; }

    /// <summary>"SemSite", "SiteLegado" ou "ECommerce" — calculado sem IA
    /// (ver ProspectingService.ClassifyDigitalPresenceAsync).</summary>
    public string  DigitalPresence       { get; set; } = string.Empty;

    /// <summary>0-100, calculado sem IA a partir de nota/reviews/presença digital.</summary>
    public int     OpportunityScore      { get; set; }

    /// <summary>Faixa grosseira baseada em nº de avaliações como proxy de
    /// movimento — nunca é dado financeiro real, só heurística de porte.</summary>
    public string  EstimatedRevenueRange { get; set; } = string.Empty;
}

public class ProspectingEnrichRequest
{
    [Required, MaxLength(255)]
    public string PlaceId { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Categoria { get; set; }

    [MaxLength(255)]
    public string? Endereco { get; set; }

    public double? Rating { get; set; }
    public int?    ReviewCount { get; set; }

    [Required, MaxLength(20)]
    public string DigitalPresence { get; set; } = string.Empty;
}

public class ProspectingEnrichResponse
{
    public string EstimatedRevenueRange { get; set; } = string.Empty;
    public string AbordagemSugerida     { get; set; } = string.Empty;
}

/// <summary>Cria um Lead a partir de um candidato de prospecção confirmado
/// pelo dono da plataforma — distinto do POST /api/leads público (que só tem
/// Nome/Telefone/Email/Mensagem do formulário da landing).</summary>
public class CreateProspectLeadRequest
{
    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Telefone { get; set; }

    [MaxLength(255)]
    public string? PlaceId { get; set; }

    [MaxLength(20)]
    public string? DigitalPresence { get; set; }

    [Range(0, 100)]
    public int? OpportunityScore { get; set; }

    [MaxLength(60)]
    public string? EstimatedRevenueRange { get; set; }

    [MaxLength(2000)]
    public string? AbordagemSugerida { get; set; }
}
