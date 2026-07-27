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
}
