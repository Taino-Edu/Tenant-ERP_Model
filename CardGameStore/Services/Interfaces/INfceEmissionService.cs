// =============================================================================
// INfceEmissionService.cs — Contrato do motor de emissão de NFC-e
// =============================================================================

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

    /// <summary>Monta os dados pra exibir/imprimir o cupom da NFC-e (com QR Code, se o CSC estiver configurado).</summary>
    Task<CupomDto?> ObterCupomAsync(Guid notaId);
}

public record CupomItemDto(string Nome, int Quantidade, int PrecoUnitarioCentavos, int SubtotalCentavos);

public record CupomDto(
    string RazaoSocial, string Cnpj, string Endereco,
    string? ChaveAcesso, string? Protocolo, DateTime? EmitidoEm,
    int Serie, int Numero, string Status,
    List<CupomItemDto> Itens, int ValorTotalCentavos, string FormaPagamento,
    string? QrCodeUrl);
