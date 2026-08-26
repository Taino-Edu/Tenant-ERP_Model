using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

/// <summary>Billing da PLATAFORMA contra as lojas (o nosso financeiro), não o
/// financeiro de dentro de uma loja — esse é o FinanceiroCalculoService, que
/// opera no schema do tenant. Tudo aqui lê e escreve no catálogo (schema
/// "public") e só o dono da plataforma alcança.</summary>
public interface IPlatformBillingService
{
    /// <summary>Gera as mensalidades de um mês de competência para todas as
    /// lojas ativas que já entraram em cobrança. Idempotente: rodar de novo no
    /// mesmo mês não duplica nada.</summary>
    Task<GerarMensalidadesResultDto> GerarMensalidadesAsync(DateTime competencia);

    /// <summary>Painel do mês: MRR contratado, faturado, recebido, em aberto e a
    /// inadimplência acumulada.</summary>
    Task<BillingResumoDto> ObterResumoAsync(DateTime competencia);

    /// <summary>Cobranças de um mês de competência, com o nome de cada loja.</summary>
    Task<List<TenantChargeDto>> ListarPorCompetenciaAsync(DateTime competencia);

    /// <summary>Histórico completo de cobranças de uma loja, mais recente primeiro.</summary>
    Task<List<TenantChargeDto>> ListarPorTenantAsync(Guid tenantId);

    /// <summary>Marca como paga (ou reabre, passando null em pagoEm).</summary>
    Task<TenantChargeDto> DefinirPagamentoAsync(Guid chargeId, DateTime? pagoEm);

    /// <summary>Cria uma cobrança avulsa — implantação negociada, mês de
    /// cortesia, ajuste combinado fora do gerador automático.</summary>
    Task<TenantChargeDto> CriarCobrancaAsync(CriarCobrancaRequest request);

    /// <summary>Altera valor, vencimento e observação de uma cobrança EM ABERTO.
    /// Cobrança paga é recusada: a baixa já pode ter liberado comissão, e
    /// reescrever o valor por baixo dela deixaria os dois números divergentes
    /// sem nada indicando isso. Para corrigir uma paga: reabrir, editar, dar
    /// baixa de novo — o caminho que refaz a comissão junto.</summary>
    Task<TenantChargeDto> AtualizarCobrancaAsync(Guid chargeId, AtualizarCobrancaRequest request);

    /// <summary>Exclui uma cobrança EM ABERTO. Paga não se exclui — nem por
    /// integridade (comissão aponta para ela) nem por contabilidade.</summary>
    Task ExcluirCobrancaAsync(Guid chargeId);

    // ── Automação da cobrança (RB-01) ────────────────────────────────────────

    /// <summary>Registra no gateway toda cobrança em aberto que ainda não tem id
    /// externo. Idempotente: rodar duas vezes não emite a mesma cobrança de
    /// novo.</summary>
    Task<EmissaoGatewayResultDto> EmitirCobrancasPendentesAsync(CancellationToken ct = default);

    /// <summary>Aplica o pagamento (ou o estorno) que veio do gateway. Devolve
    /// false quando o id externo não bate com cobrança nenhuma — o que é
    /// esperado e não é erro: o webhook do gateway também avisa sobre cobranças
    /// que não são nossas mensalidades.</summary>
    Task<bool> RegistrarPagamentoExternoAsync(
        string gateway, string externalChargeId, bool paga, DateTime? pagoEm);

    /// <summary>Régua de cobrança: suspende quem está vencido além da carência e
    /// reativa quem quitou. É o que substitui a suspensão manual.</summary>
    Task<ReguaCobrancaResultDto> AplicarReguaDeCobrancaAsync(CancellationToken ct = default);
}
