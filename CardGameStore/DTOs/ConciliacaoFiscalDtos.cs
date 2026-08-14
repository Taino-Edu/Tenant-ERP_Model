// =============================================================================
// ConciliacaoFiscalDtos.cs — Cruzamento entre vendas tributáveis e documentos
// fiscais (CON-001 do plano de go-live).
//
// O universo fiscal pode ser MENOR que o universo de vendas: a emissão da NFC-e
// é uma escolha no fechamento (`EmitirNotaFiscal`), e essa escolha não é
// persistida em lugar nenhum. Consequência: uma venda fechada sem nota não
// aparece como pendência, não aparece como erro — simplesmente não existe do
// ponto de vista fiscal. Todo alerta do sistema hoje parte de uma
// NotaFiscalEmitida existente, então ninguém vê o que nunca foi criado.
//
// Esta conciliação inverte o ponto de partida: começa pelas VENDAS e pergunta
// qual documento cada uma tem. O que não tiver, aparece.
// =============================================================================

using System.Text.Json.Serialization;

namespace CardGameStore.DTOs;

/// <summary>Situação fiscal de uma venda, na ótica da conciliação.</summary>
///
/// <remarks>
/// Serializa pelo NOME ("SemDocumento") e não pelo índice (5). Sem isto, o
/// campo `situacao` de VendaConciliadaDto saía como número no JSON e o portal
/// do contador — que indexa os rótulos por nome, igual ao `PorSituacao` deste
/// mesmo DTO, que sempre foi `Dictionary&lt;string, ...&gt;` — não achava
/// tradução nenhuma: a coluna "Situação" da Conciliação aparecia como um badge
/// VAZIO em toda linha, no desktop e no celular.
///
/// O resto da API já expõe enum como texto, só que convertendo no mapeamento
/// (ex.: TenantSummaryDto.Status é `string`). Aqui o enum precisa continuar
/// sendo enum em C#, porque as propriedades calculadas do próprio DTO comparam
/// contra ele (`Situacao == SituacaoFiscalVenda.SemDocumento`) — daí o
/// conversor no tipo em vez de um `string` no DTO.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SituacaoFiscalVenda
{
    /// <summary>Nota autorizada pela SEFAZ.</summary>
    Autorizada,

    /// <summary>Emitida offline; vale para o consumidor, mas ainda precisa ser retransmitida.</summary>
    EmContingencia,

    /// <summary>Documento criado, sem conclusão: pendente de emissão ou resultado incerto.</summary>
    Pendente,

    /// <summary>A SEFAZ recusou o documento.</summary>
    Rejeitada,

    /// <summary>Nota autorizada e depois cancelada por evento.</summary>
    NotaCancelada,

    /// <summary>
    /// A venda existe e nenhuma nota foi criada para ela. É o caso que o
    /// sistema não enxergava — não é erro de emissão, é ausência de emissão.
    /// </summary>
    SemDocumento,

    /// <summary>
    /// A própria venda foi cancelada. Não é pendência fiscal; aparece para o
    /// total bater, mas não deve ser cobrada como documento faltante.
    /// </summary>
    VendaCancelada,
}

public sealed record VendaConciliadaDto(
    Guid VendaId,
    // "Comanda" ou "VendaAvulsa".
    string Origem,
    DateTime OcorridaEm,
    decimal ValorVenda,
    SituacaoFiscalVenda Situacao,
    Guid? NotaId,
    int? Serie,
    int? Numero,
    string? ChaveAcesso,
    decimal? ValorNota,
    string? MotivoRejeicao,
    // ── Decisão registrada no fechamento (CON-003) ────────────────────────────
    // Sem isto, "venda sem documento" é ambíguo: pode ser escolha deliberada do
    // operador ou falha do sistema, e o contador não tem como distinguir.
    // Nulo = venda anterior ao registro da decisão — "não sabemos", não "não escolheu".
    bool? EmissaoEscolhida = null,
    string? DecididaPor = null,
    DateTime? DecididaEm = null)
{
    /// <summary>Venda que ficou sem documento porque alguém decidiu isso, e há
    /// registro de quem. Distinto de ausência inexplicada.</summary>
    public bool SemDocumentoPorEscolha =>
        Situacao == SituacaoFiscalVenda.SemDocumento && EmissaoEscolhida == false;

    /// <summary>
    /// Venda e nota com valores diferentes. Acontece quando a venda é editada
    /// depois da emissão — o documento fiscal fica com o valor antigo e ninguém
    /// é avisado. É divergência a investigar, não erro de cálculo.
    /// </summary>
    public bool ValorDivergente =>
        ValorNota is { } nota && Math.Abs(nota - ValorVenda) >= 0.01m;

    /// <summary>Exige ação do lojista ou do contador antes do fechamento contábil.</summary>
    public bool ExigeAtencao =>
        Situacao is SituacaoFiscalVenda.SemDocumento
                 or SituacaoFiscalVenda.Pendente
                 or SituacaoFiscalVenda.Rejeitada
                 or SituacaoFiscalVenda.EmContingencia
        || ValorDivergente;
}

public sealed record ConciliacaoResumoDto(
    int Quantidade,
    decimal Valor);

/// <summary>
/// Retrato de um período: toda venda tributável, com o documento que tem (ou a
/// falta dele). O relatório não bloqueia nada — expõe.
/// </summary>
public sealed record ConciliacaoFiscalDto(
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    int TotalVendas,
    decimal ValorTotalVendas,
    // Situação → quantidade e valor. Sempre traz todas as situações, inclusive as zeradas.
    IReadOnlyDictionary<string, ConciliacaoResumoDto> PorSituacao,
    // Vendas que exigem ação, do mais antigo para o mais recente.
    IReadOnlyList<VendaConciliadaDto> Pendencias,
    IReadOnlyList<VendaConciliadaDto> Vendas)
{
    public int QuantidadePendencias => Pendencias.Count;

    /// <summary>Soma das vendas sem nenhum documento fiscal — o número que o contador precisa ver.</summary>
    public decimal ValorSemDocumento =>
        Vendas.Where(v => v.Situacao == SituacaoFiscalVenda.SemDocumento).Sum(v => v.ValorVenda);
}
