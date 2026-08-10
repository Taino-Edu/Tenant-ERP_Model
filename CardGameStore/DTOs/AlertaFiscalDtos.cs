// =============================================================================
// AlertaFiscalDtos.cs — Painel de pendências fiscais (CON-002).
// =============================================================================

using CardGameStore.Models.PostgreSQL;

namespace CardGameStore.DTOs;

public sealed record AlertaFiscalDto(
    Guid Id,
    string Tipo,
    string Severidade,
    string Titulo,
    string Detalhe,
    string? Link,
    Guid? NotaFiscalId,
    DateTime OcorridoEm,
    DateTime DetectadoEm,
    DateTime AtualizadoEm,
    int Ocorrencias,
    Guid? ResponsavelUserId,
    string? ResponsavelNome,
    DateTime? ResponsavelDefinidoEm,
    DateTime? ResolvidoEm,
    string? ResolvidoPorNome,
    string? ResolucaoObservacao,
    bool ResolvidoAutomaticamente,
    DateTime? ReabertoEm,
    int Reaberturas)
{
    public bool EstaAberto => ResolvidoEm is null;

    /// <summary>Horas desde que o FATO começou. É o número que o lojista precisa
    /// ver: "há quanto tempo esta venda está sem documento", não "há quanto tempo
    /// o sistema percebeu".</summary>
    public int IdadeEmHoras => (int)Math.Floor((DateTime.UtcNow - OcorridoEm).TotalHours);
}

/// <summary>Retrato das pendências fiscais abertas, do mais grave/antigo em diante.</summary>
public sealed record PainelAlertasFiscaisDto(
    IReadOnlyList<AlertaFiscalDto> Alertas,
    int TotalAbertos,
    int Criticos,
    int Altos,
    int Medios,
    int SemResponsavel,
    DateTime? MaisAntigoOcorridoEm);

public sealed record ResolverAlertaFiscalRequest(string Observacao);

public static class AlertaFiscalMapper
{
    public static AlertaFiscalDto ToDto(AlertaFiscal a, string? responsavelNome, string? resolvidoPorNome) =>
        new(
            a.Id,
            a.Tipo.ToString(),
            a.Severidade.ToString(),
            a.Titulo,
            a.Detalhe,
            a.Link,
            a.NotaFiscalId,
            a.OcorridoEm,
            a.DetectadoEm,
            a.AtualizadoEm,
            a.Ocorrencias,
            a.ResponsavelUserId,
            responsavelNome,
            a.ResponsavelDefinidoEm,
            a.ResolvidoEm,
            resolvidoPorNome,
            a.ResolucaoObservacao,
            a.ResolvidoAutomaticamente,
            a.ReabertoEm,
            a.Reaberturas);
}
