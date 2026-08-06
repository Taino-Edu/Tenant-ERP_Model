// =============================================================================
// IAlertaFiscalService.cs — Pendências fiscais com responsável e confirmação de
// resolução (CON-002 do plano de go-live).
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;

namespace CardGameStore.Services.Interfaces;

public interface IAlertaFiscalService
{
    /// <summary>
    /// Recalcula todas as pendências fiscais a partir do estado atual e concilia
    /// com os alertas já abertos: cria o que é novo, atualiza o que continua e
    /// resolve automaticamente o que deixou de existir.
    ///
    /// Idempotente por construção — rodar duas vezes seguidas não duplica nada,
    /// porque a identidade do alerta vem do fato, não da execução.
    /// </summary>
    Task<int> SincronizarAsync(CancellationToken ct = default);

    Task<PainelAlertasFiscaisDto> ListarAsync(bool incluirResolvidos = false, CancellationToken ct = default);

    /// <summary>Define (ou remove, com null) o responsável por resolver a pendência.</summary>
    Task<AlertaFiscal> AtribuirResponsavelAsync(Guid alertaId, Guid? responsavelUserId, CancellationToken ct = default);

    /// <summary>
    /// Confirmação humana de que a pendência foi tratada. Não suprime o fato: se
    /// ele continuar verdadeiro, o próximo ciclo reabre o alerta.
    /// </summary>
    Task<AlertaFiscal> ResolverAsync(Guid alertaId, Guid usuarioId, string observacao, CancellationToken ct = default);
}
