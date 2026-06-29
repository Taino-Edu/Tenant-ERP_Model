// =============================================================================
// KycController.cs — Verificação de maioridade (KYC)
//
// STATUS: INATIVO — não gera rotas, não está ligado em nada.
// Existe só como documentação da estrutura futura.
// Ver KYC_PLANNING.md para decisão com Maikon.
// =============================================================================

namespace CardGameStore.Controllers;

// Sem [ApiController] nem herança de ControllerBase → ASP.NET ignora completamente.
public class KycControllerDraft
{
    // Rotas planejadas (implementar depois):
    // GET  /api/kyc/status
    // POST /api/kyc/self-declaration   body: { birthDate }
    // POST /api/kyc/cpf                body: { cpf, birthDate }
    // GET  /api/kyc/admin/list         [AdminOnly]
    // POST /api/kyc/admin/{id}/override body: { approved, reason }
}

// DTOs planejados (comentados para não poluir o namespace agora)
// public record SelfDeclarationRequest(DateOnly BirthDate);
// public record CpfVerificationRequest(string Cpf, DateOnly BirthDate);
// public record AdminOverrideRequest(string Approved, string? Reason = null);
