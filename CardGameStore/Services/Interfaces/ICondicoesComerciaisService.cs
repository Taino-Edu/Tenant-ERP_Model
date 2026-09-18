using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

/// <summary>Condições comerciais negociadas com cada loja: mensalidade, dia de
/// vencimento, implantação parcelada e descontos empilháveis com vigência.
/// Toda alteração já aplica o resultado às cobranças em aberto.</summary>
public interface ICondicoesComerciaisService
{
    Task<CondicoesComerciaisDto> ObterAsync(Guid tenantId);

    Task<AlteracaoCondicoesResultDto> AtualizarAsync(Guid tenantId, AtualizarCondicoesRequest request);

    Task<AlteracaoCondicoesResultDto> CriarDescontoAsync(Guid tenantId, SalvarDescontoRequest request, string? autor);

    Task<AlteracaoCondicoesResultDto> AtualizarDescontoAsync(Guid tenantId, Guid descontoId, SalvarDescontoRequest request);

    Task<AlteracaoCondicoesResultDto> ExcluirDescontoAsync(Guid tenantId, Guid descontoId);
}
