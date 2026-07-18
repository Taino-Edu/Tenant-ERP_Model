// =============================================================================
// NfceEmissionService.cs — Motor de emissão de NFC-e via DFe.NET
//
// Monta o objeto NFe (ide/emit/dest/det/total/pag), assina com o certificado
// A1 do FiscalConfig e transmite à SEFAZ via NFe.Servicos.ServicosNFe.
//
// Decisões já verificadas contra documentação oficial / prática de mercado:
//  - PIS/COFINS sempre CST 99 ("Outras Operações") com alíquota zero: confirmado
//    como o padrão de fato usado por optantes do Simples Nacional (o DAS já
//    unifica essas contribuições — não há CST federal específico exigido pela
//    Receita pra esse regime na NFC-e).
//  - CSOSN: suporta 101, 102, 103, 300, 400, 500, 900 (os únicos que fazem
//    sentido pra um lojista que NÃO é substituto tributário). 201/202/203
//    (ICMS-ST como substituto) são bloqueados de propósito — exigem MVA/base
//    reduzida que ninguém aqui calcula sozinho; ver MontarIcmsSimplesNacional.
//  - dhEmi usa nota.CreatedAt (momento real da venda/fechamento da comanda),
//    não o momento da transmissão — importante pro caso comum de retry
//    automático rodar minutos/horas depois da venda de verdade.
//  - Todos os timestamps enviados à SEFAZ usam o fuso America/Sao_Paulo
//    explicitamente (ParaBrasil/AgoraBrasil), independente do fuso do
//    servidor onde a API está hospedada.
//  - Numeração da NFC-e é reservada com UPDATE...RETURNING atômico no
//    Postgres — não há race condition entre dois fechamentos simultâneos.
//  - QR Code é gerado pela própria lib (Zeus.Net.NFe.NFCe / ExtinfNFeSupl),
//    que já sabe a URL certa por estado — não reinventamos hash/URL na mão.
//
// Simplificações conhecidas ainda pendentes (documentadas para revisão futura
// com o contador):
//  - Não há modo de contingência formal da SEFAZ (offline/EPEC/SVC) — se a
//    SEFAZ estiver fora do ar, a nota só fica PendenteEmissao aguardando o
//    retry automático. Pra um volume pequeno de vendas isso tende a resolver
//    sozinho em minutos, mas não é o mecanismo oficial previsto em lei.
//  - Cancelamento e inutilização assumem que "sem exceção + cStat esperado" é
//    sucesso — não foi possível testar contra a SEFAZ real neste ambiente.
// =============================================================================

using System.Security.Cryptography.X509Certificates;
using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using DFe.Classes.Entidades;
using DFe.Classes.Flags;
using DFe.Utils;
using Microsoft.EntityFrameworkCore;
using NFe.Classes;
using NFe.Classes.Informacoes;
using NFe.Classes.Informacoes.Destinatario;
using NFe.Classes.Informacoes.Detalhe;
using NFe.Classes.Informacoes.Detalhe.Tributacao;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal.Tipos;
using NFe.Classes.Informacoes.Emitente;
using NFe.Classes.Informacoes.Identificacao;
using NFe.Classes.Informacoes.Identificacao.Tipos;
using NFe.Classes.Informacoes.Pagamento;
using NFe.Classes.Informacoes.Total;
using NFe.Classes.Servicos.Tipos;
using NFe.Servicos;
using NFe.Servicos.Retorno;
using NFe.Utils;
using NFe.Utils.Consulta;
using Npgsql;
using NFe.Utils.InformacoesSuplementares;
using NFe.Utils.NFe;
using NfeDocumento = NFe.Classes.NFe;

namespace CardGameStore.Services.Implementations;

public class NfceEmissionService : INfceEmissionService
{
    // Janela legal pra cancelar uma NFC-e após autorizada (padrão nacional: 30 minutos).
    private static readonly TimeSpan JanelaCancelamento = TimeSpan.FromMinutes(30);

    // Trava contra loop de reprocessamento em nota permanentemente quebrada — só se aplica a
    // PendenteEmissao/Rejeitada. Contingência (AutorizadaContingencia) usa prazo por TEMPO
    // (ver PrazoLegalContingencia): a 10 tentativas em ciclos de 15 min (FiscalRetryBackgroundService)
    // desistiria em ~2,5h, bem antes do prazo legal de 24h da NT 2015.002.
    private const int MaxTentativasReprocessamento = 10;

    // Prazo legal (NT 2015.002) pra uma NFC-e emitida em contingência offline ser retransmitida
    // e autorizada de verdade pela SEFAZ — passado isso, a venda fica permanentemente sem
    // documento fiscal válido e exige ação manual/regularização com o contador.
    private static readonly TimeSpan PrazoLegalContingencia = TimeSpan.FromHours(24);

    // Log de alerta quando a contingência está perto do prazo legal, pra dar chance de ação
    // manual antes de virar problema irreversível.
    private static readonly TimeSpan AlertaContingencia = TimeSpan.FromHours(20);

    // Todo horário enviado à SEFAZ usa esse fuso explicitamente — nunca o fuso
    // do servidor (containers em nuvem tipicamente rodam em UTC por padrão).
    private static readonly TimeZoneInfo FusoBrasil = BrazilTime.Zone;

    private static DateTimeOffset AgoraBrasil() =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, FusoBrasil);

    private static DateTimeOffset ParaBrasil(DateTime momentoUtc) =>
        TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(momentoUtc, DateTimeKind.Utc)), FusoBrasil);

    /// <summary>
    /// Distingue "SEFAZ inalcançável" (entra em contingência) de uma rejeição de negócio de
    /// verdade (SEFAZ respondeu, só não autorizou). Só os tipos de exceção claramente ligados
    /// a rede/timeout contam — qualquer outra coisa inesperada cai no catch genérico de fora
    /// (vira PendenteEmissao) em vez de declarar contingência por um motivo que pode ser bug.
    /// </summary>
    internal static bool EhFalhaDeConectividade(Exception ex) =>
        ex is System.Net.Http.HttpRequestException
           or System.Net.WebException
           or System.Net.Sockets.SocketException
           or TimeoutException
           or TaskCanceledException
        || (ex.InnerException is not null && EhFalhaDeConectividade(ex.InnerException));

    private readonly AppDbContext                _db;
        private readonly EncryptionService           _enc;
    private readonly ILogger<NfceEmissionService> _logger;

    public NfceEmissionService(AppDbContext db, EncryptionService enc, ILogger<NfceEmissionService> logger)
    {
        _db     = db;
        _enc    = enc;
        _logger = logger;
    }

    public async Task<NotaFiscalEmitida> EmitirParaComandaAsync(Guid comandaId) =>
        await EmitirAsync(NotaFiscalOrigem.Comanda, comandaId, null);

    public async Task<NotaFiscalEmitida> EmitirParaVendaAvulsaAsync(Guid vendaAvulsaId) =>
        await EmitirAsync(NotaFiscalOrigem.VendaAvulsa, null, vendaAvulsaId);

    public async Task<NotaFiscalEmitida> ReprocessarAsync(Guid notaId)
    {
        var nota = await _db.NotasFiscaisEmitidas.FindAsync(notaId)
            ?? throw new InvalidOperationException($"Nota {notaId} não encontrada.");

        if (nota.Status is not (NotaFiscalStatus.PendenteEmissao or NotaFiscalStatus.Rejeitada or NotaFiscalStatus.AutorizadaContingencia))
            return nota; // Autorizada/Cancelada não têm o que reprocessar — devolve como está.

        if (nota.Status == NotaFiscalStatus.AutorizadaContingencia)
        {
            // Prazo por TEMPO, não por contagem — 10 tentativas em ciclos de 15 min
            // esgotariam em ~2,5h, bem antes do prazo legal de 24h. Também vale pro botão de
            // retry manual: contingência nunca é bloqueada pelo contador de tentativas comum.
            if (nota.DhContingencia.HasValue && DateTime.UtcNow - nota.DhContingencia.Value > PrazoLegalContingencia)
            {
                _logger.LogError(
                    "NFC-e {NotaId} em contingência desde {DhContingencia:o} ultrapassou o prazo legal de 24h sem " +
                    "retransmitir — a venda ficou sem documento fiscal válido. Requer ação manual (regularização " +
                    "com o contador).", nota.Id, nota.DhContingencia);
                return nota;
            }

            if (nota.DhContingencia.HasValue && DateTime.UtcNow - nota.DhContingencia.Value > AlertaContingencia)
                _logger.LogWarning(
                    "NFC-e {NotaId} em contingência desde {DhContingencia:o} está se aproximando do prazo legal " +
                    "de 24h sem retransmitir com sucesso.", nota.Id, nota.DhContingencia);
        }
        else if (nota.TentativasReprocessamento >= MaxTentativasReprocessamento)
        {
            _logger.LogWarning(
                "NFC-e {NotaId} atingiu o limite de {Max} tentativas de reprocessamento — não vai tentar de novo.",
                nota.Id, MaxTentativasReprocessamento);
            return nota;
        }

        nota.TentativasReprocessamento++;
        await _db.SaveChangesAsync();

        await ExecutarComTratamentoDeErroAsync(nota, async () =>
        {
            var dados = nota.Origem == NotaFiscalOrigem.Comanda
                ? await CarregarDadosComandaAsync(nota.ComandaId!.Value)
                : await CarregarDadosVendaAvulsaAsync(nota.VendaAvulsaId!.Value);

            nota.ValorTotalEmCentavos = dados.Itens.Sum(i => i.SubtotalCentavos);
            await TransmitirAsync(nota, dados);
        });

        return nota;
    }

    public async Task<NotaFiscalEmitida> CancelarAsync(Guid notaId, string justificativa)
    {
        if (string.IsNullOrWhiteSpace(justificativa) || justificativa.Trim().Length < 15)
            throw new InvalidOperationException("A justificativa do cancelamento precisa ter pelo menos 15 caracteres (exigência da SEFAZ).");

        var nota = await _db.NotasFiscaisEmitidas.FindAsync(notaId)
            ?? throw new InvalidOperationException($"Nota {notaId} não encontrada.");

        if (nota.Status != NotaFiscalStatus.Autorizada)
            throw new InvalidOperationException("Só é possível cancelar uma nota Autorizada.");

        // F14: a janela conta a partir da AUTORIZAÇÃO de verdade (AutorizadoEm), não de
        // EmitidoEm — que em contingência preserva o momento da venda, não da autorização
        // pela SEFAZ. Sem essa distinção, uma nota retransmitida horas depois (dentro do
        // prazo legal de 24h de F2) nasceria autorizada já fora da janela de cancelamento,
        // incancelável desde o primeiro segundo.
        if (nota.AutorizadoEm is null || DateTime.UtcNow - nota.AutorizadoEm.Value > JanelaCancelamento)
            throw new InvalidOperationException(
                $"Fora da janela legal de cancelamento ({JanelaCancelamento.TotalMinutes:0} minutos após a autorização).");

        var (cfg, cfgServico, certificado, _, _, _) = await AbrirConfiguracaoSefazAsync();
        using var _certDispose = certificado;

        using var servico = new ServicosNFe(cfgServico, certificado);
        var retorno = servico.RecepcaoEventoCancelamento(
            idlote: 1, sequenciaEvento: 1,
            protocoloAutorizacao: nota.Protocolo!, chaveNFe: nota.ChaveAcesso!,
            justificativa: justificativa.Trim(), cpfcnpj: cfg.Cnpj, dhEvento: AgoraBrasil());

        var infEvento = retorno.Retorno?.retEvento?.FirstOrDefault()?.infEvento;
        if (infEvento is null || infEvento.cStat is not (135 or 136))
        {
            var motivo = infEvento?.xMotivo ?? retorno.RetornoStr ?? "SEFAZ não retornou motivo.";
            throw new InvalidOperationException($"SEFAZ rejeitou o cancelamento: {motivo}");
        }

        nota.Status                    = NotaFiscalStatus.Cancelada;
        nota.CanceladoEm                = DateTime.UtcNow;
        nota.JustificativaCancelamento  = justificativa.Trim();
        // F9: protocolo e XML do evento eram descartados (só cStat era lido) — sem eles não
        // há prova documental do cancelamento pra guarda de 5 anos / ZIP do contador.
        nota.ProtocoloCancelamento      = infEvento.nProt;
        nota.XmlEventoCancelamento      = retorno.ProcEventosNFe?.FirstOrDefault()?.ObterXmlString();
        await _db.SaveChangesAsync();

        _logger.LogInformation("NFC-e {NotaId} (chave {Chave}) cancelada com sucesso.", nota.Id, nota.ChaveAcesso);
        return nota;
    }

    public async Task<CupomDto?> ObterCupomAsync(Guid notaId)
    {
        var nota = await _db.NotasFiscaisEmitidas.FindAsync(notaId);
        if (nota is null) return null;

        var dados = nota.Origem == NotaFiscalOrigem.Comanda
            ? await CarregarDadosComandaAsync(nota.ComandaId!.Value)
            : await CarregarDadosVendaAvulsaAsync(nota.VendaAvulsaId!.Value);

        var cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        var endereco = cfg is null ? "" : $"{cfg.Logradouro}, {cfg.Numero} - {cfg.Bairro} - {cfg.Municipio}/{cfg.Uf}";

        return new CupomDto(
            RazaoSocial: cfg?.RazaoSocial ?? "",
            Cnpj:        cfg?.Cnpj ?? "",
            Endereco:    endereco,
            ChaveAcesso: nota.ChaveAcesso,
            Protocolo:   nota.Protocolo,
            EmitidoEm:   nota.EmitidoEm,
            Serie:       nota.Serie ?? 0,
            Numero:      nota.Numero ?? 0,
            Status:      nota.Status.ToString(),
            Itens:       dados.Itens.Select(i => new CupomItemDto(i.Nome, i.Quantidade, i.PrecoUnitarioCentavos, i.SubtotalCentavos)).ToList(),
            ValorTotalCentavos: nota.ValorTotalEmCentavos,
            FormaPagamento: dados.FormaPagamento,
            QrCodeUrl:   nota.UrlQrCode);
    }

    // ── Orquestração ──────────────────────────────────────────────────────────

    private async Task<NotaFiscalEmitida> EmitirAsync(NotaFiscalOrigem origem, Guid? comandaId, Guid? vendaAvulsaId)
    {
        var nota = new NotaFiscalEmitida
        {
            Origem        = origem,
            ComandaId     = comandaId,
            VendaAvulsaId = vendaAvulsaId,
            Status        = NotaFiscalStatus.PendenteEmissao,
        };
        _db.NotasFiscaisEmitidas.Add(nota);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg &&
            (pg.ConstraintName == "ix_notas_fiscais_comanda_unica" || pg.ConstraintName == "ix_notas_fiscais_venda_avulsa_unica"))
        {
            // F8: corrida entre duas chamadas concorrentes pra emitir NFC-e da mesma origem —
            // o guard de aplicação em FiscalController (checa-então-insere) não é atômico;
            // esse índice único no banco é a rede de segurança de verdade. Descarta a linha
            // que perdeu a corrida e devolve a nota que já existe pra essa origem, em vez de
            // derrubar o caller com exceção (quebraria a garantia de "nunca lança" do serviço).
            _db.ChangeTracker.Clear();
            return origem == NotaFiscalOrigem.Comanda
                ? await _db.NotasFiscaisEmitidas.FirstAsync(n => n.ComandaId == comandaId)
                : await _db.NotasFiscaisEmitidas.FirstAsync(n => n.VendaAvulsaId == vendaAvulsaId);
        }

        await ExecutarComTratamentoDeErroAsync(nota, async () =>
        {
            var dados = origem == NotaFiscalOrigem.Comanda
                ? await CarregarDadosComandaAsync(comandaId!.Value)
                : await CarregarDadosVendaAvulsaAsync(vendaAvulsaId!.Value);

            nota.ValorTotalEmCentavos = dados.Itens.Sum(i => i.SubtotalCentavos);
            await TransmitirAsync(nota, dados);
        });

        return nota;
    }

    /// <summary>
    /// Garantia central do serviço: emissão/reprocessamento NUNCA lança exceção —
    /// falha vira PendenteEmissao (com log apropriado) em vez de derrubar o caller.
    /// </summary>
    private async Task ExecutarComTratamentoDeErroAsync(NotaFiscalEmitida nota, Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch (ComandaCanceladaException)
        {
            // Comanda foi cancelada antes da nota ser transmitida à SEFAZ — nunca chegou a
            // existir de verdade, então não há evento de cancelamento a fazer, só anular
            // localmente para o retry automático parar de tentar emitir esta nota.
            nota.Status                   = NotaFiscalStatus.Cancelada;
            nota.CanceladoEm              = DateTime.UtcNow;
            nota.JustificativaCancelamento = "Comanda cancelada antes da emissão fiscal — nota anulada automaticamente (nunca transmitida à SEFAZ).";
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "NFC-e {NotaId} anulada automaticamente — comanda de origem foi cancelada antes da transmissão.", nota.Id);
        }
        catch (FiscalNaoConfiguradoException ex)
        {
            // Estado esperado enquanto o admin não termina de configurar — não é uma falha real.
            nota.MotivoRejeicao = $"Configuração fiscal pendente: {ex.Message}";
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "NFC-e {NotaId} ({Origem}) não emitida — {Motivo} Nota registrada como PendenteEmissao.",
                nota.Id, nota.Origem, ex.Message);
        }
        catch (Exception ex)
        {
            // Nunca deixa a emissão fiscal derrubar o fechamento da venda — mas isso AQUI
            // é um erro de verdade (motor configurado mas falhou), por isso LogError.
            _logger.LogError(ex,
                "Falha ao emitir NFC-e {NotaId} ({Origem}) — motor configurado mas a transmissão falhou. " +
                "Nota registrada como PendenteEmissao para nova tentativa.", nota.Id, nota.Origem);
        }
    }

    // ── Carregamento dos dados de origem ──────────────────────────────────────

    internal record ItemFiscal(
        string Nome, string Ncm, string Cfop, string? Csosn, decimal? PercentualCreditoSn,
        int Quantidade, int PrecoUnitarioCentavos, int SubtotalCentavos);

    private record DadosEmissao(
        List<ItemFiscal> Itens, string FormaPagamento, string? ClienteCpf,
        string? SegundaFormaPagamento, int SegundoValorCentavos);

    private async Task<DadosEmissao> CarregarDadosComandaAsync(Guid comandaId)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.NaturezaOperacao)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException($"Comanda {comandaId} não encontrada para emissão fiscal.");

        if (comanda.Status == ComandaStatus.Cancelada)
            throw new ComandaCanceladaException(comandaId);

        var padrao = await _db.NaturezasOperacao.FirstOrDefaultAsync(n => n.IsPadrao);

        var semNcm = comanda.Items
            .Where(item => string.IsNullOrWhiteSpace(item.Product?.Ncm))
            .Select(item => item.ItemNameSnapshot)
            .Distinct()
            .ToList();
        if (semNcm.Count > 0)
            throw new FiscalNaoConfiguradoException(
                $"Produto(s) sem NCM cadastrado (Admin > Estoque): {string.Join(", ", semNcm)}. " +
                "O NCM deve vir da nota fiscal de compra do produto — não é inventado pelo sistema.");

        var itens = comanda.Items.Select(item => new ItemFiscal(
            Nome:                 item.ItemNameSnapshot,
            Ncm:                  item.Product!.Ncm!,
            Cfop:                 item.Product?.NaturezaOperacao?.Cfop ?? padrao?.Cfop ?? "5102",
            Csosn:                item.Product?.NaturezaOperacao?.Csosn ?? padrao?.Csosn ?? "102",
            PercentualCreditoSn:  item.Product?.NaturezaOperacao?.PercentualCreditoIcmsSn ?? padrao?.PercentualCreditoIcmsSn,
            Quantidade:           item.Quantity,
            PrecoUnitarioCentavos: item.UnitPriceInCents,
            SubtotalCentavos:     item.SubtotalInCents
        )).ToList();

        return new DadosEmissao(
            itens, comanda.PaymentMethod ?? PaymentMethod.Dinheiro, comanda.User?.Cpf,
            comanda.SecondPaymentMethod, comanda.SecondPaymentAmountInCents);
    }

    private async Task<DadosEmissao> CarregarDadosVendaAvulsaAsync(Guid vendaAvulsaId)
    {
        var venda = await _db.VendasAvulsas.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vendaAvulsaId)
            ?? throw new InvalidOperationException($"Venda avulsa {vendaAvulsaId} não encontrada para emissão fiscal.");

        var productIds = venda.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Include(p => p.NaturezaOperacao)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var padrao = await _db.NaturezasOperacao.FirstOrDefaultAsync(n => n.IsPadrao);

        var semNcm = venda.Items
            .Where(item => { products.TryGetValue(item.ProductId, out var p); return string.IsNullOrWhiteSpace(p?.Ncm); })
            .Select(item => item.ProductName)
            .Distinct()
            .ToList();
        if (semNcm.Count > 0)
            throw new FiscalNaoConfiguradoException(
                $"Produto(s) sem NCM cadastrado (Admin > Estoque): {string.Join(", ", semNcm)}. " +
                "O NCM deve vir da nota fiscal de compra do produto — não é inventado pelo sistema.");

        var itens = venda.Items.Select(item =>
        {
            products.TryGetValue(item.ProductId, out var product);
            return new ItemFiscal(
                Nome:                 item.ProductName,
                Ncm:                  product!.Ncm!,
                Cfop:                 product?.NaturezaOperacao?.Cfop ?? padrao?.Cfop ?? "5102",
                Csosn:                product?.NaturezaOperacao?.Csosn ?? padrao?.Csosn ?? "102",
                PercentualCreditoSn:  product?.NaturezaOperacao?.PercentualCreditoIcmsSn ?? padrao?.PercentualCreditoIcmsSn,
                Quantidade:           item.Quantity,
                PrecoUnitarioCentavos: item.UnitPriceInCents,
                SubtotalCentavos:     item.SubtotalInCents
            );
        }).ToList();

        string? cpf = null;
        if (venda.UserId.HasValue)
            cpf = (await _db.Users.FindAsync(venda.UserId.Value))?.Cpf;

        return new DadosEmissao(
            itens, venda.PaymentMethod, cpf,
            venda.SecondPaymentMethod, venda.SecondPaymentAmountInCents);
    }

    // ── Montagem, assinatura e transmissão ─────────────────────────────────────

    /// <summary>
    /// Carrega o certificado (descriptografado) e monta a config de conexão com a SEFAZ,
    /// reaproveitada por emissão, cancelamento e inutilização.
    /// </summary>
    private async Task<(FiscalConfig cfg, ConfiguracaoServico cfgServico, X509Certificate2 certificado,
        ConfiguracaoCertificado cfgCertificado, Estado estado, TipoAmbiente ambiente)>
        AbrirConfiguracaoSefazAsync()
    {
        var cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        if (cfg is null || !cfg.CertificadoConfigurado)
            throw new FiscalNaoConfiguradoException("Certificado digital ainda não configurado.");

        if (string.IsNullOrWhiteSpace(cfg.RazaoSocial) || string.IsNullOrWhiteSpace(cfg.Logradouro) ||
            string.IsNullOrWhiteSpace(cfg.CodigoMunicipioIbge) || string.IsNullOrWhiteSpace(cfg.Uf))
            throw new FiscalNaoConfiguradoException("Dados da empresa (razão social/endereço) incompletos em Admin > Fiscal.");

        // F12: pré-voo cobria só certificado/endereço — CNPJ default "" gera chave de acesso
        // inválida, e sem CSC a transmissão segue sem infNFeSupl.qrCode (QR obrigatório em
        // NFC-e) → rejeição certa da SEFAZ, além de queimar um número à toa. Falha ANTES de
        // reservar número (AbrirConfiguracaoSefazAsync roda antes de ReservarProximoNumeroNfceAsync).
        if (string.IsNullOrWhiteSpace(cfg.Cnpj) || cfg.Cnpj.Length != 14)
            throw new FiscalNaoConfiguradoException("CNPJ ausente ou inválido em Admin > Fiscal — precisa ter 14 dígitos.");

        if (string.IsNullOrWhiteSpace(cfg.CscId) || string.IsNullOrWhiteSpace(cfg.CscToken))
            throw new FiscalNaoConfiguradoException(
                "CSC (Código de Segurança do Contribuinte) não configurado em Admin > Fiscal — sem ele a NFC-e " +
                "transmite sem QR Code (obrigatório) e é rejeitada pela SEFAZ.");

        var pfxBytes    = Convert.FromBase64String(_enc.Decrypt(cfg.CertificadoPfxEncrypted!));
        var senha       = _enc.Decrypt(cfg.CertificadoSenhaEncrypted!);
        var certificado = Pkcs12Loader.Abrir(pfxBytes, senha);

        // Bloqueia ANTES de qualquer tentativa de rede: um certificado vencido (upload validou
        // na hora, mas venceu depois — o upload não bloqueia mais isso, mas notas antigas
        // continuam valendo) faz o handshake mTLS falhar com HttpRequestException, que
        // EhFalhaDeConectividade classificaria como "SEFAZ fora do ar" e cairia em contingência
        // offline (tpEmis=9) indevidamente — cupom que nunca vai autorizar. Falhar aqui, antes
        // do handshake, garante que erro de certificado nunca vira contingência.
        var certificadoNotAfter = certificado.NotAfter;
        if (certificadoNotAfter.ToUniversalTime() < DateTime.UtcNow)
        {
            certificado.Dispose();
            throw new FiscalNaoConfiguradoException(
                $"Certificado digital vencido em {certificadoNotAfter:dd/MM/yyyy} — atualize em Admin > Fiscal antes de emitir.");
        }

        var cfgCertificado = new ConfiguracaoCertificado { ArrayBytesArquivo = pfxBytes, Senha = senha };

        var estado   = Enum.Parse<Estado>(cfg.Uf);
        var ambiente = cfg.Ambiente == AmbienteFiscal.Producao ? TipoAmbiente.Producao : TipoAmbiente.Homologacao;

        var cfgServico = new ConfiguracaoServico
        {
            cUF             = estado,
            tpAmb           = ambiente,
            ModeloDocumento = ModeloDocumento.NFCe,
            VersaoLayout    = VersaoServico.Versao400,
            TimeOut         = 15000,
            // Sem XSDs locais empacotados — a SEFAZ valida o schema no recebimento de qualquer forma.
            ValidarSchemas  = false,
        };

        return (cfg, cfgServico, certificado, cfgCertificado, estado, ambiente);
    }

    /// <summary>
    /// Reserva atomicamente o próximo número de NFC-e via UPDATE...RETURNING no Postgres —
    /// evita que dois fechamentos de comanda simultâneos peguem o mesmo número (a leitura +
    /// incremento em memória do EF não é segura contra concorrência entre requisições).
    /// </summary>
    private async Task<int> ReservarProximoNumeroNfceAsync(Guid fiscalConfigId)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE fiscal_config SET proximo_numero_nfce = proximo_numero_nfce + 1, updated_at = now() " +
            "WHERE id = @id RETURNING proximo_numero_nfce - 1";
        var param = cmd.CreateParameter();
        param.ParameterName = "id";
        param.Value = fiscalConfigId;
        cmd.Parameters.Add(param);

        var resultado = await cmd.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Não foi possível reservar o número da NFC-e — FiscalConfig não encontrado.");
        return Convert.ToInt32(resultado);
    }

    private async Task TransmitirAsync(NotaFiscalEmitida nota, DadosEmissao dados)
    {
        var (cfg, cfgServico, certificado, cfgCertificado, estado, ambiente) = await AbrirConfiguracaoSefazAsync();
        using var _certDispose = certificado;

        // Monta os itens (e valida CSOSN) ANTES de reservar o número — uma Natureza de
        // Operação mal configurada não pode queimar um número de NFC-e sem transmitir nada.
        var detItens = dados.Itens.Select((item, idx) => MontarItem(item, idx + 1)).ToList();

        // Se esta nota já entrou em contingência offline numa tentativa anterior, a
        // retransmissão precisa reconstruir a MESMA chave de acesso (já mostrada ao
        // cliente no cupom) — número, cNf e tpEmis não podem mudar entre tentativas.
        var jaEmContingencia = nota.CnfContingencia.HasValue;
        var numero = jaEmContingencia ? nota.Numero!.Value : await ReservarProximoNumeroNfceAsync(cfg.Id);

        // F6: persiste o número reservado JÁ AQUI, antes de assinar/transmitir — se o
        // processo cair em qualquer ponto daqui pra frente, o número fica registrado na nota
        // em vez de sumir num buraco de numeração sem rastro (a reserva em si já avançou
        // proximo_numero_nfce atomicamente; só a gravação na nota que ficava pra depois).
        if (!jaEmContingencia)
        {
            nota.Serie  = cfg.SerieNfce;
            nota.Numero = numero;
            await _db.SaveChangesAsync();
        }

        // dhEmi é o momento REAL da venda (quando a nota foi criada como PendenteEmissao),
        // não o momento desta transmissão — que pode ser minutos/horas depois num retry.
        var dhEmi  = ParaBrasil(nota.CreatedAt);
        var cNf    = jaEmContingencia ? nota.CnfContingencia!.Value : Random.Shared.Next(10_000_000, 99_999_999);
        var tpEmis = jaEmContingencia ? TipoEmissao.teOffLine : TipoEmissao.teNormal;
        var chave  = ChaveFiscal.ObterChave(estado, dhEmi, cfg.Cnpj, ModeloDocumento.NFCe, cfg.SerieNfce, numero, (int)tpEmis, cNf);

        var municipioIbge = long.Parse(cfg.CodigoMunicipioIbge!);
        var valorTotal     = dados.Itens.Sum(i => i.SubtotalCentavos) / 100m;

        var nfe = new NfeDocumento
        {
            infNFe = new infNFe
            {
                versao = "4.00",
                ide = new ide
                {
                    cUF     = estado,
                    cNF     = cNf.ToString("D8"),
                    natOp   = "Venda de mercadoria",
                    mod     = ModeloDocumento.NFCe,
                    serie   = cfg.SerieNfce,
                    nNF     = numero,
                    dhEmi   = dhEmi,
                    tpNF    = TipoNFe.tnSaida,
                    idDest  = DestinoOperacao.doInterna,
                    cMunFG  = municipioIbge,
                    tpImp   = TipoImpressao.tiNFCe,
                    tpEmis  = tpEmis,
                    cDV     = chave.DigitoVerificador,
                    tpAmb   = ambiente,
                    finNFe  = FinalidadeNFe.fnNormal,
                    indFinal = ConsumidorFinal.cfConsumidorFinal,
                    indPres  = PresencaComprador.pcPresencial,
                    procEmi  = ProcessoEmissao.peAplicativoContribuinte,
                    verProc  = "1.0",
                },
                emit = new emit
                {
                    CNPJ  = cfg.Cnpj,
                    xNome = cfg.RazaoSocial,
                    IE    = string.IsNullOrWhiteSpace(cfg.InscricaoEstadual) ? null : cfg.InscricaoEstadual,
                    CRT   = MapCrt(cfg.RegimeTributario),
                    enderEmit = new enderEmit
                    {
                        xLgr    = cfg.Logradouro,
                        nro     = cfg.Numero ?? "S/N",
                        xCpl    = cfg.Complemento,
                        xBairro = cfg.Bairro ?? "-",
                        cMun    = municipioIbge,
                        xMun    = cfg.Municipio ?? "-",
                        UF      = estado,
                        CEP     = cfg.Cep,
                    },
                },
                dest = string.IsNullOrWhiteSpace(dados.ClienteCpf) ? null : new dest(VersaoServico.Versao400)
                {
                    CPF = dados.ClienteCpf,
                },
                det = detItens,
                total = new total
                {
                    ICMSTot = new ICMSTot
                    {
                        vBC = 0, vICMS = 0, vBCST = 0, vST = 0,
                        vProd    = valorTotal,
                        vFrete   = 0, vSeg = 0, vDesc = 0, vII = 0, vIPI = 0,
                        vPIS     = 0, vCOFINS = 0, vOutro = 0,
                        vNF      = valorTotal,
                    },
                },
                pag = new List<pag> { new pag { detPag = MontarDetPag(dados, valorTotal) } },
            },
        };

        // dhCont/xJust só existem (e são exigidos) em contingência offline (tpEmis=9) — a
        // lib só serializa esses campos quando fazem sentido pro tpEmis atual.
        if (jaEmContingencia)
        {
            nfe.infNFe.ide.dhCont = ParaBrasil(nota.DhContingencia!.Value);
            nfe.infNFe.ide.xJust  = nota.JustificativaContingencia;
        }

        nfe.Assina(cfgServico, certificado);

        // QR Code: usa a própria lib (sabe a URL certa de cada estado e o hash do CSC) em vez
        // de reimplementar isso na mão — evita erro de domínio/fórmula por estado.
        nfe.infNFeSupl = new infNFeSupl();
        var qrCodeUrl = string.IsNullOrWhiteSpace(cfg.CscId) || string.IsNullOrWhiteSpace(cfg.CscToken)
            ? null
            : ExtinfNFeSupl.ObterUrlQrCode(nfe.infNFeSupl, nfe, VersaoQrCode.QrCodeVersao2, cfg.CscId, cfg.CscToken, cfgCertificado);
        if (qrCodeUrl is not null)
            nfe.infNFeSupl.qrCode = qrCodeUrl;

        using var servico = new ServicosNFe(cfgServico, certificado);
        RetornoNFeAutorizacao retorno;
        try
        {
            retorno = servico.NFeAutorizacao(1, IndicadorSincronizacao.Sincrono, new List<NfeDocumento> { nfe }, false);
        }
        catch (Exception ex) when (EhFalhaDeConectividade(ex))
        {
            if (!jaEmContingencia)
            {
                // 1ª vez inalcançável nesta nota: entra em contingência offline agora. A chave
                // e o QR assinados acima (fora deste catch) foram calculados com tpEmis=Normal
                // — não servem pro cupom: tpEmis é o 35º dígito da chave de 44 posições, então
                // mudar pra contingência (tpEmis=9) troca a chave inteira. Reconstrói ide
                // (tpEmis/cDV/dhCont/xJust), reassina e regenera o QR ANTES de persistir, pra
                // bater exatamente com o que a retransmissão automática vai gerar depois (que já
                // usa tpEmis=9 desde o topo desta função, via jaEmContingencia).
                var dhContingencia            = DateTime.UtcNow;
                var justificativaContingencia = "Sem comunicação com o webservice da SEFAZ no momento da venda.";

                var chaveContingencia = ChaveFiscal.ObterChave(
                    estado, dhEmi, cfg.Cnpj, ModeloDocumento.NFCe, cfg.SerieNfce, numero, (int)TipoEmissao.teOffLine, cNf);

                nfe.infNFe.ide.tpEmis = TipoEmissao.teOffLine;
                nfe.infNFe.ide.cDV    = chaveContingencia.DigitoVerificador;
                nfe.infNFe.ide.dhCont = ParaBrasil(dhContingencia);
                nfe.infNFe.ide.xJust  = justificativaContingencia;

                // tpEmis/cDV/dhCont/xJust mudaram o conteúdo assinado — a assinatura de cima
                // não vale mais pra este XML.
                nfe.Assina(cfgServico, certificado);
                nfe.infNFeSupl = new infNFeSupl();
                var qrCodeContingencia = string.IsNullOrWhiteSpace(cfg.CscId) || string.IsNullOrWhiteSpace(cfg.CscToken)
                    ? null
                    : ExtinfNFeSupl.ObterUrlQrCode(nfe.infNFeSupl, nfe, VersaoQrCode.QrCodeVersao2, cfg.CscId, cfg.CscToken, cfgCertificado);
                if (qrCodeContingencia is not null)
                    nfe.infNFeSupl.qrCode = qrCodeContingencia;

                nota.Serie                     = cfg.SerieNfce;
                nota.Numero                    = numero;
                nota.CnfContingencia           = cNf;
                nota.DhContingencia            = dhContingencia;
                nota.JustificativaContingencia = justificativaContingencia;
                nota.Status                    = NotaFiscalStatus.AutorizadaContingencia;
                nota.ChaveAcesso                = chaveContingencia.Chave;
                nota.EmitidoEm                  = DateTime.UtcNow;
                nota.UrlQrCode                  = qrCodeContingencia;
                await _db.SaveChangesAsync();

                _logger.LogWarning(ex,
                    "NFC-e {NotaId} emitida em CONTINGÊNCIA offline — SEFAZ inalcançável no momento da venda. " +
                    "Chave/QR reconstruídos com tpEmis=9 antes da persistência. Retransmissão automática tentará " +
                    "no próximo ciclo.", nota.Id);
            }
            else
            {
                // Já estava em contingência e a SEFAZ continua inalcançável — tenta de novo depois.
                _logger.LogWarning(ex,
                    "NFC-e {NotaId} (em contingência desde {DhContingencia}) ainda não conseguiu retransmitir — " +
                    "SEFAZ continua inalcançável.", nota.Id, nota.DhContingencia);
            }
            return;
        }

        var protInfo = retorno.Retorno?.protNFe?.infProt;

        // Número já foi consumido, persistido atomicamente em ReservarProximoNumeroNfceAsync
        // e gravado na nota logo após a reserva (F6, acima) — autorizada ou não, a numeração
        // da NFC-e não pode ser reaproveitada sem inutilização.

        if (protInfo is not null && protInfo.cStat == 100)
        {
            nota.Status         = NotaFiscalStatus.Autorizada;
            nota.ChaveAcesso    = protInfo.chNFe ?? chave.Chave;
            nota.Protocolo      = protInfo.nProt;
            // Se veio de contingência, EmitidoEm já é o momento real da venda — não pisa nele
            // com o momento da confirmação tardia da SEFAZ.
            nota.EmitidoEm    ??= DateTime.UtcNow;
            // F14: AutorizadoEm é sempre o momento desta confirmação — mesmo em retransmissão
            // de contingência horas depois. É a partir daqui que a janela de cancelamento conta.
            nota.AutorizadoEm   = DateTime.UtcNow;
            // retorno.EnvioStr é o envelope <enviNFe> enviado (sem protNFe) — não é o documento
            // fiscal hábil. O nfeProc (NFe assinada + protNFe da SEFAZ) é o que a guarda de 5
            // anos e os sistemas contábeis exigem; a lib devolve o protocolo em retorno.Retorno.protNFe.
            var nfeProcAutorizado = new NFe.Classes.nfeProc
            {
                versao  = "4.00",
                NFe     = nfe,
                protNFe = retorno.Retorno!.protNFe,
            };
            nota.XmlAutorizado  = nfeProcAutorizado.ObterXmlString();
            nota.UrlQrCode      = qrCodeUrl;
            nota.MotivoRejeicao = null; // limpa motivo de tentativas anteriores que falharam antes desta autorização
            // F13: se esta nota foi Rejeitada antes (número anterior inutilizado
            // automaticamente) e agora autoriza com um número NOVO, os campos de
            // inutilização do número antigo não fazem mais sentido aqui — sem isso a nota
            // fica com estado contraditório ("Autorizada" mas mostrando "inutilizado em X").
            nota.InutilizadoEm         = null;
            nota.ProtocoloInutilizacao = null;
        }
        else
        {
            nota.Status         = NotaFiscalStatus.Rejeitada;
            nota.MotivoRejeicao = protInfo?.xMotivo ?? retorno.RetornoStr ?? "SEFAZ não retornou motivo.";

            // F5: rejeição de uma nota que estava em contingência (retransmissão alcançou a
            // SEFAZ, mas foi rejeitada por motivo de negócio) inutiliza o número atual — sem
            // limpar os campos de contingência, o PRÓXIMO reprocessamento veria jaEmContingencia
            // ainda true e tentaria reusar esse MESMO número já inutilizado, num loop que só
            // erra. Limpa aqui pra o próximo TransmitirAsync reservar um número novo do zero
            // (nota.Numero/Serie continuam documentando qual número foi inutilizado, só os
            // campos de reconstrução de chave de contingência são limpos).
            if (jaEmContingencia)
            {
                nota.CnfContingencia           = null;
                nota.DhContingencia            = null;
                nota.JustificativaContingencia = null;
            }
        }

        await _db.SaveChangesAsync();

        // Número já foi consumido acima independente do resultado — se rejeitada, esse
        // número nunca vai ser usado por nenhuma nota autorizada, então formaliza a
        // inutilização na hora pra não deixar buraco na numeração sem justificativa.
        if (nota.Status == NotaFiscalStatus.Rejeitada)
        {
            try
            {
                await InutilizarNumeroAsync(cfg, cfgServico, certificado, nota, numero);
            }
            catch (Exception ex)
            {
                // Inutilização é best-effort — não pode fazer a nota "sumir" do fluxo por causa disso.
                _logger.LogError(ex, "Falha ao inutilizar o número {Numero} da NFC-e rejeitada {NotaId}.", numero, nota.Id);
            }
        }
    }

    private async Task InutilizarNumeroAsync(
        FiscalConfig cfg, ConfiguracaoServico cfgServico, X509Certificate2 certificado, NotaFiscalEmitida nota, int numero)
    {
        using var servico = new ServicosNFe(cfgServico, certificado);
        var justificativa = $"Numero da NFCe {nota.Id} rejeitado pela SEFAZ, inutilizado automaticamente.";
        var retorno = servico.NfeInutilizacao(cfg.Cnpj, AgoraBrasil().Year, ModeloDocumento.NFCe, cfg.SerieNfce, numero, numero, justificativa);

        var infInut = retorno.Retorno?.infInut;
        if (infInut is not null && infInut.cStat == 102)
        {
            nota.InutilizadoEm            = DateTime.UtcNow;
            nota.ProtocoloInutilizacao    = infInut.nProt;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Número {Numero} inutilizado com sucesso para a NFC-e {NotaId}.", numero, nota.Id);
        }
        else
        {
            _logger.LogWarning("SEFAZ não confirmou a inutilização do número {Numero} (nota {NotaId}): {Motivo}",
                numero, nota.Id, infInut?.xMotivo ?? retorno.RetornoStr ?? "motivo desconhecido");
        }
    }

    /// <summary>
    /// Monta um ou dois detPag conforme haja segundo método de pagamento (split).
    /// O valor do primeiro método é o total menos o que foi pago no segundo, pra bater
    /// exatamente com vNF — evita a diferença de centavos ser "engolida" num só método.
    /// </summary>
    private static List<detPag> MontarDetPag(DadosEmissao dados, decimal valorTotal)
    {
        if (string.IsNullOrWhiteSpace(dados.SegundaFormaPagamento) || dados.SegundoValorCentavos <= 0)
            return new List<detPag> { new detPag { tPag = MapFormaPagamento(dados.FormaPagamento), vPag = valorTotal } };

        var valorSegundo  = dados.SegundoValorCentavos / 100m;
        var valorPrimeiro = valorTotal - valorSegundo;
        return new List<detPag>
        {
            new detPag { tPag = MapFormaPagamento(dados.FormaPagamento), vPag = valorPrimeiro },
            new detPag { tPag = MapFormaPagamento(dados.SegundaFormaPagamento), vPag = valorSegundo },
        };
    }

    private static det MontarItem(ItemFiscal item, int numero) => new()
    {
        nItem = numero,
        prod = new prod
        {
            cProd      = numero.ToString("D6"),
            cEAN       = "SEM GTIN",
            cEANTrib   = "SEM GTIN",
            xProd      = item.Nome,
            NCM        = item.Ncm,
            CFOP       = int.Parse(item.Cfop),
            uCom       = "UN",
            qCom       = item.Quantidade,
            vUnCom     = item.PrecoUnitarioCentavos / 100m,
            vProd      = item.SubtotalCentavos / 100m,
            uTrib      = "UN",
            qTrib      = item.Quantidade,
            vUnTrib    = item.PrecoUnitarioCentavos / 100m,
            indTot     = IndicadorTotal.ValorDoItemCompoeTotalNF,
        },
        imposto = new imposto
        {
            ICMS   = new ICMS   { TipoICMS   = MontarIcmsSimplesNacional(item) },
            // CST 99 "Outras Operações" é o padrão de fato usado por optantes do Simples
            // Nacional (o DAS já unifica PIS/COFINS — não há CST federal específico pra
            // esse regime na NFC-e). Confirmado contra prática de mercado, não é chute.
            PIS    = new PIS    { TipoPIS    = new PISOutr    { CST = CSTPIS.pis99,    vBC = 0, pPIS    = 0, vPIS    = 0 } },
            COFINS = new COFINS { TipoCOFINS = new COFINSOutr { CST = CSTCOFINS.cofins99, vBC = 0, pCOFINS = 0, vCOFINS = 0 } },
        },
    };

    /// <summary>
    /// Mapeia o CSOSN da Natureza de Operação pra classe ICMS correta do Simples Nacional.
    /// Cobre os 7 códigos que fazem sentido pra um lojista que NÃO é substituto tributário:
    /// 101 (com crédito), 102/103/300/400 (sem crédito — mesma estrutura de campos, só muda
    /// o código), 500 (ICMS-ST já retido antes) e 900 (outros). 201/202/203 são bloqueados de
    /// propósito: exigem MVA/base de cálculo de ICMS-ST que este sistema não calcula sozinho —
    /// inventar esses valores seria pior do que não emitir. Ajustar aqui só com o contador.
    /// </summary>
    internal static ICMSBasico MontarIcmsSimplesNacional(ItemFiscal item) => item.Csosn switch
    {
        "101" => new ICMSSN101
        {
            orig        = OrigemMercadoria.OmNacional,
            CSOSN       = Csosnicms.Csosn101,
            pCredSN     = item.PercentualCreditoSn ?? 0,
            vCredICMSSN = Math.Round(item.SubtotalCentavos / 100m * (item.PercentualCreditoSn ?? 0) / 100m, 2),
        },
        "102" or null or "" => new ICMSSN102 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn102 },
        "103"  => new ICMSSN102 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn103 },
        "300"  => new ICMSSN102 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn300 },
        "400"  => new ICMSSN102 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn400 },
        "500"  => new ICMSSN500 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn500 },
        "900"  => new ICMSSN900 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn900 },
        "201" or "202" or "203" => throw new FiscalNaoConfiguradoException(
            $"CSOSN {item.Csosn} exige cálculo de ICMS-ST (substituição tributária) como substituto — " +
            "MVA, base reduzida etc. Este sistema não inventa esses valores. Consulte o contador antes " +
            "de usar essa Natureza de Operação, ou troque para um CSOSN sem ICMS-ST (102, por exemplo)."),
        _ => throw new FiscalNaoConfiguradoException(
            $"CSOSN \"{item.Csosn}\" não é um código suportado do Simples Nacional. " +
            "Use 101, 102, 103, 300, 400, 500 ou 900 em Admin > Fiscal > Naturezas de Operação."),
    };

    private static CRT MapCrt(RegimeTributario regime) => regime switch
    {
        RegimeTributario.SimplesNacional => CRT.SimplesNacional,
        RegimeTributario.LucroPresumido  => CRT.RegimeNormal,
        RegimeTributario.LucroReal       => CRT.RegimeNormal,
        _                                => CRT.SimplesNacional,
    };

    /// <summary>
    /// Pontos/Cashback/Crediário não são formas de pagamento reconhecidas pela SEFAZ —
    /// são mecanismos internos da loja, então caem em "Outros" (99).
    /// </summary>
    private static FormaPagamento MapFormaPagamento(string formaPagamento) => formaPagamento switch
    {
        PaymentMethod.Dinheiro      => FormaPagamento.fpDinheiro,
        PaymentMethod.Pix           => FormaPagamento.fpPagamentoInstantaneoPIXDinamico,
        PaymentMethod.CartaoCredito => FormaPagamento.fpCartaoCredito,
        PaymentMethod.CartaoDebito  => FormaPagamento.fpCartaoDebito,
        _                           => FormaPagamento.fpOutro,
    };
}

/// <summary>Sinaliza que a emissão não pôde ocorrer porque o admin ainda não terminou
/// de configurar o módulo fiscal — não é uma falha de transmissão de verdade.</summary>
public class FiscalNaoConfiguradoException : Exception
{
    public FiscalNaoConfiguradoException(string message) : base(message) { }
}

/// <summary>Sinaliza que a comanda de origem foi cancelada antes da NFC-e ser
/// transmitida à SEFAZ — a nota deve ser anulada localmente, nunca emitida.</summary>
public class ComandaCanceladaException : Exception
{
    public Guid ComandaId { get; }
    public ComandaCanceladaException(Guid comandaId)
        : base($"Comanda {comandaId} foi cancelada — emissão fiscal abortada.") => ComandaId = comandaId;
}
