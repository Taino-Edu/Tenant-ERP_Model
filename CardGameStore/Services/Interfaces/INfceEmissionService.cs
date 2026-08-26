// =============================================================================
// INfceEmissionService.cs — Contrato do motor de emissão de NFC-e
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;

namespace CardGameStore.Services.Interfaces;

public interface INfceEmissionService
{
    /// <summary>
    /// Registra e tenta emitir a NFC-e referente ao fechamento de uma Comanda
    /// (itens, forma de pagamento e total são recarregados internamente).
    /// Nunca lança exceção — falhas de emissão ficam registradas como PendenteEmissao
    /// para não bloquear o fechamento da venda.
    /// </summary>
    Task<NotaFiscalEmitida> EmitirParaComandaAsync(Guid comandaId);

    /// <summary>
    /// Registra e tenta emitir a NFC-e referente a uma Venda Avulsa (balcão).
    /// Mesma garantia de não lançar exceção.
    /// </summary>
    Task<NotaFiscalEmitida> EmitirParaVendaAvulsaAsync(Guid vendaAvulsaId);

    /// <summary>Emite a partir de um snapshot vindo de ERP externo, com idempotencia no banco.</summary>
    Task<NotaFiscalEmitida> EmitirIntegracaoAsync(IntegrationFiscalEmissionRequest request);

    /// <summary>
    /// Tenta emitir de novo uma nota PendenteEmissao ou Rejeitada. Nunca lança exceção.
    /// Notas em outros status (Autorizada/Cancelada) são retornadas sem alteração.
    /// </summary>
    Task<NotaFiscalEmitida> ReprocessarAsync(Guid notaId);

    /// <summary>
    /// Cancela uma NFC-e Autorizada, dentro da janela legal (30 min). Lança
    /// <see cref="InvalidOperationException"/> se a nota não existir, não estiver
    /// autorizada, estiver fora da janela ou a justificativa for curta demais —
    /// esses são erros de uso genuínos que o admin precisa ver, diferente da
    /// emissão (que nunca lança).
    /// </summary>
    Task<NotaFiscalEmitida> CancelarAsync(Guid notaId, string justificativa);

    /// <summary>Repete somente o estorno ERP de uma nota já cancelada na SEFAZ.</summary>
    Task<NotaFiscalEmitida> ReprocessarEstornoErpAsync(Guid notaId);

    /// <summary>Inutiliza explicitamente uma faixa de numeração que não será usada.</summary>
    Task<InutilizacaoFiscal> InutilizarFaixaAsync(
        int ano, int serie, int numeroInicial, int numeroFinal, string justificativa);

    /// <summary>
    /// Monta a representação do DANFE NFC-e a partir do XML fiscal persistido.
    /// Devolve null quando a nota não tem XML — documento sem autorização não
    /// pode ser apresentado como DANFE (DFE-007 do plano de go-live).
    /// </summary>
    Task<DanfeFiscalDto?> ObterCupomAsync(Guid notaId);
}
