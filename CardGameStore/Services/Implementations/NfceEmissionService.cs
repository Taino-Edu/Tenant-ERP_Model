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
//  - Falha real de conectividade aciona contingência offline tpEmis=9 e conserva
//    número/cNF/chave/QR para retransmissão do mesmo documento em até 24 horas.
//  - Cancelamento e inutilização só concluem com os cStat esperados e persistem
//    protocolo/XML; a homologação real da SEFAZ continua obrigatória antes do go-live.
// =============================================================================

using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
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
using NFe.Classes.Informacoes.Observacoes;
using NFe.Classes.Informacoes.Pagamento;
using NFe.Classes.Informacoes.Total;
using NFe.Classes.Informacoes.Transporte;
using NFe.Classes.Servicos.Tipos;
using NFe.Servicos;
using NFe.Servicos.Retorno;
using NFe.Utils;
using NFe.Utils.Consulta;
using Npgsql;
using NFe.Utils.InformacoesSuplementares;
using NFe.Utils.NFe;
using CbsItem = NFe.Classes.Informacoes.Detalhe.Tributacao.Compartilhado.InformacoesIbsCbs.InformacoesCbs.gCBS;
using CbsTotal = NFe.Classes.Informacoes.Total.IbsCbs.Cbs.gCBSTotal;
using IbsCbsCst = NFe.Classes.Informacoes.Detalhe.Tributacao.Compartilhado.Tipos.CST;
using IbsCbsItem = NFe.Classes.Informacoes.Detalhe.Tributacao.Compartilhado.IBSCBS;
using IbsCbsItemValues = NFe.Classes.Informacoes.Detalhe.Tributacao.Compartilhado.InformacoesIbsCbs.gIBSCBS;
using IbsCbsTotal = NFe.Classes.Informacoes.Total.IbsCbs.IBSCBSTot;
using IbsItemMun = NFe.Classes.Informacoes.Detalhe.Tributacao.Compartilhado.InformacoesIbsCbs.InformacoesIbs.gIBSMun;
using IbsItemUf = NFe.Classes.Informacoes.Detalhe.Tributacao.Compartilhado.InformacoesIbsCbs.InformacoesIbs.gIBSUF;
using IbsTotal = NFe.Classes.Informacoes.Total.IbsCbs.Ibs.gIBS;
using IbsTotalMun = NFe.Classes.Informacoes.Total.IbsCbs.Ibs.gIBSMunTotal;
using IbsTotalUf = NFe.Classes.Informacoes.Total.IbsCbs.Ibs.gIBSUFTotal;
using NfeDocumento = NFe.Classes.NFe;

namespace CardGameStore.Services.Implementations;

public class NfceEmissionService : INfceEmissionService
{
    private const string DestinatarioHomologacao =
        "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";
    private const string ProdutoHomologacao =
        "NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";

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

    internal static ConfiguracaoCertificado CriarConfiguracaoCertificado(byte[] pfxBytes, string senha) => new()
    {
        TipoCertificado   = TipoCertificado.A1ByteArray,
        ArrayBytesArquivo = pfxBytes,
        Senha             = senha,
    };

    internal static ConfiguracaoServico CriarConfiguracaoServico(
        Estado estado, TipoAmbiente ambiente, ModeloDocumento modelo = ModeloDocumento.NFCe) => new()
    {
        cUF             = estado,
        tpAmb           = ambiente,
        ModeloDocumento = modelo,
        tpEmis           = TipoEmissao.teNormal,
        VersaoLayout    = VersaoServico.Versao400,
        TimeOut         = 15000,
        ValidarSchemas  = false,
    };

    internal static string MontarNfeProcXml(NfeDocumento nfe, NFe.Classes.Protocolo.protNFe protocolo)
    {
        var processo = new nfeProc
        {
            versao = "4.00",
            NFe = nfe,
            protNFe = protocolo,
        };
        return FuncoesXml.ClasseParaXmlString(processo);
    }

    /// <summary>Adaptador de fronteira para o identificador do estabelecimento.
    /// Toda dependência do formato exigido pela SEFAZ fica concentrada aqui.
    ///
    /// Aceita os dois modelos de CNPJ: o numérico de sempre e o alfanumérico que
    /// a Receita passou a emitir (IN RFB 2.229/2024), com o ambiente nacional de
    /// NF-e/NFC-e recebendo documentos nesse formato desde 01/07/2026 (NT
    /// 2026.004). Passou a conferir o dígito verificador também — antes qualquer
    /// sequência de 14 dígitos seguia pra SEFAZ e só voltava como rejeição.</summary>
    internal static string NormalizarCnpjParaSefaz(string? identificadorAtual)
    {
        var cnpj = Cnpj.Normalizar(identificadorAtual);
        if (!Cnpj.EhValido(cnpj))
            throw new FiscalNaoConfiguradoException(
                "O identificador fiscal do estabelecimento não é um CNPJ válido para a SEFAZ. " +
                "Informe as 14 posições do CNPJ da loja (numérico ou alfanumérico).");
        return cnpj;
    }

    internal static string? NormalizarCpfOpcionalParaSefaz(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return null;
        var digitos = new string(cpf.Where(char.IsDigit).ToArray());
        if (digitos.Length != 11)
            throw new FiscalNaoConfiguradoException(
                "O CPF do consumidor informado na venda deve conter 11 dígitos.");
        return digitos;
    }

    /// <summary>
    /// Distingue "SEFAZ inalcançável" (entra em contingência) de uma rejeição de negócio de
    /// verdade (SEFAZ respondeu, só não autorizou). Só os tipos de exceção claramente ligados
    /// a rede/timeout contam — qualquer outra coisa inesperada cai no catch genérico de fora
    /// (vira PendenteEmissao) em vez de declarar contingência por um motivo que pode ser bug.
    /// </summary>
    internal static bool EhFalhaDeCertificadoLocal(Exception ex) =>
        ex is AuthenticationException or CryptographicException
        || (ex.InnerException is not null && EhFalhaDeCertificadoLocal(ex.InnerException));

    internal static bool EhFalhaDeConectividade(Exception ex) =>
        !EhFalhaDeCertificadoLocal(ex) &&
        (ex is System.Net.Http.HttpRequestException
           or System.Net.WebException
           or System.Net.Sockets.SocketException
           or TimeoutException
           or TaskCanceledException
        || (ex.InnerException is not null && EhFalhaDeConectividade(ex.InnerException)));

    private readonly AppDbContext                _db;
    private readonly EncryptionService           _enc;
    private readonly ILogger<NfceEmissionService> _logger;
    private readonly IFiscalTaxEngine             _taxEngine;

    public NfceEmissionService(
        AppDbContext db, EncryptionService enc, ILogger<NfceEmissionService> logger)
        : this(db, enc, logger, new ConfigurableFiscalTaxEngine()) { }

    internal NfceEmissionService(
        AppDbContext db, EncryptionService enc, ILogger<NfceEmissionService> logger,
        IFiscalTaxEngine taxEngine)
    {
        _db     = db;
        _enc    = enc;
        _logger = logger;
        _taxEngine = taxEngine;
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

        if (nota.Status == NotaFiscalStatus.Rejeitada && nota.InutilizadoEm.HasValue)
        {
            _logger.LogWarning(
                "NFC-e {NotaId} rejeitada usa número já inutilizado e não pode ser retransmitida. " +
                "Corrija a configuração e gere uma nova venda/documento.", nota.Id);
            return nota;
        }

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

            nota.ValorTotalEmCentavos = dados.ValorLiquidoCentavos;
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
            justificativa: justificativa.Trim(), cpfcnpj: NormalizarCnpjParaSefaz(cfg.Cnpj), dhEvento: AgoraBrasil());

        var infEvento = retorno.Retorno?.retEvento?.FirstOrDefault()?.infEvento;
        if (infEvento is null || infEvento.cStat is not (135 or 136))
        {
            var motivo = infEvento?.xMotivo ?? retorno.RetornoStr ?? "SEFAZ não retornou motivo.";
            throw new InvalidOperationException($"SEFAZ rejeitou o cancelamento: {motivo}");
        }

        nota.Status                    = NotaFiscalStatus.Cancelada;
        nota.CanceladoEm                = DateTime.UtcNow;
        nota.JustificativaCancelamento  = justificativa.Trim();
        // Persiste a prova fiscal e sinaliza o estorno transacional do ERP.
        nota.ProtocoloCancelamento      = infEvento.nProt;
        nota.ErpEstornoErro             = "Estorno ERP aguardando processamento.";
        var procEvento = retorno.ProcEventosNFe?.FirstOrDefault();
        nota.XmlEventoCancelamento      = procEvento is not null
            ? FuncoesXml.ClasseParaXmlString(procEvento)
            : retorno.RetornoCompletoStr;
        await _db.SaveChangesAsync();

        _logger.LogInformation("NFC-e {NotaId} (chave {Chave}) cancelada com sucesso.", nota.Id, nota.ChaveAcesso);
        try
        {
            await EstornarOrigemNoErpAsync(nota.Id);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "NFC-e {NotaId} foi cancelada na SEFAZ, mas o estorno ERP ficou pendente.", nota.Id);
            await RegistrarFalhaEstornoAsync(nota.Id, ex.Message);
        }

        _db.ChangeTracker.Clear();
        return await _db.NotasFiscaisEmitidas.FindAsync(notaId) ?? nota;
    }

    /// <summary>
    /// Representação do DANFE NFC-e, montada SOMENTE a partir do XML fiscal
    /// persistido (DFE-001 do plano de go-live).
    ///
    /// A versão anterior remontava o cupom relendo a comanda e a FiscalConfig
    /// atuais. O resultado parecia certo na tela, mas corrigir o endereço da
    /// loja ou renomear um produto mudava a reimpressão de uma venda antiga — o
    /// papel passava a divergir do documento que a SEFAZ autorizou. Agora nada
    /// aqui consulta cadastro ou venda: o XML é a única fonte.
    ///
    /// Ordem de preferência da fonte: nfeProc autorizado &gt; XML assinado de
    /// contingência. Nota sem nenhum dos dois (pendente ou rejeitada) devolve
    /// null — documento sem autorização não pode ser apresentado como DANFE
    /// válido (DFE-007). A via de contingência é um DANFE legítimo, ainda sem
    /// protocolo: o próprio parser sinaliza esse estado.
    /// </summary>
    public async Task<DanfeFiscalDto?> ObterCupomAsync(Guid notaId)
    {
        var nota = await _db.NotasFiscaisEmitidas
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notaId);
        if (nota is null) return null;

        var xml = nota.XmlAutorizado ?? nota.XmlContingencia;
        if (string.IsNullOrWhiteSpace(xml))
        {
            _logger.LogInformation(
                "DANFE não gerado para a nota {NotaId}: status {Status} sem XML fiscal persistido.",
                notaId, nota.Status);
            return null;
        }

        try
        {
            var danfe = DanfeXmlParser.Parse(xml);

            // O cancelamento é um evento posterior, fora do XML de autorização —
            // é o único dado da representação que precisa vir do registro local.
            return nota.Status == NotaFiscalStatus.Cancelada
                ? danfe with { Situacao = DanfeSituacao.Cancelada }
                : danfe;
        }
        catch (DanfeXmlInvalidoException ex)
        {
            // XML corrompido em repouso é problema de guarda, não de venda: falha
            // de forma visível em vez de cair no cadastro e mascarar a perda.
            _logger.LogError(ex, "XML fiscal da nota {NotaId} não pôde ser lido para gerar o DANFE.", notaId);
            return null;
        }
    }

    // ── Orquestração ──────────────────────────────────────────────────────────

    private async Task<NotaFiscalEmitida> EmitirAsync(NotaFiscalOrigem origem, Guid? comandaId, Guid? vendaAvulsaId)
    {
        var existente = await BuscarNotaDaOrigemAsync(origem, comandaId, vendaAvulsaId);
        if (existente is not null) return existente;

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

            nota.ValorTotalEmCentavos = dados.ValorLiquidoCentavos;
            await TransmitirAsync(nota, dados);
        });

        return nota;
    }

    public async Task<InutilizacaoFiscal> InutilizarFaixaAsync(
        int ano, int serie, int numeroInicial, int numeroFinal, string justificativa)
    {
        justificativa = justificativa?.Trim() ?? string.Empty;
        var anoAtual = AgoraBrasil().Year;
        if (ano is < 2000 || ano > 9999 || ano < anoAtual - 1 || ano > anoAtual)
            throw new InvalidOperationException("O ano deve ser o atual ou o imediatamente anterior.");
        if (serie is < 1 or > 999)
            throw new InvalidOperationException("A série deve estar entre 1 e 999.");
        if (numeroInicial < 1 || numeroFinal < numeroInicial || numeroFinal - numeroInicial > 999)
            throw new InvalidOperationException("Informe uma faixa crescente de no máximo 1.000 números.");
        if (justificativa.Length is < 15 or > 255)
            throw new InvalidOperationException("A justificativa deve ter entre 15 e 255 caracteres.");

        var existente = await _db.InutilizacoesFiscais.FirstOrDefaultAsync(i =>
            i.Ano == ano && i.Serie == serie &&
            i.NumeroInicial == numeroInicial && i.NumeroFinal == numeroFinal);
        if (existente is not null) return existente;

        var inicioAnoUtc = BrazilTime.ToUtcFromLocal(new DateTime(ano, 1, 1));
        var fimAnoUtc = BrazilTime.ToUtcFromLocal(new DateTime(ano + 1, 1, 1));

        var conflitaComDocumentoValido = await _db.NotasFiscaisEmitidas.AnyAsync(n =>
            n.Serie == serie && n.Numero >= numeroInicial && n.Numero <= numeroFinal &&
            ((n.EmitidoEm.HasValue && n.EmitidoEm.Value >= inicioAnoUtc && n.EmitidoEm.Value < fimAnoUtc) ||
             (!n.EmitidoEm.HasValue && n.CreatedAt >= inicioAnoUtc && n.CreatedAt < fimAnoUtc)) &&
            (n.Status == NotaFiscalStatus.Autorizada ||
             n.Status == NotaFiscalStatus.AutorizadaContingencia ||
             n.Status == NotaFiscalStatus.Cancelada));
        if (conflitaComDocumentoValido)
            throw new InvalidOperationException(
                "A faixa contém NFC-e autorizada, cancelada ou em contingência e não pode ser inutilizada.");

        var (cfg, cfgServico, certificado, _, _, _) = await AbrirConfiguracaoSefazAsync();
        using var _certDispose = certificado;
        using var servico = new ServicosNFe(cfgServico, certificado);
        var retorno = servico.NfeInutilizacao(
            NormalizarCnpjParaSefaz(cfg.Cnpj), ano, ModeloDocumento.NFCe,
            serie, numeroInicial, numeroFinal, justificativa);
        var infInut = retorno.Retorno?.infInut;
        if (infInut is null || infInut.cStat != 102)
            throw new InvalidOperationException(
                $"SEFAZ rejeitou a inutilização: {infInut?.xMotivo ?? retorno.RetornoStr ?? "motivo não informado"}");

        var registro = new InutilizacaoFiscal
        {
            Ano = ano,
            Serie = serie,
            NumeroInicial = numeroInicial,
            NumeroFinal = numeroFinal,
            Justificativa = justificativa,
            Protocolo = infInut.nProt ?? string.Empty,
            XmlRetorno = retorno.RetornoCompletoStr,
            InutilizadoEm = DateTime.UtcNow,
        };
        _db.InutilizacoesFiscais.Add(registro);

        var notasAbandonadas = await _db.NotasFiscaisEmitidas
            .Where(n => n.Serie == serie && n.Numero >= numeroInicial && n.Numero <= numeroFinal &&
                        ((n.EmitidoEm.HasValue && n.EmitidoEm.Value >= inicioAnoUtc && n.EmitidoEm.Value < fimAnoUtc) ||
                         (!n.EmitidoEm.HasValue && n.CreatedAt >= inicioAnoUtc && n.CreatedAt < fimAnoUtc)) &&
                        n.Status != NotaFiscalStatus.Autorizada &&
                        n.Status != NotaFiscalStatus.AutorizadaContingencia &&
                        n.Status != NotaFiscalStatus.Cancelada)
            .ToListAsync();
        foreach (var nota in notasAbandonadas)
        {
            nota.Status = NotaFiscalStatus.Rejeitada;
            nota.InutilizadoEm = registro.InutilizadoEm;
            nota.ProtocoloInutilizacao = registro.Protocolo;
            nota.MotivoRejeicao ??= "Numeração abandonada e inutilizada explicitamente na SEFAZ.";
        }

        await _db.SaveChangesAsync();
        _logger.LogWarning(
            "Faixa NFC-e {Serie}/{Inicio}-{Fim} de {Ano} inutilizada na SEFAZ. Protocolo {Protocolo}.",
            serie, numeroInicial, numeroFinal, ano, registro.Protocolo);
        return registro;
    }

    public async Task<NotaFiscalEmitida> ReprocessarEstornoErpAsync(Guid notaId)
    {
        var nota = await _db.NotasFiscaisEmitidas.FindAsync(notaId)
            ?? throw new InvalidOperationException("Nota fiscal não encontrada.");
        if (nota.Status != NotaFiscalStatus.Cancelada)
            throw new InvalidOperationException("O estorno ERP só se aplica a nota já cancelada na SEFAZ.");
        if (!nota.ErpEstornadoEm.HasValue)
        {
            try { await EstornarOrigemNoErpAsync(notaId); }
            catch (Exception ex) { await RegistrarFalhaEstornoAsync(notaId, ex.Message); }
        }
        _db.ChangeTracker.Clear();
        return await _db.NotasFiscaisEmitidas.FindAsync(notaId) ?? nota;
    }

    internal async Task EstornarOrigemNoErpAsync(Guid notaId)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync();
            var nota = await _db.NotasFiscaisEmitidas.FindAsync(notaId)
                ?? throw new InvalidOperationException("Nota cancelada não encontrada para estorno ERP.");
            if (nota.ErpEstornadoEm.HasValue)
            {
                await transaction.CommitAsync();
                return;
            }

            string formaPagamento;
            string? segundaForma;
            if (nota.Origem == NotaFiscalOrigem.Comanda)
            {
                var comanda = await _db.Comandas
                    .Include(c => c.Items)
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == nota.ComandaId)
                    ?? throw new InvalidOperationException("Comanda da nota cancelada não foi encontrada.");
                if (!comanda.FiscalEffectsCapturedAt.HasValue)
                    throw new InvalidOperationException(
                        "A venda é anterior ao rastreamento de efeitos fiscais e exige estorno manual.");

                foreach (var item in comanda.Items)
                    await RestaurarEstoqueAsync(item.ProductId, item.VariantId, item.Quantity);
                ReverterSaldos(comanda.User, comanda.PointsDebitedAtSale,
                    comanda.PointsAwardedAtSale, comanda.CashbackDebitedAtSale);
                await ReverterCrediarioAsync(comanda.CrediarioIdAtSale,
                    comanda.CrediarioAmountAtSale, MontarItensCrediario(comanda.Items));
                comanda.Status = ComandaStatus.Cancelada;
                formaPagamento = comanda.PaymentMethod ?? PaymentMethod.Dinheiro;
                segundaForma = comanda.SecondPaymentMethod;
            }
            else
            {
                var venda = await _db.VendasAvulsas.FirstOrDefaultAsync(v => v.Id == nota.VendaAvulsaId)
                    ?? throw new InvalidOperationException("Venda avulsa da nota cancelada não foi encontrada.");
                if (!venda.FiscalEffectsCapturedAt.HasValue)
                    throw new InvalidOperationException(
                        "A venda é anterior ao rastreamento de efeitos fiscais e exige estorno manual.");

                foreach (var item in venda.Items)
                    await RestaurarEstoqueAsync(item.ProductId, item.VariantId, item.Quantity);
                var user = venda.UserId.HasValue ? await _db.Users.FindAsync(venda.UserId.Value) : null;
                ReverterSaldos(user, venda.PointsDebitedAtSale,
                    venda.PointsAwardedAtSale, venda.CashbackDebitedAtSale);
                await ReverterCrediarioAsync(venda.CrediarioIdAtSale,
                    venda.CrediarioAmountAtSale, MontarItensCrediario(venda.Items));
                venda.CanceladoEm = DateTime.UtcNow;
                formaPagamento = venda.PaymentMethod;
                segundaForma = venda.SecondPaymentMethod;
            }

            nota.ErpEstornadoEm = DateTime.UtcNow;
            nota.ErpEstornoErro = null;
            await CriarAlertaReembolsoExternoAsync(nota, formaPagamento, segundaForma);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }

    private async Task RestaurarEstoqueAsync(Guid? productId, Guid? variantId, int quantidade)
    {
        if (variantId.HasValue)
        {
            var atualizadas = await _db.ProductVariants.Where(v => v.Id == variantId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity + quantidade));
            if (atualizadas == 0) throw new InvalidOperationException($"Variante {variantId} não encontrada para devolver estoque.");
        }
        else if (productId.HasValue)
        {
            var atualizados = await _db.Products.Where(p => p.Id == productId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantidade));
            if (atualizados == 0) throw new InvalidOperationException($"Produto {productId} não encontrado para devolver estoque.");
        }
    }

    private static void ReverterSaldos(User? user, int pontosDebitados, int pontosConcedidos, int cashbackDebitado)
    {
        if (user is null && (pontosDebitados != 0 || pontosConcedidos != 0 || cashbackDebitado != 0))
            throw new InvalidOperationException("Cliente não encontrado para reverter pontos/cashback.");
        if (user is null) return;

        user.PointsBalance += pontosDebitados - pontosConcedidos;
        user.BalanceInCents += cashbackDebitado;
        user.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ReverterCrediarioAsync(Guid? crediarioId, int valor, List<ItemCrediarioDto> itensDaVenda)
    {
        if (!crediarioId.HasValue || valor <= 0) return;
        var crediario = await _db.Crediarios.FindAsync(crediarioId.Value)
            ?? throw new InvalidOperationException("Crediário da venda não encontrado para estorno.");
        var novoTotal = crediario.ValorEmCentavos - valor;
        if (novoTotal < 0 || crediario.ValorPagoEmCentavos > novoTotal)
            throw new InvalidOperationException(
                "O crediário já possui pagamento que impede estorno automático; ajuste manual obrigatório.");

        crediario.ValorEmCentavos = novoTotal;
        RemoverItensCrediario(crediario, itensDaVenda);
        if (novoTotal == 0 && crediario.ValorPagoEmCentavos == 0)
            _db.Crediarios.Remove(crediario);
    }

    private static List<ItemCrediarioDto> MontarItensCrediario(IEnumerable<ComandaItem> itens) => itens.Select(i => new ItemCrediarioDto
    {
        ItemName = i.ItemNameSnapshot, Quantity = i.Quantity,
        UnitPriceInReais = i.UnitPriceInCents / 100m, SubtotalInReais = i.SubtotalInCents / 100m,
    }).ToList();

    private static List<ItemCrediarioDto> MontarItensCrediario(IEnumerable<VendaAvulsaItem> itens) => itens.Select(i => new ItemCrediarioDto
    {
        ItemName = i.ProductName, Quantity = i.Quantity,
        UnitPriceInReais = i.UnitPriceInCents / 100m, SubtotalInReais = i.SubtotalInCents / 100m,
    }).ToList();

    private static void RemoverItensCrediario(Crediario crediario, IEnumerable<ItemCrediarioDto> itensDaVenda)
    {
        if (string.IsNullOrWhiteSpace(crediario.ItensJson)) return;
        var atuais = JsonSerializer.Deserialize<List<ItemCrediarioDto>>(crediario.ItensJson) ?? [];
        foreach (var item in itensDaVenda)
        {
            var indice = atuais.FindLastIndex(i => i.ItemName == item.ItemName && i.Quantity == item.Quantity &&
                i.UnitPriceInReais == item.UnitPriceInReais && i.SubtotalInReais == item.SubtotalInReais);
            if (indice >= 0) atuais.RemoveAt(indice);
        }
        crediario.ItensJson = atuais.Count == 0 ? null : JsonSerializer.Serialize(atuais);
    }

    private async Task CriarAlertaReembolsoExternoAsync(NotaFiscalEmitida nota, string principal, string? segunda)
    {
        static bool Externo(string? forma) => forma is PaymentMethod.Dinheiro or PaymentMethod.Pix
            or PaymentMethod.CartaoCredito or PaymentMethod.CartaoDebito;
        if (!Externo(principal) && !Externo(segunda)) return;

        var admins = await _db.Users.Where(u => u.Role == UserRole.Admin && u.IsActive).Select(u => u.Id).ToListAsync();
        foreach (var adminId in admins)
            _db.Notifications.Add(new Notification
            {
                UserId = adminId,
                Title = "Reembolso de venda cancelada",
                Body = $"A NFC-e nº {nota.Numero} foi cancelada e o ERP estornado. Confirme o reembolso externo ({principal}{(segunda is null ? "" : " + " + segunda)}).",
                Link = "/admin/fiscal",
            });
    }

    private async Task RegistrarFalhaEstornoAsync(Guid notaId, string erro)
    {
        _db.ChangeTracker.Clear();
        var nota = await _db.NotasFiscaisEmitidas.FindAsync(notaId);
        if (nota is null) return;
        var deveNotificar = !string.Equals(nota.ErpEstornoErro, erro, StringComparison.Ordinal);
        nota.ErpEstornoErro = erro;
        if (deveNotificar)
        {
            var admins = await _db.Users.Where(u => u.Role == UserRole.Admin && u.IsActive).Select(u => u.Id).ToListAsync();
            foreach (var adminId in admins)
                _db.Notifications.Add(new Notification
                {
                    UserId = adminId,
                    Title = "Estorno ERP fiscal pendente",
                    Body = $"A NFC-e nº {nota.Numero} foi cancelada na SEFAZ, mas o ERP não foi estornado: {erro}",
                    Link = "/admin/fiscal",
                });
        }
        await _db.SaveChangesAsync();
    }

    private Task<NotaFiscalEmitida?> BuscarNotaDaOrigemAsync(
        NotaFiscalOrigem origem, Guid? comandaId, Guid? vendaAvulsaId) =>
        origem == NotaFiscalOrigem.Comanda
            ? _db.NotasFiscaisEmitidas.FirstOrDefaultAsync(n => n.ComandaId == comandaId)
            : _db.NotasFiscaisEmitidas.FirstOrDefaultAsync(n => n.VendaAvulsaId == vendaAvulsaId);

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
            // Deixa a causa visível ao admin; sem isso a UI só mostrava
            // "PendenteEmissao" e o diagnóstico ficava preso no log da VPS.
            var mensagem = ex.GetBaseException().Message;
            nota.MotivoRejeicao = $"Falha na emissão: {mensagem[..Math.Min(mensagem.Length, 900)]}";
            await _db.SaveChangesAsync();

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
        int Quantidade, int PrecoUnitarioCentavos, int SubtotalCentavos,
        int OrigemMercadoria = 0, int? ModalidadeBcSt = null,
        decimal? PercentualMvaSt = null, decimal? PercentualReducaoBcSt = null,
        decimal? AliquotaIcmsSt = null, decimal? AliquotaIcmsProprio = null,
        decimal? AliquotaFcpSt = null, int? BaseStFixaEmCentavos = null,
        string IbsCbsCst = "000", string IbsCbsClassTrib = "000001",
        string? Cest = null,
        decimal? PercentualTributosFederais = null,
        decimal? PercentualTributosEstaduais = null,
        decimal? PercentualTributosMunicipais = null,
        string? FonteTributos = null,
        bool TributosPreenchidosAutomaticamente = false,
        DateTime? TributosVigenciaFim = null,
        // ── Regime normal (Lucro Presumido/Real) ─────────────────────────────
        string? Cst = null,
        decimal? PercentualReducaoBc = null,
        decimal? AliquotaFcp = null,
        int? BaseStRetidaEmCentavos = null,
        int? ValorStRetidoEmCentavos = null,
        string? CstPis = null, string? CstCofins = null,
        decimal? AliquotaPis = null, decimal? AliquotaCofins = null,
        // ── Identificação do produto no XML (XML-001) ────────────────────────
        // ProdutoId dá ao cProd uma identidade estável (cruza com estoque e
        // escrituração); Gtin é o código de barras do cadastro, usado no cEAN
        // só quando é um GTIN válido.
        Guid? ProdutoId = null,
        string? Gtin = null);

    private record DadosEmissao(
        List<ItemFiscal> Itens, string FormaPagamento, string? ClienteCpf,
        string? SegundaFormaPagamento, int SegundoValorCentavos, int DescontoTotalCentavos)
    {
        public int ValorBrutoCentavos => Itens.Sum(i => i.SubtotalCentavos);
        public int ValorLiquidoCentavos => Math.Max(0, ValorBrutoCentavos - DescontoTotalCentavos);
    }

    private async Task<DadosEmissao> CarregarDadosComandaAsync(Guid comandaId, bool permitirCancelada = false)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.NaturezaOperacao)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException($"Comanda {comandaId} não encontrada para emissão fiscal.");

        if (comanda.Status == ComandaStatus.Cancelada && !permitirCancelada)
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

        var itens = comanda.Items.Select(item =>
        {
            var regra = item.Product?.NaturezaOperacao ?? padrao;
            return CriarItemFiscal(
                item.ItemNameSnapshot, item.Product!, item.Quantity,
                item.UnitPriceInCents, item.SubtotalInCents, regra);
        }).ToList();

        var valorBruto = itens.Sum(i => i.SubtotalCentavos);
        var descontoTotal = Math.Clamp(valorBruto - comanda.TotalInCents, 0, valorBruto);

        return new DadosEmissao(
            itens, comanda.PaymentMethod ?? PaymentMethod.Dinheiro, comanda.User?.Cpf,
            comanda.SecondPaymentMethod, comanda.SecondPaymentAmountInCents, descontoTotal);
    }

    private async Task<DadosEmissao> CarregarDadosVendaAvulsaAsync(Guid vendaAvulsaId, bool permitirCancelada = false)
    {
        var venda = await _db.VendasAvulsas.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vendaAvulsaId)
            ?? throw new InvalidOperationException($"Venda avulsa {vendaAvulsaId} não encontrada para emissão fiscal.");
        if (venda.CanceladoEm.HasValue && !permitirCancelada)
            throw new InvalidOperationException("A venda avulsa foi cancelada e não pode gerar uma nova NFC-e.");

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
            return CriarItemFiscal(
                item.ProductName, product!, item.Quantity,
                item.UnitPriceInCents, item.SubtotalInCents,
                product?.NaturezaOperacao ?? padrao);
        }).ToList();

        string? cpf = null;
        if (venda.UserId.HasValue)
            cpf = (await _db.Users.FindAsync(venda.UserId.Value))?.Cpf;

        return new DadosEmissao(
            itens, venda.PaymentMethod, cpf,
            venda.SecondPaymentMethod, venda.SecondPaymentAmountInCents,
            Math.Clamp(venda.DiscountInCents, 0, itens.Sum(i => i.SubtotalCentavos)));
    }

    private static ItemFiscal CriarItemFiscal(
        string nome, Product product, int quantidade, int precoUnitarioCentavos,
        int subtotalCentavos, NaturezaOperacao? regra) => new(
        Nome: nome,
        Ncm: product.Ncm!,
        Cfop: regra?.Cfop ?? "5102",
        Csosn: regra?.Csosn ?? "102",
        PercentualCreditoSn: regra?.PercentualCreditoIcmsSn,
        Quantidade: quantidade,
        PrecoUnitarioCentavos: precoUnitarioCentavos,
        SubtotalCentavos: subtotalCentavos,
        OrigemMercadoria: regra?.OrigemMercadoria ?? 0,
        ModalidadeBcSt: regra?.ModalidadeBcSt,
        PercentualMvaSt: regra?.PercentualMvaSt,
        PercentualReducaoBcSt: regra?.PercentualReducaoBcSt,
        AliquotaIcmsSt: regra?.AliquotaIcmsSt,
        AliquotaIcmsProprio: regra?.AliquotaIcmsProprio,
        AliquotaFcpSt: regra?.AliquotaFcpSt,
        BaseStFixaEmCentavos: regra?.BaseStFixaEmCentavos,
        IbsCbsCst: regra?.IbsCbsCst ?? "000",
        IbsCbsClassTrib: regra?.IbsCbsClassTrib ?? "000001",
        Cest: product.Cest,
        PercentualTributosFederais: product.PercentualTributosFederais,
        PercentualTributosEstaduais: product.PercentualTributosEstaduais,
        PercentualTributosMunicipais: product.PercentualTributosMunicipais,
        FonteTributos: product.FonteTributos,
        TributosPreenchidosAutomaticamente: product.TributosPreenchidosAutomaticamente,
        TributosVigenciaFim: product.TributosVigenciaFim,
        Cst: regra?.Cst,
        PercentualReducaoBc: regra?.PercentualReducaoBc,
        AliquotaFcp: regra?.AliquotaFcp,
        BaseStRetidaEmCentavos: regra?.BaseStRetidaEmCentavos,
        ValorStRetidoEmCentavos: regra?.ValorStRetidoEmCentavos,
        CstPis: regra?.CstPis,
        CstCofins: regra?.CstCofins,
        AliquotaPis: regra?.AliquotaPis,
        AliquotaCofins: regra?.AliquotaCofins,
        ProdutoId: product.Id,
        Gtin: product.Barcode);

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

        _ = NormalizarCnpjParaSefaz(cfg.Cnpj);
        if (string.IsNullOrWhiteSpace(cfg.InscricaoEstadual))
            throw new FiscalNaoConfiguradoException("Inscrição Estadual não configurada em Admin > Fiscal.");
        if (string.IsNullOrWhiteSpace(cfg.CscId) || string.IsNullOrWhiteSpace(cfg.CscTokenEncrypted))
            throw new FiscalNaoConfiguradoException("CSC (identificador e token) não configurado em Admin > Fiscal.");
        if (cfg.SerieNfce is < 1 or > 999 || cfg.ProximoNumeroNfce < 1)
            throw new FiscalNaoConfiguradoException("Série ou próximo número da NFC-e inválido.");
        // Os três regimes montam documento completo: itens por CSOSN (Simples) ou
        // por CST (Presumido/Real), com PIS/COFINS do regime, e os totalizadores
        // consolidam esses grupos a partir dos próprios itens (REG-001). A
        // coerência CRT × código de tributação é validada por item em
        // MontarIcms*, antes de reservar número.
        //
        // Isto NÃO é aprovação fiscal: o XML completo fora do Simples ainda
        // precisa passar por XSD, homologação na SEFAZ por CST e aceite do
        // contador antes de um tenant real emitir (REG-001 na seção 10 do plano).
        _ = SanitizarCep(cfg.Cep);
        if (new string(cfg.CodigoMunicipioIbge.Where(char.IsDigit).ToArray()).Length != 7)
            throw new FiscalNaoConfiguradoException("Código IBGE do município deve ter 7 dígitos.");

        var pfxBytes    = Convert.FromBase64String(_enc.Decrypt(cfg.CertificadoPfxEncrypted!));
        var senha       = _enc.Decrypt(cfg.CertificadoSenhaEncrypted!);
        var certificado = Pkcs12Loader.Abrir(pfxBytes, senha);
        var agora = DateTime.UtcNow;
        if (certificado.NotBefore.ToUniversalTime() > agora)
        {
            certificado.Dispose();
            throw new FiscalNaoConfiguradoException("O certificado A1 ainda não está dentro do período de validade.");
        }
        var certificadoValidoAte = certificado.NotAfter;
        if (certificadoValidoAte.ToUniversalTime() <= agora)
        {
            certificado.Dispose();
            throw new FiscalNaoConfiguradoException(
                $"O certificado A1 venceu em {certificadoValidoAte:dd/MM/yyyy}. Atualize-o em Admin > Fiscal.");
        }
        var cfgCertificado = CriarConfiguracaoCertificado(pfxBytes, senha);

        if (!Enum.TryParse<Estado>(cfg.Uf, ignoreCase: true, out var estado))
        {
            certificado.Dispose();
            throw new FiscalNaoConfiguradoException($"UF do emitente inválida: \"{cfg.Uf}\".");
        }
        var ambiente = cfg.Ambiente == AmbienteFiscal.Producao ? TipoAmbiente.Producao : TipoAmbiente.Homologacao;

        // Sem XSDs locais empacotados: a SEFAZ valida o schema no recebimento.
        var cfgServico = CriarConfiguracaoServico(estado, ambiente);

        return (cfg, cfgServico, certificado, cfgCertificado, estado, ambiente);
    }

    /// <summary>
    /// Reserva atomicamente o próximo número de NFC-e via UPDATE...RETURNING no Postgres —
    /// evita que dois fechamentos de comanda simultâneos peguem o mesmo número (a leitura +
    /// incremento em memória do EF não é segura contra concorrência entre requisições).
    /// </summary>
    private async Task<int> ReservarProximoNumeroNfceAsync(Guid fiscalConfigId)
    {
        // Usa a pipeline do EF para o interceptor de tenant aplicar o search_path.
        // DbConnection aberta manualmente atualizava o schema public, não a loja.
        var resultados = await _db.Database.SqlQueryRaw<int>(
            "UPDATE fiscal_config SET proximo_numero_nfce = proximo_numero_nfce + 1, updated_at = now() " +
            "WHERE id = {0} RETURNING proximo_numero_nfce - 1 AS \"Value\"",
            fiscalConfigId)
            // UPDATE ... RETURNING não é SQL componível. ToListAsync executa o
            // comando exatamente como está; SingleOrDefaultAsync tentaria envolvê-lo
            // em SELECT e o Postgres rejeita essa composição.
            .ToListAsync();

        if (resultados.Count != 1)
            throw new InvalidOperationException("Não foi possível reservar o número da NFC-e — FiscalConfig não encontrado.");
        return resultados[0];
    }

    private async Task TransmitirAsync(NotaFiscalEmitida nota, DadosEmissao dados)
    {
        // Retransmissão de contingência (RES-002): se já existe o XML assinado
        // offline, reenvia EXATAMENTE aquele documento — não remonta a partir da
        // comanda. A remontagem releria os dados atuais, e uma edição na comanda
        // entre a venda offline e a retransmissão produziria um documento
        // diferente com a MESMA chave: divergente do que o consumidor já levou.
        if (nota.DhContingencia.HasValue && !string.IsNullOrWhiteSpace(nota.XmlContingencia))
        {
            await RetransmitirContingenciaAsync(nota);
            return;
        }

        var (cfg, cfgServico, certificado, cfgCertificado, estado, ambiente) = await AbrirConfiguracaoSefazAsync();
        using var _certDispose = certificado;

        // M14: decriptado uma vez aqui — cfg.CscTokenEncrypted nunca é usado direto no QR Code.
        var cscToken = string.IsNullOrWhiteSpace(cfg.CscTokenEncrypted) ? null : _enc.Decrypt(cfg.CscTokenEncrypted);

        // Monta os itens (e valida CSOSN) ANTES de reservar o número — uma Natureza de
        // Operação mal configurada não pode queimar um número de NFC-e sem transmitir nada.
        var jaEmContingencia = nota.DhContingencia.HasValue;
        var anoIbsCbs = jaEmContingencia
            ? ParaBrasil(nota.EmitidoEm ?? nota.DhContingencia ?? nota.CreatedAt).Year
            : AgoraBrasil().Year;
        var incluirIbsCbs = ambiente == TipoAmbiente.Homologacao || anoIbsCbs >= 2027;
        if (incluirIbsCbs && anoIbsCbs != 2026)
            throw new FiscalNaoConfiguradoException(
                $"As alíquotas de IBS/CBS para {anoIbsCbs} ainda não estão configuradas. " +
                "Atualize o motor fiscal conforme a tabela oficial vigente antes de emitir.");

        var descontosPorItem = DistribuirDesconto(dados.Itens, dados.DescontoTotalCentavos);
        var detItens = dados.Itens
            .Select((item, idx) => _taxEngine.MontarItem(
                item, idx + 1, descontosPorItem[idx], incluirIbsCbs, cfg.RegimeTributario))
            .ToList();
        if (ambiente == TipoAmbiente.Homologacao && detItens.Count > 0)
            detItens[0].prod.xProd = ProdutoHomologacao;
        var totaisIcms = _taxEngine.SomarTotaisIcms(detItens);
        var tributosPorItem = dados.Itens
            .Select((item, indice) => CalcularTributosAproximados(item, descontosPorItem[indice]))
            .ToList();
        var tributosFederais = tributosPorItem.Sum(t => t.Federal);
        var tributosEstaduais = tributosPorItem.Sum(t => t.Estadual);
        var tributosMunicipais = tributosPorItem.Sum(t => t.Municipal);
        var tributosTotais = tributosFederais + tributosEstaduais + tributosMunicipais;
        var fontesTributos = string.Join(", ", tributosPorItem.Select(t => t.Fonte).Distinct(StringComparer.OrdinalIgnoreCase));
        if (fontesTributos.Length > 500)
            throw new FiscalNaoConfiguradoException(
                "As fontes de transparencia tributaria da venda ultrapassam 500 caracteres. " +
                "Padronize a mesma fonte/versao nos produtos antes de emitir.");

        nota.TributosFederaisEmCentavos = DecimalParaCentavos(tributosFederais);
        nota.TributosEstaduaisEmCentavos = DecimalParaCentavos(tributosEstaduais);
        nota.TributosMunicipaisEmCentavos = DecimalParaCentavos(tributosMunicipais);
        nota.FontesTributos = fontesTributos;
        nota.TributosItensJson = JsonSerializer.Serialize(
            tributosPorItem.Select(t => DecimalParaCentavos(t.Total)).ToList());

        // Mantém número e cNF imutáveis também após rejeição corrigível. O nome da
        // coluna CnfContingencia é preservado por compatibilidade; DhContingencia é
        // quem distingue uma emissão offline (tpEmis=9) de uma emissão normal.
        var numero = nota.Numero ?? await ReservarProximoNumeroNfceAsync(cfg.Id);
        if (!nota.Numero.HasValue)
        {
            // Persiste imediatamente o número reservado. Se o processo cair antes da
            // resposta da SEFAZ, a lacuna continua rastreável e pode ser inutilizada.
            nota.Serie = cfg.SerieNfce;
            nota.Numero = numero;
            await _db.SaveChangesAsync();
        }
        // Tentativa normal precisa de horário atual para não ser rejeitada por emissão
        // atrasada. A contingência preserva o instante do documento entregue ao cliente.
        var dhEmi = jaEmContingencia
            ? ParaBrasil(nota.EmitidoEm ?? nota.DhContingencia ?? nota.CreatedAt)
            : AgoraBrasil();
        var cNf = nota.CnfContingencia ?? Random.Shared.Next(10_000_000, 99_999_999);
        if (!nota.CnfContingencia.HasValue)
        {
            nota.CnfContingencia = cNf;
            await _db.SaveChangesAsync();
        }
        var tpEmis = jaEmContingencia ? TipoEmissao.teOffLine : TipoEmissao.teNormal;
        cfgServico.tpEmis = tpEmis;
        var cnpj   = NormalizarCnpjParaSefaz(cfg.Cnpj);
        var chave  = ChaveFiscal.ObterChave(estado, dhEmi, cnpj, ModeloDocumento.NFCe, cfg.SerieNfce, numero, (int)tpEmis, cNf);

        var municipioIbge = long.Parse(cfg.CodigoMunicipioIbge!);
        var cpfDestinatario = NormalizarCpfOpcionalParaSefaz(dados.ClienteCpf);
        var valorBruto     = detItens.Sum(i => i.prod.vProd);
        var valorDesconto  = dados.DescontoTotalCentavos / 100m;
        var valorTotal     = dados.ValorLiquidoCentavos / 100m;
        var cep            = SanitizarCep(cfg.Cep);

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
                    CNPJ  = cnpj,
                    xNome = cfg.RazaoSocial,
                    IE    = string.IsNullOrWhiteSpace(cfg.InscricaoEstadual)
                        ? null
                        : new string(cfg.InscricaoEstadual.Where(char.IsDigit).ToArray()),
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
                        CEP     = cep,
                    },
                },
                dest = cpfDestinatario is null ? null : new dest(VersaoServico.Versao400)
                {
                    CPF       = cpfDestinatario,
                    xNome     = ambiente == TipoAmbiente.Homologacao ? DestinatarioHomologacao : null,
                    indIEDest = indIEDest.NaoContribuinte,
                },
                det = detItens,
                total = new total
                {
                    // Todos os valores vêm da soma dos itens (REG-001). No Simples
                    // a maioria continua zero porque o CSOSN não destaca ICMS
                    // próprio — a diferença é que agora isso é resultado do
                    // cálculo, não um zero fixo que mentia fora do Simples.
                    ICMSTot = new ICMSTot
                    {
                        vBC = totaisIcms.BaseIcms, vICMS = totaisIcms.ValorIcms,
                        vICMSDeson = totaisIcms.ValorDeson, vFCP = totaisIcms.ValorFcp,
                        vBCST = totaisIcms.BaseSt, vST = totaisIcms.ValorSt,
                        vFCPST = totaisIcms.ValorFcpSt, vFCPSTRet = 0,
                        vProd    = valorBruto,
                        vFrete   = 0, vSeg = 0, vDesc = valorDesconto, vII = 0, vIPI = 0,
                        vIPIDevol = 0,
                        vPIS     = totaisIcms.ValorPis, vCOFINS = totaisIcms.ValorCofins, vOutro = 0,
                        vTotTrib = tributosTotais,
                        vNF      = valorTotal,
                    },
                    IBSCBSTot = incluirIbsCbs ? _taxEngine.MontarTotaisIbsCbs2026(detItens) : null,
                },
                transp = new transp { modFrete = ModalidadeFrete.mfSemFrete },
                pag = new List<pag> { new pag { detPag = MontarDetPag(dados, valorTotal) } },
                infAdic = new infAdic
                {
                    infCpl = MontarTextoTransparenciaTributaria(
                        tributosFederais, tributosEstaduais, tributosMunicipais, fontesTributos),
                },
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

        // QR Code v3, implantado nacionalmente pela NT 2025.001.
        nfe.infNFeSupl = new infNFeSupl();
        var qrCodeUrl = ExtinfNFeSupl.ObterUrlQrCode(
            nfe.infNFeSupl, nfe, VersaoQrCode.QrCodeVersao3, cfg.CscId!, cscToken, cfgCertificado);
        nfe.infNFeSupl.qrCode = qrCodeUrl;
        nfe.infNFeSupl.urlChave = ExtinfNFeSupl.ObterUrlConsulta(
            nfe.infNFeSupl, nfe, VersaoQrCode.QrCodeVersao3);

        // O documento offline precisa estar completamente montado antes de ser entregue
        // ao consumidor. Persistimos a chave, o QR e o XML ASSINADO de tpEmis=9 antes de
        // tentar retransmitir (RES-002): é o documento que o cliente leva, e é ele — não
        // uma remontagem — que precisa ser reenviado à SEFAZ quando a conexão voltar.
        if (jaEmContingencia)
        {
            nota.Status          = NotaFiscalStatus.AutorizadaContingencia;
            nota.Serie           = cfg.SerieNfce;
            nota.Numero          = numero;
            nota.ChaveAcesso     = chave.Chave;
            nota.UrlQrCode       = qrCodeUrl;
            nota.XmlContingencia = FuncoesXml.ClasseParaXmlString(nfe);
            nota.EmitidoEm     ??= DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

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
                // Registra os dados imutáveis e remonta imediatamente o documento como
                // tpEmis=9. A nota só vira AutorizadaContingencia dentro da segunda montagem,
                // depois que chave e QR offline estiverem prontos e persistidos.
                nota.Serie                     = cfg.SerieNfce;
                nota.Numero                    = numero;
                nota.CnfContingencia           = cNf;
                nota.DhContingencia            = DateTime.UtcNow;
                nota.EmitidoEm                ??= nota.DhContingencia;
                nota.JustificativaContingencia = "Sem comunicação com o webservice da SEFAZ no momento da venda.";
                await _db.SaveChangesAsync();

                _logger.LogWarning(ex,
                    "NFC-e {NotaId}: SEFAZ inalcançável; reconstruindo documento em contingência offline.", nota.Id);

                await TransmitirAsync(nota, dados);
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
            nota.AutorizadoEm   = DateTime.UtcNow;
            // Se veio de contingência, EmitidoEm já é o momento real da venda — não pisa nele
            // com o momento da confirmação tardia da SEFAZ.
            nota.EmitidoEm    ??= DateTime.UtcNow;
            nota.XmlAutorizado  = MontarNfeProcXml(nfe, retorno.Retorno!.protNFe);
            // A partir daqui o documento fiscal é o nfeProc autorizado. O XML de
            // contingência já cumpriu seu papel (foi entregue e retransmitido) e
            // deixa de ser fonte do DANFE — mantê-lo seria uma segunda via
            // possível do mesmo documento.
            nota.XmlContingencia = null;
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
            if (retorno.Retorno?.cStat == 225)
                _logger.LogError(
                    "SEFAZ rejeitou NFC-e {NotaId} com cStat 225. XML exato enviado: {XmlEnvio}",
                    nota.Id, retorno.EnvioStr);

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
                // A retransmissão foi rejeitada por regra de negócio: aquele
                // documento offline não vira nota válida. O próximo TransmitirAsync
                // reserva número novo e monta um documento novo, então o XML antigo
                // não deve sobreviver como fonte de DANFE.
                nota.XmlContingencia           = null;
            }
        }

        await _db.SaveChangesAsync();

        // Rejeição não inutiliza automaticamente: o XML pode ser corrigido e
        // retransmitido com o mesmo nNF/cNF. Inutilização fica no fluxo explícito de
        // abandono de número/faixa, com justificativa do responsável fiscal.
    }

    /// <summary>
    /// Reenvia à SEFAZ o XML assinado que já foi entregue ao consumidor em
    /// contingência offline (RES-002). Não remonta nem reassina: desserializa o
    /// documento persistido e transmite exatamente ele. A chave, o número, o
    /// dhEmi e o dhCont são os do documento original — a SEFAZ identifica a nota
    /// pela chave, então isto é a mesma NFC-e, agora buscando autorização.
    /// </summary>
    private async Task RetransmitirContingenciaAsync(NotaFiscalEmitida nota)
    {
        var (_, cfgServico, certificado, _, _, _) = await AbrirConfiguracaoSefazAsync();
        using var _certDispose = certificado;

        NfeDocumento nfe;
        try
        {
            nfe = FuncoesXml.XmlStringParaClasse<NfeDocumento>(nota.XmlContingencia!);
        }
        catch (Exception ex)
        {
            // XML de contingência corrompido em repouso: não dá pra retransmitir
            // nem inventar outro. Fica em contingência para tratamento manual —
            // o documento que o consumidor levou continua válido até o prazo legal.
            _logger.LogError(ex,
                "NFC-e {NotaId}: XML de contingência ilegível; retransmissão automática não é possível.", nota.Id);
            return;
        }

        using var servico = new ServicosNFe(cfgServico, certificado);
        RetornoNFeAutorizacao retorno;
        try
        {
            retorno = servico.NFeAutorizacao(1, IndicadorSincronizacao.Sincrono, new List<NfeDocumento> { nfe }, false);
        }
        catch (Exception ex) when (EhFalhaDeConectividade(ex))
        {
            _logger.LogWarning(ex,
                "NFC-e {NotaId} (em contingência desde {DhContingencia:o}) ainda não conseguiu retransmitir — " +
                "SEFAZ continua inalcançável.", nota.Id, nota.DhContingencia);
            return;
        }

        var protInfo = retorno.Retorno?.protNFe?.infProt;
        if (protInfo is not null && protInfo.cStat == 100)
        {
            nota.Status          = NotaFiscalStatus.Autorizada;
            nota.ChaveAcesso     = protInfo.chNFe ?? nota.ChaveAcesso;
            nota.Protocolo       = protInfo.nProt;
            nota.AutorizadoEm    = DateTime.UtcNow;
            // EmitidoEm é o momento real da venda offline — não pisa nele com o
            // instante da confirmação tardia (mesma regra do caminho normal).
            nota.EmitidoEm     ??= DateTime.UtcNow;
            nota.XmlAutorizado   = MontarNfeProcXml(nfe, retorno.Retorno!.protNFe);
            // O nfeProc autorizado passa a ser a fonte do DANFE; o XML offline
            // cumpriu seu papel.
            nota.XmlContingencia = null;
            nota.MotivoRejeicao  = null;
        }
        else
        {
            // Rejeição de negócio na retransmissão: o documento offline não vira
            // nota válida. Limpa os campos de reconstrução de contingência para o
            // próximo reprocessamento montar um documento novo do zero (mesma
            // lógica de F5 no caminho normal).
            nota.Status                    = NotaFiscalStatus.Rejeitada;
            nota.MotivoRejeicao            = protInfo?.xMotivo ?? retorno.RetornoStr ?? "SEFAZ não retornou motivo.";
            nota.CnfContingencia           = null;
            nota.DhContingencia            = null;
            nota.JustificativaContingencia = null;
            nota.XmlContingencia           = null;
        }

        await _db.SaveChangesAsync();
    }

    private static List<detPag> MontarDetPag(DadosEmissao dados, decimal valorTotal) =>
        MontarDetPag(dados.FormaPagamento, dados.SegundaFormaPagamento, dados.SegundoValorCentavos, valorTotal);

    /// <summary>
    /// Monta um ou dois detPag conforme haja segundo método de pagamento (split).
    /// O valor do primeiro método é o total menos o que foi pago no segundo, pra bater
    /// exatamente com vNF — evita a diferença de centavos ser "engolida" num só método.
    /// </summary>
    internal static List<detPag> MontarDetPag(
        string formaPagamento, string? segundaForma, int segundoValorCentavos, decimal valorTotal)
    {
        if (string.IsNullOrWhiteSpace(segundaForma) || segundoValorCentavos <= 0)
            return new List<detPag> { MontarDetPagUnico(formaPagamento, valorTotal) };

        var valorSegundo  = segundoValorCentavos / 100m;
        var valorPrimeiro = valorTotal - valorSegundo;
        return new List<detPag>
        {
            MontarDetPagUnico(formaPagamento, valorPrimeiro),
            MontarDetPagUnico(segundaForma, valorSegundo),
        };
    }

    /// <summary>
    /// Monta um detPag. Para cartão de crédito/débito E Pix, a SEFAZ exige o grupo `card`
    /// (rejeição observada em homologação: "Não informados os dados do cartão de
    /// crédito/débito" — a mesma rejeição aparece pra Pix, não só cartão; a validação
    /// da SEFAZ trata todo pagamento eletrônico igual, não só tPag 03/04). O sistema
    /// não integra com maquininha/TEF nem gateway de Pix — não há CNPJ da credenciadora,
    /// bandeira nem autorização pra informar — então o grupo é enviado só com
    /// `tpIntegra = Não integrado`, que é o mínimo aceito pela SEFAZ nesse caso.
    ///
    /// Crediário (05), pontos e cashback (19) agora usam código próprio e não
    /// precisam mais de xPag. O xPag fica reservado ao 99 ("Outros"), único
    /// código que a SEFAZ rejeita sem descrição ("Descrição do pagamento
    /// obrigatória para meio de pagamento 99-outros" — rejeição observada em
    /// produção). Se um meio novo cair no fpOutro, a descrição continua saindo.
    /// </summary>
    private static detPag MontarDetPagUnico(string formaPagamento, decimal valor)
    {
        var tPag = MapFormaPagamento(formaPagamento);
        var pag = new detPag { tPag = tPag, vPag = valor };
        if (formaPagamento is PaymentMethod.CartaoCredito or PaymentMethod.CartaoDebito or PaymentMethod.Pix)
            pag.card = new card { tpIntegra = TipoIntegracaoPagamento.TipNaoIntegrado };
        if (tPag == FormaPagamento.fpOutro)
            pag.xPag = DescricaoFormaPagamentoOutro(formaPagamento);
        return pag;
    }

    private static string DescricaoFormaPagamentoOutro(string formaPagamento) => formaPagamento switch
    {
        // Só é chamado quando o meio cai no fpOutro (99). Crediário, pontos e
        // cashback têm código próprio e não passam mais por aqui.
        _                       => formaPagamento,
    };

    internal static string SanitizarNcm(string ncm)
    {
        var digitos = new string((ncm ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitos.Length != 8)
            throw new FiscalNaoConfiguradoException(
                $"NCM \"{ncm}\" invalido. Informe exatamente 8 digitos numericos no cadastro do produto.");
        return digitos;
    }

    internal static string? SanitizarCest(string? cest, bool obrigatorio)
    {
        var digitos = new string((cest ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitos.Length == 0 && !obrigatorio) return null;
        if (digitos.Length != 7)
            throw new FiscalNaoConfiguradoException(
                obrigatorio
                    ? "CEST obrigatorio para produto sujeito a ICMS-ST. Informe exatamente 7 digitos no cadastro do produto."
                    : $"CEST \"{cest}\" invalido. Informe exatamente 7 digitos ou deixe o campo vazio.");
        return digitos;
    }

    /// <summary>
    /// Código do produto no XML (XML-001). Usa o Id do produto — identidade
    /// estável que cruza com estoque e escrituração —, não a posição do item na
    /// nota. Antes o cProd era "000001", "000002"… e não dava para relacionar a
    /// venda ao cadastro. Fallback para a posição só se o item não trouxer Id
    /// (não deveria acontecer em venda com produto real).
    /// </summary>
    internal static string MontarCodigoProduto(ItemFiscal item, int numero) =>
        item.ProdutoId is { } id ? id.ToString("N") : numero.ToString("D6");

    /// <summary>
    /// Valida o código de barras como GTIN antes de mandá-lo no cEAN (XML-001 /
    /// NT 2021.003). A SEFAZ rejeita (611) cEAN que não seja um GTIN válido ou o
    /// literal "SEM GTIN": mandar um código de barras interno malformado como se
    /// fosse GTIN derruba a nota. Só passa GTIN-8/12/13/14 com dígito verificador
    /// correto; qualquer outra coisa vira null e o chamador usa "SEM GTIN".
    ///
    /// O cálculo do dígito é o padrão GS1 (módulo 10) — a biblioteca fiscal só
    /// oferece consulta ao CCG por webservice, inviável a cada venda.
    /// </summary>
    internal static string? SanitizarGtin(string? gtin)
    {
        if (string.IsNullOrWhiteSpace(gtin)) return null;
        var digitos = new string(gtin.Where(char.IsDigit).ToArray());
        if (digitos.Length is not (8 or 12 or 13 or 14)) return null;

        // Dígito verificador GS1: soma ponderada 3/1 da direita para a esquerda,
        // excluindo o próprio DV; o total arredondado para a próxima dezena menos
        // a soma é o DV esperado.
        var soma = 0;
        for (var i = 0; i < digitos.Length - 1; i++)
        {
            var d = digitos[digitos.Length - 2 - i] - '0';
            soma += i % 2 == 0 ? d * 3 : d;
        }
        var dvEsperado = (10 - soma % 10) % 10;
        return dvEsperado == digitos[^1] - '0' ? digitos : null;
    }

    /// <summary>
    /// Descrição do item no XML (XML-001). O leiaute limita xProd a 120
    /// caracteres; um nome de produto mais longo, mandado cru, é rejeição na
    /// SEFAZ. Trunca sem quebrar no meio de um caractere multibyte e colapsa
    /// espaços — o nome comercial completo continua no cadastro.
    /// </summary>
    internal static string SanitizarXProd(string nome)
    {
        var limpo = string.Join(' ', (nome ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (limpo.Length == 0) return "Item sem descricao";
        return limpo.Length <= 120 ? limpo : limpo[..120].TrimEnd();
    }

    internal static int SanitizarCfop(string cfop)
    {
        var digitos = new string((cfop ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitos.Length != 4 || !int.TryParse(digitos, out var valor))
            throw new FiscalNaoConfiguradoException(
                $"CFOP \"{cfop}\" invalido. Informe exatamente 4 digitos numericos em Admin > Fiscal.");
        return valor;
    }

    internal static string? SanitizarCep(string? cep)
    {
        if (string.IsNullOrWhiteSpace(cep)) return null;
        var digitos = new string(cep.Where(char.IsDigit).ToArray());
        if (digitos.Length != 8)
            throw new FiscalNaoConfiguradoException(
                $"CEP \"{cep}\" invalido. Informe exatamente 8 digitos numericos em Admin > Fiscal.");
        return digitos;
    }

    internal static IReadOnlyList<int> DistribuirDesconto(IReadOnlyList<ItemFiscal> itens, int descontoTotalCentavos)
    {
        if (itens.Count == 0) return Array.Empty<int>();

        var valorBruto = itens.Sum(i => i.SubtotalCentavos);
        if (valorBruto <= 0) return new int[itens.Count];
        var desconto = Math.Clamp(descontoTotalCentavos, 0, valorBruto);
        var resultado = new int[itens.Count];
        var restante = desconto;

        for (var i = 0; i < itens.Count; i++)
        {
            var descontoItem = i == itens.Count - 1
                ? restante
                : (int)((long)desconto * itens[i].SubtotalCentavos / valorBruto);
            descontoItem = Math.Clamp(descontoItem, 0, itens[i].SubtotalCentavos);
            resultado[i] = descontoItem;
            restante -= descontoItem;
        }

        return resultado;
    }

    internal static det MontarItem(
        ItemFiscal item, int numero, int descontoCentavos = 0, bool incluirIbsCbs = false,
        RegimeTributario regime = RegimeTributario.SimplesNacional)
    {
        var regimeNormal = regime != RegimeTributario.SimplesNacional;
        var calculoSt = ItemTemIcmsSt(item, regimeNormal)
            ? CalcularIcmsStInclusoNoPreco(item, descontoCentavos, regimeNormal)
            : null;
        var desconto = descontoCentavos / 100m;
        // Em ST o preço do cadastro é final ao consumidor. Separamos o imposto sem
        // alterar o total cobrado: (vProd - vDesc) + vST + vFCPST = preço líquido.
        var valorProduto = calculoSt is null
            ? item.SubtotalCentavos / 100m
            : calculoSt.ValorOperacaoLiquido + desconto;
        // Mesma base para IBS/CBS e para PIS/COFINS: valor do produto menos o
        // desconto incondicional do item.
        var baseIbsCbs = Math.Max(0, valorProduto - desconto);
        var tributosAproximados = CalcularTributosAproximados(item, descontoCentavos);
        var cest = SanitizarCest(item.Cest, regimeNormal ? CstExigeCest(item.Cst) : CsosnExigeCest(item.Csosn));
        var gtin = SanitizarGtin(item.Gtin) ?? "SEM GTIN";

        return new det
        {
            nItem = numero,
            prod = new prod
            {
                cProd      = MontarCodigoProduto(item, numero),
                cEAN       = gtin,
                cEANTrib   = gtin,
                xProd      = SanitizarXProd(item.Nome),
                NCM        = SanitizarNcm(item.Ncm),
                CEST       = cest,
                CFOP       = SanitizarCfop(item.Cfop),
                uCom       = "UN",
                qCom       = item.Quantidade,
                vUnCom     = item.Quantidade > 0 ? valorProduto / item.Quantidade : 0,
                vProd      = valorProduto,
                vDesc      = desconto,
                uTrib      = "UN",
                qTrib      = item.Quantidade,
                vUnTrib    = item.Quantidade > 0 ? valorProduto / item.Quantidade : 0,
                indTot     = IndicadorTotal.ValorDoItemCompoeTotalNF,
            },
            imposto = new imposto
            {
                vTotTrib = tributosAproximados.Total,
                ICMS   = new ICMS
                {
                    TipoICMS = regimeNormal
                        ? MontarIcmsRegimeNormal(item, descontoCentavos, calculoSt)
                        : MontarIcmsSimplesNacional(item, descontoCentavos, calculoSt),
                },
                PIS    = new PIS    { TipoPIS    = MontarPis(item, regime, baseIbsCbs) },
                COFINS = new COFINS { TipoCOFINS = MontarCofins(item, regime, baseIbsCbs) },
                IBSCBS = incluirIbsCbs ? MontarIbsCbs2026(item, baseIbsCbs) : null,
            },
        };
    }

    internal sealed record TributosAproximados(
        decimal Federal, decimal Estadual, decimal Municipal, string Fonte)
    {
        public decimal Total => Federal + Estadual + Municipal;
    }

    internal static TributosAproximados CalcularTributosAproximados(
        ItemFiscal item, int descontoCentavos = 0)
    {
        if (!item.PercentualTributosFederais.HasValue ||
            !item.PercentualTributosEstaduais.HasValue ||
            !item.PercentualTributosMunicipais.HasValue ||
            string.IsNullOrWhiteSpace(item.FonteTributos))
            throw new FiscalNaoConfiguradoException(
                $"Produto \"{item.Nome}\" sem transparencia tributaria configurada. " +
                "Informe os percentuais federal, estadual e municipal e a fonte/versao em Admin > Estoque.");
        if (item.TributosPreenchidosAutomaticamente &&
            item.TributosVigenciaFim is { } fim && fim.Date < BrazilTime.NowBr().Date)
            throw new FiscalNaoConfiguradoException(
                $"Tabela IBPT do produto \"{item.Nome}\" venceu em {fim:dd/MM/yyyy}. " +
                "Sincronize o IBPT em Admin > Fiscal antes de emitir.");

        ValidarPercentualTributario(item.PercentualTributosFederais.Value, "federal", item.Nome);
        ValidarPercentualTributario(item.PercentualTributosEstaduais.Value, "estadual", item.Nome);
        ValidarPercentualTributario(item.PercentualTributosMunicipais.Value, "municipal", item.Nome);

        var baseCalculo = Math.Max(0, item.SubtotalCentavos - descontoCentavos) / 100m;
        return new TributosAproximados(
            ArredondarTributo(baseCalculo * item.PercentualTributosFederais.Value / 100m),
            ArredondarTributo(baseCalculo * item.PercentualTributosEstaduais.Value / 100m),
            ArredondarTributo(baseCalculo * item.PercentualTributosMunicipais.Value / 100m),
            item.FonteTributos.Trim());
    }

    private static void ValidarPercentualTributario(decimal percentual, string esfera, string produto)
    {
        if (percentual is < 0 or > 100)
            throw new FiscalNaoConfiguradoException(
                $"Percentual aproximado {esfera} do produto \"{produto}\" deve ficar entre 0 e 100.");
    }

    private static bool CsosnExigeCest(string? csosn) =>
        csosn is "201" or "202" or "203" or "500";

    /// <summary>Os CSTs que envolvem ST (próprio ou já retido) exigem CEST no item.</summary>
    private static bool CstExigeCest(string? cst) =>
        cst is "10" or "30" or "60" or "70";

    // ── PIS/COFINS ────────────────────────────────────────────────────────────
    //
    // No Simples Nacional os dois estão dentro do DAS: o XML sai com CST 99
    // ("Outras Operações") e valor zero — é o que a SEFAZ espera de CRT=1, e é o
    // que este motor sempre fez. Fora do Simples cada item precisa de CST e
    // alíquota reais, e a alíquota depende do regime de apuração:
    //
    //   Lucro Presumido → cumulativo:     PIS 0,65%  COFINS 3,00%
    //   Lucro Real      → não-cumulativo: PIS 1,65%  COFINS 7,60%
    //
    // A natureza de operação pode sobrescrever CST e alíquota (venda com
    // alíquota zero, monofásico, isenta) — o padrão do regime é só o ponto de
    // partida pra quem não configurou nada.

    private const decimal PisCumulativo       = 0.65m;
    private const decimal CofinsCumulativo    = 3.00m;
    private const decimal PisNaoCumulativo    = 1.65m;
    private const decimal CofinsNaoCumulativo = 7.60m;

    /// <summary>CSTs de PIS/COFINS que não têm base nem alíquota (isenta, alíquota zero, suspensão…).</summary>
    private static bool CstFederalSemTributo(string cst) =>
        cst is "04" or "05" or "06" or "07" or "08" or "09";

    internal static PISBasico MontarPis(ItemFiscal item, RegimeTributario regime, decimal baseCalculo)
    {
        if (regime == RegimeTributario.SimplesNacional)
            return new PISOutr { CST = CSTPIS.pis99, vBC = 0, pPIS = 0, vPIS = 0 };

        var cst = NormalizarCstFederal(item.CstPis, "01");
        var aliquota = item.AliquotaPis ?? (regime == RegimeTributario.LucroReal
            ? PisNaoCumulativo
            : PisCumulativo);

        if (CstFederalSemTributo(cst))
            return new PISNT { CST = MapCstPis(cst) };

        // CST 49..99 é o grupo "Outras Operações": aceita base e alíquota, mas
        // não entra no grupo de alíquota básica do leiaute.
        if (cst is not ("01" or "02"))
            return new PISOutr
            {
                CST = MapCstPis(cst),
                vBC = ArredondarTributo(baseCalculo),
                pPIS = aliquota,
                vPIS = ArredondarTributo(baseCalculo * aliquota / 100m),
            };

        return new PISAliq
        {
            CST  = MapCstPis(cst),
            vBC  = ArredondarTributo(baseCalculo),
            pPIS = aliquota,
            vPIS = ArredondarTributo(baseCalculo * aliquota / 100m),
        };
    }

    internal static COFINSBasico MontarCofins(ItemFiscal item, RegimeTributario regime, decimal baseCalculo)
    {
        if (regime == RegimeTributario.SimplesNacional)
            return new COFINSOutr { CST = CSTCOFINS.cofins99, vBC = 0, pCOFINS = 0, vCOFINS = 0 };

        var cst = NormalizarCstFederal(item.CstCofins, "01");
        var aliquota = item.AliquotaCofins ?? (regime == RegimeTributario.LucroReal
            ? CofinsNaoCumulativo
            : CofinsCumulativo);

        if (CstFederalSemTributo(cst))
            return new COFINSNT { CST = MapCstCofins(cst) };

        if (cst is not ("01" or "02"))
            return new COFINSOutr
            {
                CST = MapCstCofins(cst),
                vBC = ArredondarTributo(baseCalculo),
                pCOFINS = aliquota,
                vCOFINS = ArredondarTributo(baseCalculo * aliquota / 100m),
            };

        return new COFINSAliq
        {
            CST     = MapCstCofins(cst),
            vBC     = ArredondarTributo(baseCalculo),
            pCOFINS = aliquota,
            vCOFINS = ArredondarTributo(baseCalculo * aliquota / 100m),
        };
    }

    private static string NormalizarCstFederal(string? cst, string padrao)
    {
        if (string.IsNullOrWhiteSpace(cst)) return padrao;
        var limpo = new string(cst.Where(char.IsDigit).ToArray());
        return limpo.Length switch
        {
            0 => padrao,
            1 => "0" + limpo,
            2 => limpo,
            _ => throw new FiscalNaoConfiguradoException($"CST de PIS/COFINS \"{cst}\" inválido — use dois dígitos."),
        };
    }

    private static CSTPIS MapCstPis(string cst) =>
        Enum.TryParse<CSTPIS>($"pis{cst}", out var valor)
            ? valor
            : throw new FiscalNaoConfiguradoException(
                $"CST de PIS \"{cst}\" não existe no leiaute da NFC-e.");

    private static CSTCOFINS MapCstCofins(string cst) =>
        Enum.TryParse<CSTCOFINS>($"cofins{cst}", out var valor)
            ? valor
            : throw new FiscalNaoConfiguradoException(
                $"CST de COFINS \"{cst}\" não existe no leiaute da NFC-e.");

    /// <summary>
    /// Tributação integral usada na fase de testes de 2026. Pela regra UB16-10 da
    /// NT 2025.002, a base subtrai o desconto incondicional informado no item.
    /// </summary>
    internal static IbsCbsItem MontarIbsCbs2026(ItemFiscal item, decimal? baseCalculoInformada = null)
    {
        if (!string.Equals(item.IbsCbsCst, "000", StringComparison.Ordinal))
            throw new FiscalNaoConfiguradoException(
                $"CST IBS/CBS {item.IbsCbsCst} ainda exige um provedor de cálculo específico. " +
                "Configure CST 000 ou habilite o provedor correspondente para esta natureza.");
        if (string.IsNullOrWhiteSpace(item.IbsCbsClassTrib) || item.IbsCbsClassTrib.Length != 6 ||
            !item.IbsCbsClassTrib.All(char.IsDigit))
            throw new FiscalNaoConfiguradoException("cClassTrib do IBS/CBS deve conter 6 dígitos.");

        var baseCalculo = baseCalculoInformada ?? item.SubtotalCentavos / 100m;
        var valorIbsUf  = ArredondarTributo(baseCalculo * 0.001m);
        var valorCbs    = ArredondarTributo(baseCalculo * 0.009m);

        return new IbsCbsItem
        {
            CST        = IbsCbsCst.Cst000,
            cClassTrib = item.IbsCbsClassTrib,
            gIBSCBS = new IbsCbsItemValues
            {
                vBC = baseCalculo,
                gIBSUF = new IbsItemUf { pIBSUF = 0.1m, vIBSUF = valorIbsUf },
                gIBSMun = new IbsItemMun { pIBSMun = 0m, vIBSMun = 0m },
                vIBS = valorIbsUf,
                gCBS = new CbsItem { pCBS = 0.9m, vCBS = valorCbs },
            },
        };
    }

    internal static IbsCbsTotal MontarTotaisIbsCbs2026(IEnumerable<det> itens)
    {
        var grupos = itens.Select(i => i.imposto.IBSCBS!.gIBSCBS!).ToList();
        var baseTotal   = grupos.Sum(g => g.vBC);
        var ibsUfTotal  = grupos.Sum(g => g.gIBSUF!.vIBSUF);
        var ibsMunTotal = grupos.Sum(g => g.gIBSMun!.vIBSMun);
        var cbsTotal    = grupos.Sum(g => g.gCBS!.vCBS);

        return new IbsCbsTotal
        {
            vBCIBSCBS = baseTotal,
            gIBS = new IbsTotal
            {
                gIBSUF = new IbsTotalUf { vDif = 0, vDevTrib = 0, vIBSUF = ibsUfTotal },
                gIBSMun = new IbsTotalMun { vDif = 0, vDevTrib = 0, vIBSMun = ibsMunTotal },
                vIBS = ibsUfTotal + ibsMunTotal,
                vCredPres = 0,
                vCredPresCondSus = 0,
            },
            gCBS = new CbsTotal
            {
                vDif = 0,
                vDevTrib = 0,
                vCBS = cbsTotal,
                vCredPres = 0,
                vCredPresCondSus = 0,
            },
        };
    }

    internal sealed record TotaisIcms(
        decimal BaseIcms, decimal ValorIcms, decimal ValorDeson,
        decimal BaseSt, decimal ValorSt,
        decimal ValorFcp, decimal ValorFcpSt,
        decimal ValorPis, decimal ValorCofins);

    /// <summary>
    /// Consolida nos totais do documento os tributos destacados nos itens
    /// (REG-001).
    ///
    /// A versão anterior era um <c>switch</c> que só conhecia ICMSSN201 e
    /// ICMSSN202. Isso bastava no Simples — CSOSN não destaca ICMS próprio, e o
    /// resto do ICMSTot é legitimamente zero. Quando o motor passou a montar
    /// itens por CST (Lucro Presumido/Real), nenhuma das dez classes novas tinha
    /// <c>case</c>: o item destacava vICMS e o total mandava zero, o que é
    /// divergência entre soma dos itens e totalizador — rejeição certa, com
    /// numeração queimada. E o <c>default</c> silencioso não quebrava teste
    /// nenhum.
    ///
    /// Agora usa os getters polimórficos da própria biblioteca fiscal
    /// (<c>Tributacao.Extensions</c>), que operam sobre <c>ICMSBasico</c> e
    /// funcionam para qualquer subtipo. Não há mais <c>case</c> a esquecer: um
    /// CST novo entra sozinho. Isso não economiza linhas — elimina a classe
    /// inteira de bug.
    ///
    /// Exceção: o FCP não tem getter na biblioteca, então continua sendo lido
    /// por tipo. É o único ponto que precisa de manutenção ao surgir um grupo
    /// novo, e está isolado em <see cref="SomarFcp"/>.
    /// </summary>
    internal static TotaisIcms SomarTotaisIcms(IEnumerable<det> itens)
    {
        decimal baseIcms = 0, valorIcms = 0, valorDeson = 0;
        decimal baseSt = 0, valorSt = 0;
        decimal valorFcp = 0, valorFcpSt = 0;
        decimal valorPis = 0, valorCofins = 0;

        foreach (var item in itens)
        {
            var icms = item.imposto.ICMS?.TipoICMS;
            if (icms is not null)
            {
                baseIcms   += icms.GetIcmsBcValue();
                valorIcms  += icms.GetIcmsValue();
                valorDeson += icms.GetIcmsDesonValue();
                baseSt     += icms.GetIcmsBcStValue();
                valorSt    += icms.GetIcmsStValue();

                var (fcp, fcpSt) = SomarFcp(icms);
                valorFcp   += fcp;
                valorFcpSt += fcpSt;
            }

            if (item.imposto.PIS?.TipoPIS is { } pis)
                valorPis += pis.GetPisValue();
            if (item.imposto.COFINS?.TipoCOFINS is { } cofins)
                valorCofins += cofins.GetCofinsValue();
        }

        return new TotaisIcms(
            ArredondarTributo(baseIcms), ArredondarTributo(valorIcms), ArredondarTributo(valorDeson),
            ArredondarTributo(baseSt), ArredondarTributo(valorSt),
            ArredondarTributo(valorFcp), ArredondarTributo(valorFcpSt),
            ArredondarTributo(valorPis), ArredondarTributo(valorCofins));
    }

    /// <summary>
    /// FCP próprio e FCP-ST do item. Único grupo sem getter polimórfico na
    /// biblioteca — as classes que o possuem estão listadas explicitamente para
    /// que a ausência de um tipo seja visível aqui, e não um zero silencioso no
    /// total do documento.
    /// </summary>
    private static (decimal Fcp, decimal FcpSt) SomarFcp(ICMSBasico icms) => icms switch
    {
        ICMS00 x => (x.vFCP ?? 0, 0),
        ICMS10 x => (x.vFCP ?? 0, x.vFCPST ?? 0),
        ICMS20 x => (x.vFCP ?? 0, 0),
        ICMS30 x => (0, x.vFCPST ?? 0),
        ICMS51 x => (x.vFCP ?? 0, 0),
        ICMS70 x => (x.vFCP ?? 0, x.vFCPST ?? 0),
        ICMS90 x => (x.vFCP ?? 0, x.vFCPST ?? 0),
        // Simples Nacional: só os CSOSN com ST carregam FCP-ST.
        ICMSSN201 x => (0, x.vFCPST ?? 0),
        ICMSSN202 x => (0, x.vFCPST ?? 0),
        _ => (0, 0),
    };

    private static decimal ArredondarTributo(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static int DecimalParaCentavos(decimal valor) =>
        checked((int)Math.Round(valor * 100m, 0, MidpointRounding.AwayFromZero));

    internal static string MontarTextoTransparenciaTributaria(
        decimal federal, decimal estadual, decimal municipal, string fontes) =>
        $"Tributos aproximados: Federal R$ {FormatarValorFiscal(federal)}, " +
        $"Estadual R$ {FormatarValorFiscal(estadual)}, Municipal R$ {FormatarValorFiscal(municipal)}. " +
        $"Fonte: {fontes}. Lei 12.741/2012.";

    private static string FormatarValorFiscal(decimal valor) =>
        valor.ToString("F2", CultureInfo.GetCultureInfo("pt-BR"));

    internal sealed record CalculoIcmsSt(
        DeterminacaoBaseIcmsSt Modalidade, decimal? Mva, decimal? Reducao,
        decimal ValorOperacaoLiquido, decimal BaseSt, decimal AliquotaSt,
        decimal ValorSt, decimal? BaseFcpSt, decimal? AliquotaFcpSt, decimal? ValorFcpSt);

    private static bool CsosnTemIcmsSt(string? csosn) => csosn is "201" or "202" or "203";

    /// <summary>
    /// CSTs em que a LOJA é a substituta e recolhe o ST — o 60 fica de fora de
    /// propósito: nele o ST já foi retido pelo fornecedor, não há o que calcular.
    /// No 90 ("Outras") o ST é opcional: só entra se a natureza trouxer alíquota.
    /// </summary>
    private static bool CstTemIcmsSt(ItemFiscal item) =>
        item.Cst is "10" or "30" or "70" ||
        (item.Cst == "90" && item.AliquotaIcmsSt is > 0);

    /// <summary>Se este item recolhe ST, considerando o regime da loja.</summary>
    private static bool ItemTemIcmsSt(ItemFiscal item, bool regimeNormal) =>
        regimeNormal ? CstTemIcmsSt(item) : CsosnTemIcmsSt(item.Csosn);

    /// <summary>
    /// Decompõe o preço final já cobrado do consumidor em operação + ICMS-ST + FCP-ST.
    /// A fórmula segue a orientação nacional: ST = ICMS sobre BC-ST menos ICMS próprio.
    /// Serve aos dois regimes — o que muda entre eles é o código informado no XML
    /// (CSOSN 201/202/203 ou CST 10/30/70/90), não a conta.
    /// </summary>
    internal static CalculoIcmsSt CalcularIcmsStInclusoNoPreco(
        ItemFiscal item, int descontoCentavos = 0, bool regimeNormal = false)
    {
        if (!ItemTemIcmsSt(item, regimeNormal))
            throw new ArgumentException("O item não usa código de tributação com ICMS-ST.", nameof(item));

        var codigo = regimeNormal ? $"CST {item.Cst}" : $"CSOSN {item.Csosn}";
        if (item.ModalidadeBcSt is null || item.ModalidadeBcSt is < 0 or > 6)
            throw new FiscalNaoConfiguradoException($"{codigo}: informe a modalidade da BC-ST (0 a 6).");
        if (item.AliquotaIcmsSt is null or <= 0 || item.AliquotaIcmsProprio is null or < 0)
            throw new FiscalNaoConfiguradoException(
                $"{codigo}: informe as alíquotas do ICMS-ST e da operação própria.");

        var modalidade = (DeterminacaoBaseIcmsSt)item.ModalidadeBcSt.Value;
        var reducao = Math.Clamp(item.PercentualReducaoBcSt ?? 0, 0, 100) / 100m;
        var aliquotaSt = item.AliquotaIcmsSt.Value / 100m;
        var aliquotaPropria = item.AliquotaIcmsProprio.Value / 100m;
        var aliquotaFcp = Math.Clamp(item.AliquotaFcpSt ?? 0, 0, 100) / 100m;
        var precoFinal = Math.Max(0, item.SubtotalCentavos - descontoCentavos) / 100m;

        decimal operacao;
        decimal baseSt;
        decimal? mva = null;

        if (modalidade == DeterminacaoBaseIcmsSt.DbisMargemValorAgregado)
        {
            if (item.PercentualMvaSt is null or < 0)
                throw new FiscalNaoConfiguradoException($"{codigo}: informe o percentual de MVA-ST.");
            mva = item.PercentualMvaSt.Value;
            var fatorBase = (1 + mva.Value / 100m) * (1 - reducao);
            var fatorTotal = 1 + fatorBase * aliquotaSt - aliquotaPropria + fatorBase * aliquotaFcp;
            if (fatorTotal <= 0)
                throw new FiscalNaoConfiguradoException("Parâmetros de ICMS-ST resultaram em fator de cálculo inválido.");
            operacao = precoFinal / fatorTotal;
            baseSt = operacao * fatorBase;
        }
        else if (modalidade == DeterminacaoBaseIcmsSt.DbisValordaOperacao)
        {
            var fatorBase = 1 - reducao;
            var fatorTotal = 1 + fatorBase * aliquotaSt - aliquotaPropria + fatorBase * aliquotaFcp;
            if (fatorTotal <= 0)
                throw new FiscalNaoConfiguradoException("Parâmetros de ICMS-ST resultaram em fator de cálculo inválido.");
            operacao = precoFinal / fatorTotal;
            baseSt = operacao * fatorBase;
        }
        else
        {
            if (item.BaseStFixaEmCentavos is null or <= 0)
                throw new FiscalNaoConfiguradoException(
                    $"{codigo}: esta modalidade exige base/pauta ST fixa por unidade.");
            baseSt = item.BaseStFixaEmCentavos.Value / 100m * item.Quantidade * (1 - reducao);
            var impostoFixo = baseSt * (aliquotaSt + aliquotaFcp);
            operacao = (precoFinal - impostoFixo) / (1 - aliquotaPropria);
            if (operacao < 0)
                throw new FiscalNaoConfiguradoException("A base/pauta ST supera o preço final do item.");
        }

        operacao = ArredondarTributo(operacao);
        baseSt = ArredondarTributo(baseSt);
        var valorSt = ArredondarTributo(Math.Max(0, baseSt * aliquotaSt - operacao * aliquotaPropria));
        var valorFcp = aliquotaFcp > 0 ? ArredondarTributo(baseSt * aliquotaFcp) : (decimal?)null;

        // Absorve eventual centavo de arredondamento na operação para manter o total exato.
        operacao = precoFinal - valorSt - (valorFcp ?? 0);
        if (operacao < 0)
            throw new FiscalNaoConfiguradoException("ICMS-ST/FCP calculado supera o preço final do item.");

        return new CalculoIcmsSt(
            modalidade, mva, item.PercentualReducaoBcSt, operacao, baseSt,
            item.AliquotaIcmsSt.Value, valorSt,
            valorFcp.HasValue ? baseSt : null,
            valorFcp.HasValue ? item.AliquotaFcpSt : null,
            valorFcp);
    }

    internal static ICMSBasico MontarIcmsSimplesNacional(
        ItemFiscal item, int descontoCentavos = 0, CalculoIcmsSt? calculoSt = null)
    {
        if (item.OrigemMercadoria is < 0 or > 8)
            throw new FiscalNaoConfiguradoException("Origem da mercadoria deve estar entre 0 e 8.");
        var origem = (OrigemMercadoria)item.OrigemMercadoria;
        var baseLiquida = Math.Max(0, item.SubtotalCentavos - descontoCentavos) / 100m;

        return item.Csosn switch
        {
            "101" => new ICMSSN101
            {
                orig = origem, CSOSN = Csosnicms.Csosn101,
                pCredSN = item.PercentualCreditoSn ?? 0,
                vCredICMSSN = ArredondarTributo(baseLiquida * (item.PercentualCreditoSn ?? 0) / 100m),
            },
            "102" or null or "" => new ICMSSN102 { orig = origem, CSOSN = Csosnicms.Csosn102 },
            "103" => new ICMSSN102 { orig = origem, CSOSN = Csosnicms.Csosn103 },
            "300" => new ICMSSN102 { orig = origem, CSOSN = Csosnicms.Csosn300 },
            "400" => new ICMSSN102 { orig = origem, CSOSN = Csosnicms.Csosn400 },
            "500" => new ICMSSN500 { orig = origem, CSOSN = Csosnicms.Csosn500 },
            "900" => new ICMSSN900 { orig = origem, CSOSN = Csosnicms.Csosn900 },
            "201" => MontarIcmsSn201(item, calculoSt ?? CalcularIcmsStInclusoNoPreco(item, descontoCentavos), origem),
            "202" => MontarIcmsSn202(item, calculoSt ?? CalcularIcmsStInclusoNoPreco(item, descontoCentavos), origem, Csosnicms.Csosn202),
            "203" => MontarIcmsSn202(item, calculoSt ?? CalcularIcmsStInclusoNoPreco(item, descontoCentavos), origem, Csosnicms.Csosn203),
            _ => throw new FiscalNaoConfiguradoException(
                $"CSOSN \"{item.Csosn}\" não é suportado pelo provedor Simples Nacional."),
        };
    }

    /// <summary>
    /// Monta o ICMS de quem está FORA do Simples (CRT=3): CST no lugar do CSOSN.
    ///
    /// A diferença de fundo em relação ao Simples é que aqui o ICMS da operação
    /// própria é destacado no XML (vBC/pICMS/vICMS) em vez de ficar embutido no
    /// DAS. O cálculo do ST, quando existe, é o MESMO do Simples — a decomposição
    /// do preço final ao consumidor já estava pronta e é reaproveitada inteira.
    /// </summary>
    internal static ICMSBasico MontarIcmsRegimeNormal(
        ItemFiscal item, int descontoCentavos = 0, CalculoIcmsSt? calculoSt = null)
    {
        if (item.OrigemMercadoria is < 0 or > 8)
            throw new FiscalNaoConfiguradoException("Origem da mercadoria deve estar entre 0 e 8.");
        var origem = (OrigemMercadoria)item.OrigemMercadoria;

        var cst = string.IsNullOrWhiteSpace(item.Cst) ? null : item.Cst.Trim();
        if (cst is null)
            throw new FiscalNaoConfiguradoException(
                $"A loja está fora do Simples Nacional e a natureza de operação do item \"{item.Nome}\" " +
                "não tem CST de ICMS. Cadastre o CST em Admin > Fiscal > Naturezas de operação.");

        var baseCheia = Math.Max(0, item.SubtotalCentavos - descontoCentavos) / 100m;

        // Nos CSTs com ST, o preço de cadastro já é o final ao consumidor: a
        // operação própria é o que sobra depois de separar ST e FCP-ST.
        var valorOperacao = calculoSt?.ValorOperacaoLiquido ?? baseCheia;

        decimal AliquotaPropria()
        {
            if (item.AliquotaIcmsProprio is null or < 0)
                throw new FiscalNaoConfiguradoException(
                    $"CST {cst}: informe a alíquota de ICMS da operação própria na natureza de operação.");
            return item.AliquotaIcmsProprio.Value;
        }

        return cst switch
        {
            "00" => MontarIcms00(item, origem, valorOperacao, AliquotaPropria()),
            "20" => MontarIcms20(item, origem, valorOperacao, AliquotaPropria()),
            // Isenta, não tributada e suspensão compartilham o mesmo grupo no XML.
            "40" or "41" or "50" => new ICMS40 { orig = origem, CST = MapCst(cst) },
            "60" => MontarIcms60(item, origem),
            "10" => MontarIcms10(item, origem, ExigirSt(item, cst, calculoSt), AliquotaPropria()),
            "30" => MontarIcms30(item, origem, ExigirSt(item, cst, calculoSt)),
            "70" => MontarIcms70(item, origem, ExigirSt(item, cst, calculoSt), AliquotaPropria()),
            "90" => MontarIcms90(item, origem, valorOperacao, calculoSt),
            // 51 (diferimento) é operação de indústria/atacado; numa venda a
            // consumidor final por NFC-e ela não aparece, e implementar sem caso
            // de uso real seria código não exercitado no lugar mais sensível.
            "51" => throw new FiscalNaoConfiguradoException(
                "CST 51 (diferimento) não é aplicável a venda a consumidor final por NFC-e."),
            _ => throw new FiscalNaoConfiguradoException(
                $"CST de ICMS \"{cst}\" não é suportado pelo motor fiscal. " +
                "Use 00, 10, 20, 30, 40, 41, 50, 60, 70 ou 90."),
        };
    }

    private static CalculoIcmsSt ExigirSt(ItemFiscal item, string cst, CalculoIcmsSt? calculoSt) =>
        calculoSt ?? CalcularIcmsStInclusoNoPreco(item, 0, regimeNormal: true);

    private static ICMS00 MontarIcms00(
        ItemFiscal item, OrigemMercadoria origem, decimal baseCalculo, decimal aliquota)
    {
        var vbc = ArredondarTributo(baseCalculo);
        return new ICMS00
        {
            orig  = origem,
            CST   = Csticms.Cst00,
            modBC = DeterminacaoBaseIcms.DbiValorOperacao,
            vBC   = vbc,
            pICMS = aliquota,
            vICMS = ArredondarTributo(vbc * aliquota / 100m),
        };
    }

    private static ICMS20 MontarIcms20(
        ItemFiscal item, OrigemMercadoria origem, decimal baseCheia, decimal aliquota)
    {
        var reducao = Math.Clamp(item.PercentualReducaoBc ?? 0, 0, 100);
        if (reducao <= 0)
            throw new FiscalNaoConfiguradoException(
                "CST 20 exige o percentual de redução da base de cálculo na natureza de operação.");

        var vbc = ArredondarTributo(baseCheia * (1 - reducao / 100m));
        var icms = new ICMS20
        {
            orig   = origem,
            CST    = Csticms.Cst20,
            modBC  = DeterminacaoBaseIcms.DbiValorOperacao,
            pRedBC = reducao,
            vBC    = vbc,
            pICMS  = aliquota,
            vICMS  = ArredondarTributo(vbc * aliquota / 100m),
        };
        AplicarFcpProprio(item, vbc, valor => { icms.vBCFCP = vbc; icms.pFCP = item.AliquotaFcp; icms.vFCP = valor; });
        return icms;
    }

    private static ICMS60 MontarIcms60(ItemFiscal item, OrigemMercadoria origem) => new()
    {
        orig = origem,
        CST  = Csticms.Cst60,
        // Retenção anterior é informativa e boa parte do varejo não recebe esse
        // dado do fornecedor — só vai ao XML quando o contador cadastrou.
        vBCSTRet    = item.BaseStRetidaEmCentavos is > 0 ? item.BaseStRetidaEmCentavos.Value / 100m : null,
        vICMSSTRet  = item.ValorStRetidoEmCentavos is > 0 ? item.ValorStRetidoEmCentavos.Value / 100m : null,
    };

    private static ICMS10 MontarIcms10(
        ItemFiscal item, OrigemMercadoria origem, CalculoIcmsSt c, decimal aliquota)
    {
        var vbc = ArredondarTributo(c.ValorOperacaoLiquido);
        var icms = new ICMS10
        {
            orig    = origem,
            CST     = Csticms.Cst10,
            modBC   = DeterminacaoBaseIcms.DbiValorOperacao,
            vBC     = vbc,
            pICMS   = aliquota,
            vICMS   = ArredondarTributo(vbc * aliquota / 100m),
            modBCST = c.Modalidade,
            pMVAST  = c.Mva,
            pRedBCST = c.Reducao,
            vBCST   = c.BaseSt,
            pICMSST = c.AliquotaSt,
            vICMSST = c.ValorSt,
        };
        if (c.ValorFcpSt.HasValue)
        {
            icms.vBCFCPST = c.BaseFcpSt;
            icms.pFCPST   = c.AliquotaFcpSt;
            icms.vFCPST   = c.ValorFcpSt;
        }
        return icms;
    }

    private static ICMS30 MontarIcms30(ItemFiscal item, OrigemMercadoria origem, CalculoIcmsSt c)
    {
        var icms = new ICMS30
        {
            orig    = origem,
            CST     = Csticms.Cst30,
            modBCST = c.Modalidade,
            pMVAST  = c.Mva,
            pRedBCST = c.Reducao,
            vBCST   = c.BaseSt,
            pICMSST = c.AliquotaSt,
            vICMSST = c.ValorSt,
        };
        if (c.ValorFcpSt.HasValue)
        {
            icms.vBCFCPST = c.BaseFcpSt;
            icms.pFCPST   = c.AliquotaFcpSt;
            icms.vFCPST   = c.ValorFcpSt;
        }
        return icms;
    }

    private static ICMS70 MontarIcms70(
        ItemFiscal item, OrigemMercadoria origem, CalculoIcmsSt c, decimal aliquota)
    {
        var reducao = Math.Clamp(item.PercentualReducaoBc ?? 0, 0, 100);
        if (reducao <= 0)
            throw new FiscalNaoConfiguradoException(
                "CST 70 exige o percentual de redução da base de cálculo da operação própria.");

        var vbc = ArredondarTributo(c.ValorOperacaoLiquido * (1 - reducao / 100m));
        var icms = new ICMS70
        {
            orig    = origem,
            CST     = Csticms.Cst70,
            modBC   = DeterminacaoBaseIcms.DbiValorOperacao,
            pRedBC  = reducao,
            vBC     = vbc,
            pICMS   = aliquota,
            vICMS   = ArredondarTributo(vbc * aliquota / 100m),
            modBCST = c.Modalidade,
            pMVAST  = c.Mva,
            pRedBCST = c.Reducao,
            vBCST   = c.BaseSt,
            pICMSST = c.AliquotaSt,
            vICMSST = c.ValorSt,
        };
        if (c.ValorFcpSt.HasValue)
        {
            icms.vBCFCPST = c.BaseFcpSt;
            icms.pFCPST   = c.AliquotaFcpSt;
            icms.vFCPST   = c.ValorFcpSt;
        }
        return icms;
    }

    private static ICMS90 MontarIcms90(
        ItemFiscal item, OrigemMercadoria origem, decimal valorOperacao, CalculoIcmsSt? c)
    {
        var aliquota = item.AliquotaIcmsProprio ?? 0;
        var reducao = Math.Clamp(item.PercentualReducaoBc ?? 0, 0, 100);
        var vbc = ArredondarTributo(valorOperacao * (1 - reducao / 100m));

        var icms = new ICMS90
        {
            orig   = origem,
            CST    = Csticms.Cst90,
            modBC  = DeterminacaoBaseIcms.DbiValorOperacao,
            pRedBC = reducao > 0 ? reducao : null,
            vBC    = vbc,
            pICMS  = aliquota,
            vICMS  = ArredondarTributo(vbc * aliquota / 100m),
        };

        if (c is not null)
        {
            icms.modBCST = c.Modalidade;
            icms.pMVAST  = c.Mva;
            icms.pRedBCST = c.Reducao;
            icms.vBCST   = c.BaseSt;
            icms.pICMSST = c.AliquotaSt;
            icms.vICMSST = c.ValorSt;
            if (c.ValorFcpSt.HasValue)
            {
                icms.vBCFCPST = c.BaseFcpSt;
                icms.pFCPST   = c.AliquotaFcpSt;
                icms.vFCPST   = c.ValorFcpSt;
            }
        }
        return icms;
    }

    /// <summary>FCP da operação própria — só entra no XML quando a natureza traz alíquota.</summary>
    private static void AplicarFcpProprio(ItemFiscal item, decimal baseCalculo, Action<decimal> aplicar)
    {
        var aliquota = Math.Clamp(item.AliquotaFcp ?? 0, 0, 100);
        if (aliquota <= 0) return;
        aplicar(ArredondarTributo(baseCalculo * aliquota / 100m));
    }

    private static Csticms MapCst(string cst) => cst switch
    {
        "00" => Csticms.Cst00, "10" => Csticms.Cst10, "20" => Csticms.Cst20,
        "30" => Csticms.Cst30, "40" => Csticms.Cst40, "41" => Csticms.Cst41,
        "50" => Csticms.Cst50, "51" => Csticms.Cst51, "60" => Csticms.Cst60,
        "70" => Csticms.Cst70, "90" => Csticms.Cst90,
        _ => throw new FiscalNaoConfiguradoException($"CST de ICMS \"{cst}\" inválido."),
    };

    private static ICMSSN201 MontarIcmsSn201(ItemFiscal item, CalculoIcmsSt c, OrigemMercadoria origem)
    {
        var icms = new ICMSSN201
        {
            orig = origem, CSOSN = Csosnicms.Csosn201, modBCST = c.Modalidade,
            pMVAST = c.Mva, pRedBCST = c.Reducao, vBCST = c.BaseSt,
            pICMSST = c.AliquotaSt, vICMSST = c.ValorSt,
            pCredSN = item.PercentualCreditoSn ?? 0,
            vCredICMSSN = ArredondarTributo(c.ValorOperacaoLiquido * (item.PercentualCreditoSn ?? 0) / 100m),
        };
        AplicarFcpSt(icms, c);
        return icms;
    }

    private static ICMSSN202 MontarIcmsSn202(
        ItemFiscal item, CalculoIcmsSt c, OrigemMercadoria origem, Csosnicms csosn)
    {
        var icms = new ICMSSN202
        {
            orig = origem, CSOSN = csosn, modBCST = c.Modalidade,
            pMVAST = c.Mva, pRedBCST = c.Reducao, vBCST = c.BaseSt,
            pICMSST = c.AliquotaSt, vICMSST = c.ValorSt,
        };
        AplicarFcpSt(icms, c);
        return icms;
    }

    private static void AplicarFcpSt(ICMSSN201 icms, CalculoIcmsSt c)
    {
        if (!c.ValorFcpSt.HasValue) return;
        icms.vBCFCPST = c.BaseFcpSt;
        icms.pFCPST = c.AliquotaFcpSt;
        icms.vFCPST = c.ValorFcpSt;
    }

    private static void AplicarFcpSt(ICMSSN202 icms, CalculoIcmsSt c)
    {
        if (!c.ValorFcpSt.HasValue) return;
        icms.vBCFCPST = c.BaseFcpSt;
        icms.pFCPST = c.AliquotaFcpSt;
        icms.vFCPST = c.ValorFcpSt;
    }

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
    /// <summary>
    /// Traduz o meio comercial do ERP para o código da Tabela de Meios de
    /// Pagamento (FIS-002 do plano de go-live).
    ///
    /// Crediário, pontos e cashback caíam todos em 99 ("Outros"). Existem
    /// códigos próprios e vigentes para os dois casos, e usar 99 quando há
    /// código específico degrada a qualidade declaratória — ainda mais numa
    /// loja onde crediário e fidelidade são o modelo do negócio, não exceção:
    ///
    ///   • 05 (fpCartaoDaLoja) — "Cartão da Loja (Private Label), Crediário
    ///     Digital, Outros Crediários". A descrição foi ampliada pelo Informe
    ///     Técnico 2024.002, vigente em produção desde 01/07/2024; antes dele o
    ///     código cobria só o cartão de loja, origem da confusão comum.
    ///   • 19 (fpProgramadefidelidade) — "Programa de fidelidade, Cashback,
    ///     Crédito Virtual". Não confundir com 12 (vale-presente) nem com 21
    ///     (crédito em loja por troca/devolução, que é dinheiro já pago antes).
    ///
    /// O mapeamento é responsabilidade nossa: a biblioteca fiscal expõe todos os
    /// enums lado a lado e não escolhe nenhum.
    /// </summary>
    private static FormaPagamento MapFormaPagamento(string formaPagamento) => formaPagamento switch
    {
        PaymentMethod.Dinheiro      => FormaPagamento.fpDinheiro,
        PaymentMethod.Pix           => FormaPagamento.fpPagamentoInstantaneoPIXDinamico,
        PaymentMethod.CartaoCredito => FormaPagamento.fpCartaoCredito,
        PaymentMethod.CartaoDebito  => FormaPagamento.fpCartaoDebito,
        PaymentMethod.Crediario     => FormaPagamento.fpCartaoDaLoja,
        PaymentMethod.Pontos        => FormaPagamento.fpProgramadefidelidade,
        PaymentMethod.Cashback      => FormaPagamento.fpProgramadefidelidade,
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
