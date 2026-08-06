// =============================================================================
// FiscalConfigDtos.cs — Contrato de escrita da configuração fiscal.
// Fica em DTOs (e não dentro do controller) porque hoje há duas portas de
// entrada pra mesma config: /api/fiscal/config (lojista) e
// /api/contador-portal/clientes/{id}/config (contador), ambas passando por
// FiscalConfigService.
// =============================================================================

namespace CardGameStore.DTOs;

public class SaveFiscalConfigRequest
{
    public string? Cnpj              { get; init; }
    public string? RazaoSocial       { get; init; }
    public string? InscricaoEstadual { get; init; }
    public string? Logradouro          { get; init; }
    public string? Numero              { get; init; }
    public string? Complemento         { get; init; }
    public string? Bairro              { get; init; }
    public string? CodigoMunicipioIbge { get; init; }
    public string? Municipio           { get; init; }
    public string? Uf                  { get; init; }
    public string? Cep                 { get; init; }
    public string? CscId               { get; init; }
    public string? CscToken            { get; init; }
    public string? RegimeTributario  { get; init; }

    // ── Perfil IBS/CBS (RTC-001) ─────────────────────────────────────────────
    // Selecionam qual faixa do catálogo versionado de regras se aplica. O regime
    // declarado não revela nenhuma das duas, e o sistema não tem como inferi-las.

    /// <summary>Optante do Simples que excedeu o sublimite estadual. Null = não altera.</summary>
    public bool? ExcedeuSublimiteSimples  { get; init; }

    /// <summary>Optante do Simples que optou pelo regime regular de IBS/CBS. Null = não altera.</summary>
    public bool? OptouRegimeRegularIbsCbs { get; init; }
    public string? Ambiente          { get; init; }
    public int?    SerieNfce         { get; init; }
    public string? EmailContador     { get; init; }
    public string? IbptToken         { get; init; }
    public bool? IbptAutoSyncEnabled { get; init; }
    public bool? RemoverIbptToken    { get; init; }

    /// <summary>Formas de pagamento que emitem NFC-e automaticamente ao fechar a venda, sem perguntar. Null = não altera.</summary>
    public string[]? FormasPagamentoAutoEmissao { get; init; }

    // ── Parâmetros de apuração (comparativo Simples x Presumido) ──────────────
    // Não entram no XML da NFC-e; existem porque o sistema não tem como inferir
    // atividade, folha de pagamento nem as alíquotas locais de ICMS/ISS.

    /// <summary>"I" a "V" — anexo da LC 123/2006 aplicável à atividade.</summary>
    public string?  AnexoSimples                   { get; init; }
    public long?    FolhaPagamento12mEmCentavos    { get; init; }
    public long?    FolhaPagamentoMensalEmCentavos { get; init; }
    public decimal? PercentualPresuncaoIrpj        { get; init; }
    public decimal? PercentualPresuncaoCsll        { get; init; }
    public decimal? AliquotaIcmsPercentual         { get; init; }
    public decimal? AliquotaIssPercentual          { get; init; }
}
