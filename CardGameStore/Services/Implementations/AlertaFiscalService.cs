// =============================================================================
// AlertaFiscalService.cs — CON-002: alertas fiscais por severidade e idade, com
// responsável e confirmação de resolução.
//
// O modelo aqui é de RECONCILIAÇÃO, não de disparo. Cada ciclo pergunta ao banco
// "quais pendências fiscais existem AGORA?" e casa esse conjunto com os alertas
// abertos. Três consequências valem o desenho:
//
//   • deduplicação sai de graça — a identidade do alerta é derivada do fato
//     (`ResultadoIncerto:{notaId}`), então nenhum ciclo cria o segundo;
//   • resolução automática é confiável — o alerta some quando o fato some, não
//     quando alguém esquece de renová-lo;
//   • o painel nunca mente por omissão — nada depende de o disparo ter
//     acontecido no momento certo; o estado é recalculado do zero.
//
// A alternativa que existia antes (Notification avulsa com "já alertei nas
// últimas 6h?") não tinha nenhuma das três: duplicava entre reinícios, nunca
// desaparecia sozinha e não sabia dizer o que ainda estava pendente.
// =============================================================================

using System.Globalization;
using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class AlertaFiscalService : IAlertaFiscalService
{
    /// <summary>Prazo legal (NT 2015.002) para a NFC-e offline ser autorizada.</summary>
    private static readonly TimeSpan PrazoContingencia = TimeSpan.FromHours(24);

    /// <summary>A partir daqui a contingência vira crítica: sobra pouco tempo para
    /// ação manual antes de a venda ficar permanentemente sem documento válido.</summary>
    private static readonly TimeSpan ContingenciaCritica = TimeSpan.FromHours(20);

    /// <summary>Venda sem documento é pendência de fechamento diário; passando de
    /// três dias deixa de ser "o dia ainda não fechou" e vira problema.</summary>
    private static readonly TimeSpan SemDocumentoEscalona = TimeSpan.FromDays(3);

    /// <summary>Janela de dias já fechados varrida em busca de venda sem documento.
    /// O dia corrente fica de fora de propósito: cobrar documento de uma venda que
    /// acabou de acontecer geraria alerta que se resolve sozinho em minutos.</summary>
    private const int DiasConciliacaoRetroativa = 7;

    /// <summary>
    /// Cultura explícita para tudo que vai à tela do lojista. Sem isto, o texto do
    /// alerta herda a cultura do processo — e o contêiner roda em invariante, o
    /// que faria "R$ 75,00" virar "R$ 75.00" num painel fiscal brasileiro.
    /// Foi assim que o CI pegou o defeito: o teste passava na máquina pt-BR do
    /// desenvolvedor e falhava no runner Linux.
    /// </summary>
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly AppDbContext _db;
    private readonly IConciliacaoFiscalService _conciliacao;
    private readonly ILogger<AlertaFiscalService> _logger;
    private readonly IEmailService? _email;

    public AlertaFiscalService(
        AppDbContext db, IConciliacaoFiscalService conciliacao, ILogger<AlertaFiscalService> logger,
        IEmailService? email = null)
    {
        _db          = db;
        _conciliacao = conciliacao;
        _logger      = logger;
        // Opcional: sem serviço de e-mail o painel continua funcionando igual —
        // o canal externo é reforço do alerta, não a fonte dele.
        _email       = email;
    }

    /// <summary>Uma pendência detectada no estado atual, ainda sem vínculo com o
    /// alerta persistido.</summary>
    private sealed record PendenciaDetectada(
        string Chave,
        TipoAlertaFiscal Tipo,
        SeveridadeAlertaFiscal Severidade,
        string Titulo,
        string Detalhe,
        DateTime OcorridoEm,
        string? Link = null,
        Guid? NotaFiscalId = null);

    // ── Reconciliação ─────────────────────────────────────────────────────────

    public async Task<int> SincronizarAsync(CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;

        var detectadas = new List<PendenciaDetectada>();

        // Pendências que nascem de uma NOTA valem sempre: se existe nota rejeitada,
        // em contingência ou com resultado incerto, a loja emitiu — e precisa
        // resolver, mesmo que tenha desistido do fiscal depois.
        detectadas.AddRange(await DetectarPorNotaAsync(ct));

        // As demais nascem de uma EXPECTATIVA de emissão, e essa expectativa só
        // existe se a loja está aparelhada para emitir. Sem certificado, nenhuma
        // venda deveria ter documento — cobrar isso encheria o painel de uma loja
        // que nunca optou pelo fiscal com um alerta por dia de venda, escalando
        // para Alta em três dias. O módulo fiscal vem habilitado por padrão em
        // todo tenant, então esta é a diferença entre "tem o módulo" e "usa".
        if (await LojaEmiteDocumentoFiscalAsync(ct))
        {
            detectadas.AddRange(await DetectarVendasSemDocumentoAsync(ct));
            detectadas.AddRange(await DetectarLacunasDeNumeracaoAsync(ct));
            detectadas.AddRange(await DetectarExportacaoMensalPendenteAsync(ct));
            detectadas.AddRange(await DetectarRegraIbsCbsDesatualizadaAsync(ct));
        }

        // Duas detecções com a mesma chave seriam um bug de composição — a última
        // vence, mas isso não deve acontecer e o banco recusaria a duplicata.
        var porChave = detectadas
            .GroupBy(p => p.Chave)
            .ToDictionary(g => g.Key, g => g.Last());

        var persistidos = await _db.AlertasFiscais
            .Where(a => porChave.Keys.Contains(a.Chave) || a.ResolvidoEm == null)
            .ToListAsync(ct);
        var persistidosPorChave = persistidos.ToDictionary(a => a.Chave);

        var novosCriticos = new List<AlertaFiscal>();

        foreach (var (chave, pendencia) in porChave)
        {
            if (!persistidosPorChave.TryGetValue(chave, out var alerta))
            {
                alerta = new AlertaFiscal
                {
                    Chave        = chave,
                    Tipo         = pendencia.Tipo,
                    Severidade   = pendencia.Severidade,
                    Titulo       = pendencia.Titulo,
                    Detalhe      = pendencia.Detalhe,
                    Link         = pendencia.Link,
                    NotaFiscalId = pendencia.NotaFiscalId,
                    OcorridoEm   = pendencia.OcorridoEm,
                    DetectadoEm  = agora,
                    AtualizadoEm = agora,
                };
                _db.AlertasFiscais.Add(alerta);
                if (alerta.Severidade == SeveridadeAlertaFiscal.Critica)
                    novosCriticos.Add(alerta);
                continue;
            }

            var escalouParaCritica =
                alerta.Severidade != SeveridadeAlertaFiscal.Critica &&
                pendencia.Severidade == SeveridadeAlertaFiscal.Critica;

            // O fato continua verdadeiro: o alerta reabre se alguém o tinha dado
            // por resolvido. Confirmação humana não apaga pendência fiscal.
            if (alerta.ResolvidoEm is not null)
            {
                alerta.ResolvidoEm              = null;
                alerta.ResolvidoPorUserId       = null;
                alerta.ResolucaoObservacao      = null;
                alerta.ResolvidoAutomaticamente = false;
                alerta.ReabertoEm               = agora;
                alerta.Reaberturas++;
            }

            alerta.Tipo         = pendencia.Tipo;
            alerta.Severidade   = pendencia.Severidade;
            alerta.Titulo       = pendencia.Titulo;
            alerta.Detalhe      = pendencia.Detalhe;
            alerta.Link         = pendencia.Link;
            alerta.NotaFiscalId = pendencia.NotaFiscalId;
            alerta.OcorridoEm   = pendencia.OcorridoEm;
            alerta.AtualizadoEm = agora;
            alerta.Ocorrencias++;

            if (escalouParaCritica) novosCriticos.Add(alerta);
        }

        // O que estava aberto e não foi detectado agora deixou de existir.
        var resolvidosAutomaticamente = 0;
        foreach (var alerta in persistidos.Where(a => a.ResolvidoEm is null && !porChave.ContainsKey(a.Chave)))
        {
            alerta.ResolvidoEm              = agora;
            alerta.ResolvidoAutomaticamente = true;
            alerta.ResolucaoObservacao      = "A condição que gerou o alerta deixou de existir.";
            alerta.AtualizadoEm             = agora;
            resolvidosAutomaticamente++;
        }

        await NotificarCriticosAsync(novosCriticos, ct);
        await _db.SaveChangesAsync(ct);

        if (novosCriticos.Count > 0 || resolvidosAutomaticamente > 0)
            _logger.LogInformation(
                "Alertas fiscais sincronizados: {Abertos} aberto(s), {Criticos} crítico(s) novo(s), " +
                "{Resolvidos} resolvido(s) automaticamente.",
                porChave.Count, novosCriticos.Count, resolvidosAutomaticamente);

        return porChave.Count;
    }

    /// <summary>
    /// Severidade crítica também vira notificação — é o "imediato" que o plano
    /// pede para resultado incerto. A deduplicação é estrutural: só notifica na
    /// criação ou na escalada, nunca a cada ciclo.
    /// </summary>
    private async Task NotificarCriticosAsync(List<AlertaFiscal> criticos, CancellationToken ct)
    {
        if (criticos.Count == 0) return;

        var admins = await _db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToListAsync(ct);
        if (admins.Count == 0) return;

        foreach (var alerta in criticos)
            foreach (var admin in admins)
                _db.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Title  = Truncar(alerta.Titulo, 120),
                    Body   = Truncar(alerta.Detalhe, 500),
                    Link   = alerta.Link ?? "/admin/fiscal",
                });

        if (_email is null) return;

        // Um e-mail por admin, sobre o alerta mais grave do ciclo — não um por
        // alerta. Quem recebe cinco e-mails de uma vez para de ler no segundo, e
        // a mensagem diz quantos há no total para não esconder o resto.
        var principal = criticos[0];
        foreach (var admin in admins.Where(a => !string.IsNullOrWhiteSpace(a.Email)))
        {
            try
            {
                await _email.SendAlertaFiscalCriticoAsync(
                    admin.Email!, admin.Name, principal.Titulo, principal.Detalhe, criticos.Count);
            }
            catch (Exception ex)
            {
                // SMTP fora do ar não pode impedir o alerta de ser gravado — o
                // painel é a fonte, o e-mail é o reforço.
                _logger.LogWarning(ex,
                    "Falha ao enviar e-mail de alerta fiscal crítico para {Email}.", admin.Email);
            }
        }
    }

    // ── Detecções ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A loja está aparelhada para emitir? O critério é o certificado A1: sem ele
    /// a emissão nem chega a tentar (lança FiscalNaoConfiguradoException), então
    /// nenhuma venda deveria ter documento e nada há a cobrar.
    /// </summary>
    private async Task<bool> LojaEmiteDocumentoFiscalAsync(CancellationToken ct)
    {
        var cfg = await _db.FiscalConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == FiscalConfig.SingletonId, ct);
        return cfg?.CertificadoConfigurado == true;
    }

    /// <summary>
    /// Pendências que vivem numa nota: resultado incerto, contingência e
    /// rejeição. Uma consulta só — são os três estados não-terminais do mesmo
    /// registro.
    /// </summary>
    private async Task<List<PendenciaDetectada>> DetectarPorNotaAsync(CancellationToken ct)
    {
        var notas = await _db.NotasFiscaisEmitidas.AsNoTracking()
            .Where(n => n.Status == NotaFiscalStatus.ResultadoIncerto
                     || n.Status == NotaFiscalStatus.AutorizadaContingencia
                     || (n.Status == NotaFiscalStatus.Rejeitada && n.InutilizadoEm == null))
            .Select(n => new
            {
                n.Id, n.Status, n.Serie, n.Numero, n.ChaveAcesso, n.MotivoRejeicao,
                n.DhContingencia, n.ResultadoIncertoEm, n.CreatedAt, n.ValorTotalEmCentavos,
            })
            .ToListAsync(ct);

        var agora = DateTime.UtcNow;
        var pendencias = new List<PendenciaDetectada>(notas.Count);

        foreach (var nota in notas)
        {
            var documento = nota.Numero.HasValue
                ? $"NFC-e nº {nota.Numero} (série {nota.Serie})"
                : "NFC-e sem número reservado";
            var valor = string.Format(PtBr, "R$ {0:N2}", nota.ValorTotalEmCentavos / 100m);

            switch (nota.Status)
            {
                case NotaFiscalStatus.ResultadoIncerto:
                    // Imediato e sempre crítico: o risco aqui não cresce com o
                    // tempo, ele já está no máximo desde o primeiro segundo — pode
                    // existir um documento autorizado que este sistema não conhece.
                    pendencias.Add(new PendenciaDetectada(
                        Chave: $"{nameof(TipoAlertaFiscal.ResultadoIncerto)}:{nota.Id}",
                        Tipo: TipoAlertaFiscal.ResultadoIncerto,
                        Severidade: SeveridadeAlertaFiscal.Critica,
                        Titulo: $"{documento}: resposta da SEFAZ não chegou",
                        Detalhe:
                            $"A transmissão de {valor} não teve resposta e o documento pode estar autorizado na " +
                            $"SEFAZ com a chave {nota.ChaveAcesso ?? "(não registrada)"}. " +
                            "Não emita outra nota para esta venda: o sistema consulta a chave a cada ciclo. " +
                            "Se persistir, consulte a chave no portal da SEFAZ da sua UF antes de qualquer ação.",
                        OcorridoEm: nota.ResultadoIncertoEm ?? nota.CreatedAt,
                        Link: "/admin/fiscal",
                        NotaFiscalId: nota.Id));
                    break;

                case NotaFiscalStatus.AutorizadaContingencia:
                    var desde = nota.DhContingencia ?? nota.CreatedAt;
                    var idade = agora - desde;
                    var horasRestantes = (int)Math.Floor((PrazoContingencia - idade).TotalHours);
                    pendencias.Add(new PendenciaDetectada(
                        Chave: $"{nameof(TipoAlertaFiscal.ContingenciaPendente)}:{nota.Id}",
                        Tipo: TipoAlertaFiscal.ContingenciaPendente,
                        Severidade: idade >= ContingenciaCritica
                            ? SeveridadeAlertaFiscal.Critica
                            : SeveridadeAlertaFiscal.Alta,
                        Titulo: $"{documento}: em contingência há {(int)idade.TotalHours}h",
                        Detalhe: horasRestantes > 0
                            ? $"O cupom de {valor} foi entregue offline e ainda não foi autorizado pela SEFAZ. " +
                              $"Restam cerca de {horasRestantes}h do prazo legal de 24h. O sistema retransmite " +
                              "automaticamente a cada 15 minutos — verifique a conexão com a internet da loja."
                            : $"O cupom de {valor} passou do prazo legal de 24h sem autorização da SEFAZ. " +
                              "A venda está sem documento fiscal válido: trate a regularização com o contador.",
                        OcorridoEm: desde,
                        Link: "/admin/fiscal",
                        NotaFiscalId: nota.Id));
                    break;

                default: // Rejeitada
                    pendencias.Add(new PendenciaDetectada(
                        Chave: $"{nameof(TipoAlertaFiscal.NotaRejeitada)}:{nota.Id}",
                        Tipo: TipoAlertaFiscal.NotaRejeitada,
                        Severidade: SeveridadeAlertaFiscal.Alta,
                        Titulo: $"{documento}: rejeitada pela SEFAZ",
                        Detalhe:
                            $"Motivo: {nota.MotivoRejeicao ?? "não informado"}. " +
                            "A venda de " + valor + " está sem documento fiscal. Corrija o cadastro apontado no " +
                            "motivo e reprocesse a nota; se o número não for mais utilizável, inutilize a faixa " +
                            "para manter a sequência íntegra.",
                        OcorridoEm: nota.CreatedAt,
                        Link: "/admin/fiscal",
                        NotaFiscalId: nota.Id));
                    break;
            }
        }

        return pendencias;
    }

    /// <summary>
    /// Vendas de dias já fechados que não geraram documento nenhum — o caso que
    /// CON-001 tornou visível e que aqui vira cobrança com responsável. Um alerta
    /// por dia, porque a unidade de conferência do lojista é o fechamento diário.
    /// </summary>
    private async Task<List<PendenciaDetectada>> DetectarVendasSemDocumentoAsync(CancellationToken ct)
    {
        var hojeBr = BrazilTime.NowBr().Date;
        var inicioBr = hojeBr.AddDays(-DiasConciliacaoRetroativa);
        var ontemBr = hojeBr.AddDays(-1);
        if (ontemBr < inicioBr) return new List<PendenciaDetectada>();

        var conciliacao = await _conciliacao.ConciliarAsync(inicioBr, ontemBr);
        var agora = DateTime.UtcNow;

        return conciliacao.Vendas
            .Where(v => v.Situacao == SituacaoFiscalVenda.SemDocumento)
            .GroupBy(v => TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(v.OcorridaEm, DateTimeKind.Utc), BrazilTime.Zone).Date)
            .Select(dia =>
            {
                var inicioDoDiaUtc = BrazilTime.DateToUtcStart(dia.Key);
                var total = dia.Sum(v => v.ValorVenda);
                return new PendenciaDetectada(
                    Chave: $"{nameof(TipoAlertaFiscal.VendaSemDocumento)}:{dia.Key:yyyy-MM-dd}",
                    Tipo: TipoAlertaFiscal.VendaSemDocumento,
                    Severidade: agora - inicioDoDiaUtc >= SemDocumentoEscalona
                        ? SeveridadeAlertaFiscal.Alta
                        : SeveridadeAlertaFiscal.Media,
                    Titulo: string.Format(PtBr, "{0} venda(s) sem documento fiscal em {1:dd/MM/yyyy}", dia.Count(), dia.Key),
                    Detalhe:
                        string.Format(PtBr, "Somam R$ {0:N2} em vendas fechadas que não geraram NFC-e. ", total) +
                        "Não é erro de emissão — é ausência dela. Emita as notas em atraso pelo histórico, " +
                        "ou registre com o contador a razão de não emitir para que o mês feche coerente.",
                    OcorridoEm: inicioDoDiaUtc,
                    Link: "/admin/fiscal");
            })
            .ToList();
    }

    /// <summary>
    /// Buraco na sequência de numeração: número que nenhum registro local reivindica
    /// e nenhuma inutilização cobre. A SEFAZ cobra a sequência íntegra da série —
    /// e a decisão (reusar, corrigir ou inutilizar) é do responsável fiscal, então
    /// aqui só se expõe o buraco.
    /// </summary>
    private async Task<List<PendenciaDetectada>> DetectarLacunasDeNumeracaoAsync(CancellationToken ct)
    {
        var anoBr = BrazilTime.NowBr().Year;
        var inicioAnoUtc = BrazilTime.DateToUtcStart(new DateTime(anoBr, 1, 1));
        var fimAnoUtc = BrazilTime.DateToUtcStart(new DateTime(anoBr + 1, 1, 1));

        var notas = await _db.NotasFiscaisEmitidas.AsNoTracking()
            .Where(n => n.Serie != null && n.Numero != null &&
                        ((n.EmitidoEm != null && n.EmitidoEm >= inicioAnoUtc && n.EmitidoEm < fimAnoUtc) ||
                         (n.EmitidoEm == null && n.CreatedAt >= inicioAnoUtc && n.CreatedAt < fimAnoUtc)))
            .Select(n => new { Serie = n.Serie!.Value, Numero = n.Numero!.Value, n.CreatedAt })
            .ToListAsync(ct);
        if (notas.Count == 0) return new List<PendenciaDetectada>();

        var inutilizacoes = await _db.InutilizacoesFiscais.AsNoTracking()
            .Where(i => i.Ano == anoBr)
            .Select(i => new { i.Serie, i.NumeroInicial, i.NumeroFinal })
            .ToListAsync(ct);

        var pendencias = new List<PendenciaDetectada>();

        foreach (var serie in notas.GroupBy(n => n.Serie))
        {
            var usados = serie.Select(n => n.Numero).ToHashSet();
            var menor = usados.Min();
            var maior = usados.Max();
            var faixasInutilizadas = inutilizacoes.Where(i => i.Serie == serie.Key).ToList();

            var lacunas = Enumerable.Range(menor, maior - menor + 1)
                .Where(numero => !usados.Contains(numero))
                .Where(numero => !faixasInutilizadas.Any(f => numero >= f.NumeroInicial && numero <= f.NumeroFinal))
                .ToList();
            if (lacunas.Count == 0) continue;

            // "Desde quando se sabe": a primeira nota emitida DEPOIS do buraco é a
            // prova de que ele já existia. Usar a data de detecção esconderia a idade
            // real da pendência a cada reinício do serviço.
            var primeiroBuraco = lacunas.Min();
            var conhecidoDesde = serie
                .Where(n => n.Numero > primeiroBuraco)
                .Select(n => n.CreatedAt)
                .DefaultIfEmpty(DateTime.UtcNow)
                .Min();

            pendencias.Add(new PendenciaDetectada(
                Chave: $"{nameof(TipoAlertaFiscal.LacunaNumeracao)}:{anoBr}:{serie.Key}",
                Tipo: TipoAlertaFiscal.LacunaNumeracao,
                Severidade: SeveridadeAlertaFiscal.Media,
                Titulo: $"Série {serie.Key}: {lacunas.Count} número(s) faltando na sequência de {anoBr}",
                Detalhe:
                    $"Números sem registro local nem inutilização: {ResumirFaixas(lacunas)}. " +
                    "Consulte a sequência no portal da SEFAZ: se os números não foram usados, inutilize a faixa; " +
                    "se foram, recupere os documentos antes do fechamento do período.",
                OcorridoEm: conhecidoDesde,
                Link: "/admin/fiscal"));
        }

        return pendencias;
    }

    /// <summary>
    /// O ZIP mensal de XMLs ao contador não saiu neste mês. Só vale a partir do
    /// dia 2: o job roda no dia 1 e alertar antes dele seria alarme falso diário.
    /// </summary>
    private async Task<List<PendenciaDetectada>> DetectarExportacaoMensalPendenteAsync(CancellationToken ct)
    {
        var vazio = new List<PendenciaDetectada>();

        var hojeBr = BrazilTime.NowBr().Date;
        var cfg = await _db.FiscalConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == FiscalConfig.SingletonId, ct);
        if (!ExportacaoMensalEstaAtrasada(hojeBr, cfg?.UltimoEnvioMensalXmls, cfg?.EmailContador))
            return vazio;

        // Sem documento no mês anterior não há o que exportar — cobrar o envio
        // seria cobrar um e-mail vazio.
        var mesAnterior = hojeBr.AddMonths(-1);
        var inicioUtc = BrazilTime.DateToUtcStart(new DateTime(mesAnterior.Year, mesAnterior.Month, 1));
        var fimUtc = BrazilTime.DateToUtcStart(new DateTime(mesAnterior.Year, mesAnterior.Month, 1).AddMonths(1));
        var temDocumentos = await _db.NotasFiscaisEmitidas.AsNoTracking()
            .AnyAsync(n => n.XmlAutorizado != null &&
                           n.EmitidoEm != null && n.EmitidoEm >= inicioUtc && n.EmitidoEm < fimUtc, ct);
        if (!temDocumentos) return vazio;

        var inicioDoMesUtc = BrazilTime.DateToUtcStart(new DateTime(hojeBr.Year, hojeBr.Month, 1));

        return new List<PendenciaDetectada>
        {
            new(
                Chave: $"{nameof(TipoAlertaFiscal.ExportacaoMensalPendente)}:{hojeBr:yyyy-MM}",
                Tipo: TipoAlertaFiscal.ExportacaoMensalPendente,
                Severidade: hojeBr.Day >= 5 ? SeveridadeAlertaFiscal.Alta : SeveridadeAlertaFiscal.Media,
                Titulo: $"XMLs de {mesAnterior:MM/yyyy} ainda não foram enviados ao contador",
                Detalhe:
                    $"O envio automático do dia 1 não se concluiu para {cfg!.EmailContador}. " +
                    "Baixe e envie o ZIP manualmente em Admin > Fiscal > Exportar XMLs, e verifique a " +
                    "configuração de e-mail — a guarda dos documentos é obrigação do emitente.",
                OcorridoEm: inicioDoMesUtc,
                Link: "/admin/fiscal"),
        };
    }

    /// <summary>
    /// A regra de IBS/CBS aplicada aos documentos passou da data recomendada de
    /// revisão — ou não existe regra para hoje (RTC-001).
    ///
    /// Este alerta é a contrapartida de o motor não travar mais na virada do ano:
    /// como a emissão continua com a última regra conhecida, é aqui que se cobra
    /// a conferência contra a legislação vigente. Substituir uma parada geral por
    /// silêncio seria trocar um defeito por outro.
    /// </summary>
    private async Task<List<PendenciaDetectada>> DetectarRegraIbsCbsDesatualizadaAsync(CancellationToken ct)
    {
        var vazio = new List<PendenciaDetectada>();

        var cfg = await _db.FiscalConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == FiscalConfig.SingletonId, ct);
        if (cfg is null) return vazio;

        var hojeBr = DateOnly.FromDateTime(BrazilTime.NowBr().Date);
        var perfil = CatalogoRegrasIbsCbs.PerfilDe(cfg);
        var regra = CatalogoRegrasIbsCbs.Para(hojeBr, perfil);
        var revisaoEm = CatalogoRegrasIbsCbs.RevisaoRecomendadaEm;

        if (regra is null)
            return new List<PendenciaDetectada>
            {
                new(
                    Chave: $"{nameof(TipoAlertaFiscal.RegraIbsCbsDesatualizada)}:{perfil}:sem-regra",
                    Tipo: TipoAlertaFiscal.RegraIbsCbsDesatualizada,
                    Severidade: SeveridadeAlertaFiscal.Alta,
                    Titulo: "Nenhuma regra de IBS/CBS cobre a data de hoje",
                    Detalhe:
                        string.Format(PtBr, "O catálogo versionado não tem faixa vigente para {0:dd/MM/yyyy} no perfil ", hojeBr) +
                        $"{perfil}. As notas continuam sendo emitidas, mas SEM os grupos de IBS/CBS. " +
                        "Atualize o catálogo conforme a Nota Técnica vigente antes do próximo fechamento.",
                    OcorridoEm: BrazilTime.DateToUtcStart(hojeBr.ToDateTime(TimeOnly.MinValue)),
                    Link: "/admin/fiscal"),
            };

        if (hojeBr < revisaoEm) return vazio;

        return new List<PendenciaDetectada>
        {
            new(
                Chave: $"{nameof(TipoAlertaFiscal.RegraIbsCbsDesatualizada)}:{regra.Versao}",
                Tipo: TipoAlertaFiscal.RegraIbsCbsDesatualizada,
                Severidade: SeveridadeAlertaFiscal.Media,
                Titulo: $"Regra de IBS/CBS {regra.Versao} precisa ser reconferida",
                Detalhe:
                    string.Format(PtBr,
                        "A regra em uso desde {0:dd/MM/yyyy} continua sendo aplicada às notas " +
                        "(IBS UF {1:0.###}%, IBS municipal {2:0.###}%, CBS {3:0.###}%), mas a revisão era " +
                        "recomendada para {4:dd/MM/yyyy}. Fonte registrada: {5} (consultada em {6:dd/MM/yyyy}). ",
                        regra.VigenciaInicio, regra.AliquotaIbsUf, regra.AliquotaIbsMun, regra.AliquotaCbs,
                        revisaoEm, regra.FonteOficial, regra.ConsultadoEm) +
                    "Confira a Nota Técnica vigente com o " +
                    "contador e atualize o catálogo se as alíquotas mudaram.",
                OcorridoEm: BrazilTime.DateToUtcStart(revisaoEm.ToDateTime(TimeOnly.MinValue)),
                Link: "/admin/fiscal"),
        };
    }

    // ── Painel e ações ────────────────────────────────────────────────────────

    public async Task<PainelAlertasFiscaisDto> ListarAsync(
        bool incluirResolvidos = false, CancellationToken ct = default)
    {
        var query = _db.AlertasFiscais.AsNoTracking();
        if (!incluirResolvidos) query = query.Where(a => a.ResolvidoEm == null);

        // A severidade é persistida como TEXTO (para a coluna ser legível em
        // consulta manual ao banco), então ordenar pela coluna daria ordem
        // alfabética — "Alta" antes de "Critica", exatamente o inverso do que o
        // painel precisa. A ordem de gravidade é explícita aqui.
        var alertas = await query
            .OrderBy(a => a.ResolvidoEm == null ? 0 : 1)
            .ThenBy(a => a.Severidade == SeveridadeAlertaFiscal.Critica ? 0
                       : a.Severidade == SeveridadeAlertaFiscal.Alta    ? 1
                       : 2)
            .ThenBy(a => a.OcorridoEm)
            .Take(200)
            .ToListAsync(ct);

        var userIds = alertas
            .SelectMany(a => new[] { a.ResponsavelUserId, a.ResolvidoPorUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var nomes = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        string? Nome(Guid? id) => id.HasValue && nomes.TryGetValue(id.Value, out var n) ? n : null;

        var dtos = alertas
            .Select(a => AlertaFiscalMapper.ToDto(a, Nome(a.ResponsavelUserId), Nome(a.ResolvidoPorUserId)))
            .ToList();
        var abertos = dtos.Where(a => a.EstaAberto).ToList();

        return new PainelAlertasFiscaisDto(
            Alertas: dtos,
            TotalAbertos: abertos.Count,
            Criticos: abertos.Count(a => a.Severidade == nameof(SeveridadeAlertaFiscal.Critica)),
            Altos: abertos.Count(a => a.Severidade == nameof(SeveridadeAlertaFiscal.Alta)),
            Medios: abertos.Count(a => a.Severidade == nameof(SeveridadeAlertaFiscal.Media)),
            SemResponsavel: abertos.Count(a => a.ResponsavelUserId is null),
            MaisAntigoOcorridoEm: abertos.Count == 0 ? null : abertos.Min(a => a.OcorridoEm));
    }

    public async Task<AlertaFiscal> AtribuirResponsavelAsync(
        Guid alertaId, Guid? responsavelUserId, CancellationToken ct = default)
    {
        var alerta = await _db.AlertasFiscais.FindAsync(new object?[] { alertaId }, ct)
            ?? throw new InvalidOperationException("Alerta fiscal não encontrado.");

        if (responsavelUserId.HasValue)
        {
            var existe = await _db.Users.AnyAsync(
                u => u.Id == responsavelUserId.Value && u.IsActive, ct);
            if (!existe)
                throw new InvalidOperationException("O responsável informado não é um usuário ativo.");
        }

        alerta.ResponsavelUserId     = responsavelUserId;
        alerta.ResponsavelDefinidoEm = responsavelUserId.HasValue ? DateTime.UtcNow : null;
        alerta.AtualizadoEm          = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return alerta;
    }

    public async Task<AlertaFiscal> ResolverAsync(
        Guid alertaId, Guid usuarioId, string observacao, CancellationToken ct = default)
    {
        observacao = observacao?.Trim() ?? string.Empty;
        if (observacao.Length < 5)
            throw new InvalidOperationException(
                "Descreva em poucas palavras o que foi feito — a confirmação de resolução precisa ser auditável.");
        if (observacao.Length > 500)
            throw new InvalidOperationException("A observação de resolução deve ter no máximo 500 caracteres.");

        var alerta = await _db.AlertasFiscais.FindAsync(new object?[] { alertaId }, ct)
            ?? throw new InvalidOperationException("Alerta fiscal não encontrado.");
        if (alerta.ResolvidoEm is not null)
            return alerta; // idempotente: confirmar duas vezes não é erro

        alerta.ResolvidoEm              = DateTime.UtcNow;
        alerta.ResolvidoPorUserId       = usuarioId;
        alerta.ResolucaoObservacao      = observacao;
        alerta.ResolvidoAutomaticamente = false;
        alerta.AtualizadoEm             = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Alerta fiscal {AlertaId} ({Tipo}) marcado como resolvido por {UsuarioId}.",
            alerta.Id, alerta.Tipo, usuarioId);
        return alerta;
    }

    // ── Utilitários ───────────────────────────────────────────────────────────

    /// <summary>
    /// Decide se o envio mensal de XMLs ao contador está atrasado. Isolado do
    /// banco porque é a única detecção cuja resposta depende do dia do mês — e
    /// uma regra assim precisa ser testável sem esperar a data virar.
    ///
    /// Só vale a partir do dia 2: o job de envio roda no dia 1, e cobrar antes
    /// dele transformaria todo primeiro dia do mês em alarme falso.
    /// </summary>
    internal static bool ExportacaoMensalEstaAtrasada(
        DateTime hojeBr, DateTime? ultimoEnvioUtc, string? emailContador)
    {
        if (hojeBr.Day < 2) return false;
        if (string.IsNullOrWhiteSpace(emailContador)) return false;

        return ultimoEnvioUtc is not { } enviado
            || enviado.Year != hojeBr.Year
            || enviado.Month != hojeBr.Month;
    }

    /// <summary>Compacta números consecutivos em faixas ("500-503, 507") — uma
    /// lista de 40 números soltos não cabe no alerta nem ajuda ninguém.</summary>
    internal static string ResumirFaixas(IReadOnlyList<int> numeros, int maxFaixas = 8)
    {
        if (numeros.Count == 0) return string.Empty;

        var ordenados = numeros.Distinct().OrderBy(n => n).ToList();
        var faixas = new List<string>();
        var inicio = ordenados[0];
        var anterior = inicio;

        foreach (var numero in ordenados.Skip(1))
        {
            if (numero == anterior + 1) { anterior = numero; continue; }
            faixas.Add(inicio == anterior ? $"{inicio}" : $"{inicio}-{anterior}");
            inicio = anterior = numero;
        }
        faixas.Add(inicio == anterior ? $"{inicio}" : $"{inicio}-{anterior}");

        return faixas.Count <= maxFaixas
            ? string.Join(", ", faixas)
            : string.Join(", ", faixas.Take(maxFaixas)) + $" e mais {faixas.Count - maxFaixas} faixa(s)";
    }

    private static string Truncar(string texto, int limite) =>
        texto.Length <= limite ? texto : texto[..(limite - 1)] + "…";
}
