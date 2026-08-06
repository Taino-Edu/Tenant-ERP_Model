// =============================================================================
// ConciliacaoFiscalService.cs — Toda venda tributável, com o documento que tem
// ou a falta dele (CON-001 do plano de go-live).
//
// A inversão que dá valor a este serviço: os alertas existentes partem de uma
// NotaFiscalEmitida e perguntam "esta nota está bem?". Isso nunca enxerga a
// venda que não gerou nota nenhuma — e a emissão é opcional no fechamento. Aqui
// o ponto de partida são as VENDAS; o documento é o que se procura.
//
// O relatório não bloqueia caixa nem impede fechamento: ele expõe. A decisão de
// não emitir continua sendo do lojista; o que não pode é ela ficar invisível.
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ConciliacaoFiscalService : IConciliacaoFiscalService
{
    private readonly AppDbContext _db;

    public ConciliacaoFiscalService(AppDbContext db) => _db = db;

    public async Task<ConciliacaoFiscalDto> ConciliarAsync(DateTime inicioBr, DateTime fimBr)
    {
        inicioBr = inicioBr.Date;
        fimBr = fimBr.Date;

        var iniUtc = BrazilTime.DateToUtcStart(inicioBr);
        var fimUtc = BrazilTime.DateToUtcStart(fimBr.AddDays(1));

        // ── Universo de vendas do período ─────────────────────────────────────
        // Comanda cancelada não é venda tributável e fica fora; o recorte é o
        // mesmo do cálculo financeiro (ClosedAt + Fechada).
        var comandas = await _db.Comandas.AsNoTracking()
            .Where(c => c.Status == ComandaStatus.Fechada
                     && c.ClosedAt >= iniUtc && c.ClosedAt < fimUtc)
            .Select(c => new { c.Id, c.ClosedAt, c.TotalInCents,
                               c.FiscalEmissaoEscolhida, c.FiscalDecisaoPorUserId, c.FiscalDecisaoEm })
            .ToListAsync();

        var vendas = await _db.VendasAvulsas.AsNoTracking()
            .Where(v => v.SoldAt >= iniUtc && v.SoldAt < fimUtc)
            .Select(v => new { v.Id, v.SoldAt, v.TotalInCents, v.CanceladoEm,
                               v.FiscalEmissaoEscolhida, v.FiscalDecisaoPorUserId, v.FiscalDecisaoEm })
            .ToListAsync();

        // As notas são buscadas pelos IDs das vendas do período, não por data
        // própria: uma venda de ontem pode ter tido a nota criada hoje (retry,
        // contingência), e filtrar por CreatedAt da nota perderia justamente
        // esses casos — que são os que mais interessam à conciliação.
        var comandaIds = comandas.Select(c => c.Id).ToList();
        var vendaIds = vendas.Select(v => v.Id).ToList();

        var notas = await _db.NotasFiscaisEmitidas.AsNoTracking()
            .Where(n => (n.ComandaId != null && comandaIds.Contains(n.ComandaId.Value))
                     || (n.VendaAvulsaId != null && vendaIds.Contains(n.VendaAvulsaId.Value)))
            .Select(n => new
            {
                n.Id, n.Origem, n.ComandaId, n.VendaAvulsaId, n.Status,
                n.Serie, n.Numero, n.ChaveAcesso, n.ValorTotalEmCentavos, n.MotivoRejeicao,
            })
            .ToListAsync();

        // GroupBy + First porque, em tese, uma origem pode ter mais de um
        // registro (rejeitada e depois reemitida). A mais recente vence.
        var notaPorComanda = notas
            .Where(n => n.Origem == NotaFiscalOrigem.Comanda && n.ComandaId.HasValue)
            .GroupBy(n => n.ComandaId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(n => n.Status == NotaFiscalStatus.Autorizada).First());

        var notaPorVenda = notas
            .Where(n => n.Origem == NotaFiscalOrigem.VendaAvulsa && n.VendaAvulsaId.HasValue)
            .GroupBy(n => n.VendaAvulsaId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(n => n.Status == NotaFiscalStatus.Autorizada).First());

        // Nome de quem decidiu, resolvido uma vez só — a conciliação de um mês
        // pode ter centenas de vendas e um punhado de operadores.
        var decisores = comandas.Select(c => c.FiscalDecisaoPorUserId)
            .Concat(vendas.Select(v => v.FiscalDecisaoPorUserId))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var nomePorId = decisores.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => decisores.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);
        string? NomeDe(Guid? id) => id.HasValue && nomePorId.TryGetValue(id.Value, out var n) ? n : null;

        var conciliadas = new List<VendaConciliadaDto>();

        foreach (var comanda in comandas)
        {
            notaPorComanda.TryGetValue(comanda.Id, out var nota);
            conciliadas.Add(Montar(
                vendaId: comanda.Id,
                origem: nameof(NotaFiscalOrigem.Comanda),
                ocorridaEm: comanda.ClosedAt!.Value,
                valorVendaCentavos: comanda.TotalInCents,
                vendaCancelada: false,
                nota: nota is null ? null : new DadosNota(
                    nota.Id, nota.Status, nota.Serie, nota.Numero,
                    nota.ChaveAcesso, nota.ValorTotalEmCentavos, nota.MotivoRejeicao),
                decisao: new DecisaoFiscal(comanda.FiscalEmissaoEscolhida,
                    NomeDe(comanda.FiscalDecisaoPorUserId), comanda.FiscalDecisaoEm)));
        }

        // A venda avulsa cancelada ENTRA no relatório, marcada como tal: ela pode
        // ter gerado nota antes do cancelamento, e essa nota precisa aparecer.
        foreach (var venda in vendas)
        {
            notaPorVenda.TryGetValue(venda.Id, out var nota);
            conciliadas.Add(Montar(
                vendaId: venda.Id,
                origem: nameof(NotaFiscalOrigem.VendaAvulsa),
                ocorridaEm: venda.SoldAt,
                valorVendaCentavos: venda.TotalInCents,
                vendaCancelada: venda.CanceladoEm.HasValue,
                nota: nota is null ? null : new DadosNota(
                    nota.Id, nota.Status, nota.Serie, nota.Numero,
                    nota.ChaveAcesso, nota.ValorTotalEmCentavos, nota.MotivoRejeicao),
                decisao: new DecisaoFiscal(venda.FiscalEmissaoEscolhida,
                    NomeDe(venda.FiscalDecisaoPorUserId), venda.FiscalDecisaoEm)));
        }

        var ordenadas = conciliadas.OrderBy(v => v.OcorridaEm).ToList();

        // Todas as situações aparecem no resumo, inclusive zeradas — um "sem
        // documento: 0" é informação, e some se o dicionário só trouxer o que ocorreu.
        var porSituacao = Enum.GetValues<SituacaoFiscalVenda>().ToDictionary(
            situacao => situacao.ToString(),
            situacao =>
            {
                var doGrupo = ordenadas.Where(v => v.Situacao == situacao).ToList();
                return new ConciliacaoResumoDto(doGrupo.Count, doGrupo.Sum(v => v.ValorVenda));
            });

        return new ConciliacaoFiscalDto(
            PeriodoInicio: inicioBr,
            PeriodoFim: fimBr,
            TotalVendas: ordenadas.Count,
            ValorTotalVendas: ordenadas.Sum(v => v.ValorVenda),
            PorSituacao: porSituacao,
            Pendencias: ordenadas.Where(v => v.ExigeAtencao).ToList(),
            Vendas: ordenadas);
    }

    private sealed record DadosNota(
        Guid Id, NotaFiscalStatus Status, int? Serie, int? Numero,
        string? ChaveAcesso, long ValorTotalEmCentavos, string? MotivoRejeicao);

    /// <summary>Decisão registrada no fechamento (CON-003).</summary>
    private sealed record DecisaoFiscal(bool? Escolhida, string? Por, DateTime? Em);

    private static VendaConciliadaDto Montar(
        Guid vendaId, string origem, DateTime ocorridaEm, int valorVendaCentavos,
        bool vendaCancelada, DadosNota? nota, DecisaoFiscal? decisao = null)
    {
        var situacao = ClassificarSituacao(vendaCancelada, nota?.Status);

        return new VendaConciliadaDto(
            VendaId: vendaId,
            Origem: origem,
            OcorridaEm: ocorridaEm,
            ValorVenda: valorVendaCentavos / 100m,
            Situacao: situacao,
            NotaId: nota?.Id,
            Serie: nota?.Serie,
            Numero: nota?.Numero,
            ChaveAcesso: nota?.ChaveAcesso,
            ValorNota: nota is null ? null : nota.ValorTotalEmCentavos / 100m,
            MotivoRejeicao: nota?.MotivoRejeicao,
            EmissaoEscolhida: decisao?.Escolhida,
            DecididaPor: decisao?.Por,
            DecididaEm: decisao?.Em);
    }

    /// <summary>
    /// A venda cancelada tem precedência sobre o estado da nota: ela pode ter
    /// documento emitido antes do cancelamento, mas não é pendência fiscal a
    /// cobrar — cobrar documento de venda cancelada geraria ruído diário.
    /// </summary>
    private static SituacaoFiscalVenda ClassificarSituacao(bool vendaCancelada, NotaFiscalStatus? status)
    {
        if (vendaCancelada) return SituacaoFiscalVenda.VendaCancelada;
        if (status is null) return SituacaoFiscalVenda.SemDocumento;

        return status switch
        {
            NotaFiscalStatus.Autorizada             => SituacaoFiscalVenda.Autorizada,
            NotaFiscalStatus.AutorizadaContingencia => SituacaoFiscalVenda.EmContingencia,
            NotaFiscalStatus.PendenteEmissao        => SituacaoFiscalVenda.Pendente,
            // Resultado incerto (RES-001) é pendência: existe documento transmitido
            // cujo destino ainda não se conhece. Conta como venda a acompanhar, não
            // como venda documentada.
            NotaFiscalStatus.ResultadoIncerto        => SituacaoFiscalVenda.Pendente,
            NotaFiscalStatus.Rejeitada              => SituacaoFiscalVenda.Rejeitada,
            NotaFiscalStatus.Cancelada              => SituacaoFiscalVenda.NotaCancelada,
            _                                       => SituacaoFiscalVenda.Pendente,
        };
    }
}
