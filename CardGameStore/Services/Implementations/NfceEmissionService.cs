// =============================================================================
// NfceEmissionService.cs — Motor de emissão de NFC-e via DFe.NET
//
// Monta o objeto NFe (ide/emit/dest/det/total/pag), assina com o certificado
// A1 do FiscalConfig e transmite à SEFAZ via NFe.Servicos.ServicosNFe.
//
// Simplificações conhecidas (documentadas para revisão futura com o contador):
//  - Pagamento dividido (segundo método) não é discriminado — usa só o principal.
//  - Itens sem Product vinculado (cartas TCG avulsas) usam NCM/CFOP/CSOSN de
//    fallback (NCM 9504.40.00 "cartas para jogar" + a Natureza de Operação
//    marcada como padrão).
//  - CSOSN só tem tratamento explícito para 102 e 500 — qualquer outro código
//    cadastrado numa Natureza de Operação cai no 102 (mais comum/neutro).
//  - PIS/COFINS sempre "Outras Operações" (CST 99) com alíquota zero — padrão
//    comum para Simples Nacional, mas não confirmado com o contador ainda.
//  - Cancelamento e inutilização assumem que "sem exceção + cStat esperado" é
//    sucesso — não foi possível testar contra a SEFAZ real neste ambiente.
// =============================================================================

using System.Security.Cryptography.X509Certificates;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using DFe.Classes.Entidades;
using DFe.Classes.Flags;
using DFe.Utils;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
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
using NFe.Utils;
using NFe.Utils.NFe;
using NfeDocumento = NFe.Classes.NFe;

namespace CardGameStore.Services.Implementations;

public class NfceEmissionService : INfceEmissionService
{
    // Janela legal pra cancelar uma NFC-e após autorizada (padrão nacional: 30 minutos).
    private static readonly TimeSpan JanelaCancelamento = TimeSpan.FromMinutes(30);

    // Trava contra loop de reprocessamento em nota permanentemente quebrada.
    private const int MaxTentativasReprocessamento = 10;

    private readonly AppDbContext                _db;
    private readonly IMongoDatabase              _mongo;
    private readonly EncryptionService           _enc;
    private readonly ILogger<NfceEmissionService> _logger;

    public NfceEmissionService(AppDbContext db, IMongoDatabase mongo, EncryptionService enc, ILogger<NfceEmissionService> logger)
    {
        _db     = db;
        _mongo  = mongo;
        _enc    = enc;
        _logger = logger;
    }

    public async Task<NotaFiscalEmitida> EmitirParaComandaAsync(Guid comandaId) =>
        await EmitirAsync(NotaFiscalOrigem.Comanda, comandaId, null);

    public async Task<NotaFiscalEmitida> EmitirParaVendaAvulsaAsync(string vendaAvulsaId) =>
        await EmitirAsync(NotaFiscalOrigem.VendaAvulsa, null, vendaAvulsaId);

    public async Task<NotaFiscalEmitida> ReprocessarAsync(Guid notaId)
    {
        var nota = await _db.NotasFiscaisEmitidas.FindAsync(notaId)
            ?? throw new InvalidOperationException($"Nota {notaId} não encontrada.");

        if (nota.Status is not (NotaFiscalStatus.PendenteEmissao or NotaFiscalStatus.Rejeitada))
            return nota; // Autorizada/Cancelada não têm o que reprocessar — devolve como está.

        if (nota.TentativasReprocessamento >= MaxTentativasReprocessamento)
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
                : await CarregarDadosVendaAvulsaAsync(nota.VendaAvulsaId!);

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

        if (nota.EmitidoEm is null || DateTime.UtcNow - nota.EmitidoEm.Value > JanelaCancelamento)
            throw new InvalidOperationException(
                $"Fora da janela legal de cancelamento ({JanelaCancelamento.TotalMinutes:0} minutos após a autorização).");

        var (cfg, cfgServico, certificado, _, _) = await AbrirConfiguracaoSefazAsync();
        using var _certDispose = certificado;

        using var servico = new ServicosNFe(cfgServico, certificado);
        var retorno = servico.RecepcaoEventoCancelamento(
            idlote: 1, sequenciaEvento: 1,
            protocoloAutorizacao: nota.Protocolo!, chaveNFe: nota.ChaveAcesso!,
            justificativa: justificativa.Trim(), cpfcnpj: cfg.Cnpj, dhEvento: DateTimeOffset.Now);

        var infEvento = retorno.Retorno?.retEvento?.FirstOrDefault()?.infEvento;
        if (infEvento is null || infEvento.cStat is not (135 or 136))
        {
            var motivo = infEvento?.xMotivo ?? retorno.RetornoStr ?? "SEFAZ não retornou motivo.";
            throw new InvalidOperationException($"SEFAZ rejeitou o cancelamento: {motivo}");
        }

        nota.Status                    = NotaFiscalStatus.Cancelada;
        nota.CanceladoEm                = DateTime.UtcNow;
        nota.JustificativaCancelamento  = justificativa.Trim();
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
            : await CarregarDadosVendaAvulsaAsync(nota.VendaAvulsaId!);

        var cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        var endereco = cfg is null ? "" : $"{cfg.Logradouro}, {cfg.Numero} - {cfg.Bairro} - {cfg.Municipio}/{cfg.Uf}";

        string? qrCodeUrl = null;
        if (nota.ChaveAcesso is not null && cfg is not null)
            qrCodeUrl = MontarUrlQrCode(cfg, nota.ChaveAcesso);

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
            QrCodeUrl:   qrCodeUrl);
    }

    /// <summary>
    /// Monta a URL do QR Code da NFC-e conforme o layout nacional (chave|versão|ambiente|idCSC|hash).
    /// Sem o CSC cadastrado (obtido na SEFAZ via emissor), retorna só um link de consulta manual,
    /// sem o hash de segurança — o cupom ainda funciona, mas o QR não é o oficial completo.
    /// Só cobre o portal de SP; outros estados usam URLs de consulta próprias.
    /// </summary>
    private static string MontarUrlQrCode(FiscalConfig cfg, string chave)
    {
        var baseUrl = cfg.Ambiente == AmbienteFiscal.Producao
            ? "https://www.qrcode.fazenda.sp.gov.br/qrcode"
            : "https://www.homologacao.qrcode.fazenda.sp.gov.br/qrcode";

        var tpAmb = cfg.Ambiente == AmbienteFiscal.Producao ? "1" : "2";

        if (string.IsNullOrWhiteSpace(cfg.CscId) || string.IsNullOrWhiteSpace(cfg.CscToken))
            return $"{baseUrl}?chNFe={chave}&nVersao=100&tpAmb={tpAmb}";

        var payload = $"{chave}|2|{tpAmb}|{cfg.CscId}";
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(payload + cfg.CscToken)));
        return $"{baseUrl}?p={payload}|{hash}";
    }

    // ── Orquestração ──────────────────────────────────────────────────────────

    private async Task<NotaFiscalEmitida> EmitirAsync(NotaFiscalOrigem origem, Guid? comandaId, string? vendaAvulsaId)
    {
        var nota = new NotaFiscalEmitida
        {
            Origem        = origem,
            ComandaId     = comandaId,
            VendaAvulsaId = vendaAvulsaId,
            Status        = NotaFiscalStatus.PendenteEmissao,
        };
        _db.NotasFiscaisEmitidas.Add(nota);
        await _db.SaveChangesAsync();

        await ExecutarComTratamentoDeErroAsync(nota, async () =>
        {
            var dados = origem == NotaFiscalOrigem.Comanda
                ? await CarregarDadosComandaAsync(comandaId!.Value)
                : await CarregarDadosVendaAvulsaAsync(vendaAvulsaId!);

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

    private record ItemFiscal(string Nome, string Ncm, string Cfop, string? Csosn, int Quantidade, int PrecoUnitarioCentavos, int SubtotalCentavos);
    private record DadosEmissao(List<ItemFiscal> Itens, string FormaPagamento, string? ClienteCpf);

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
            Quantidade:           item.Quantity,
            PrecoUnitarioCentavos: item.UnitPriceInCents,
            SubtotalCentavos:     item.SubtotalInCents
        )).ToList();

        return new DadosEmissao(itens, comanda.PaymentMethod ?? "Dinheiro", comanda.User?.Cpf);
    }

    private async Task<DadosEmissao> CarregarDadosVendaAvulsaAsync(string vendaAvulsaId)
    {
        var collection = _mongo.GetCollection<CardGameStore.Models.MongoDB.VendaAvulsa>("vendas_avulsas");
        var venda = await collection.Find(v => v.Id == vendaAvulsaId).FirstOrDefaultAsync()
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
                Quantidade:           item.Quantity,
                PrecoUnitarioCentavos: item.UnitPriceInCents,
                SubtotalCentavos:     item.SubtotalInCents
            );
        }).ToList();

        string? cpf = null;
        if (venda.UserId.HasValue)
            cpf = (await _db.Users.FindAsync(venda.UserId.Value))?.Cpf;

        return new DadosEmissao(itens, venda.PaymentMethod, cpf);
    }

    // ── Montagem, assinatura e transmissão ─────────────────────────────────────

    /// <summary>
    /// Carrega o certificado (descriptografado) e monta a config de conexão com a SEFAZ,
    /// reaproveitada por emissão, cancelamento e inutilização.
    /// </summary>
    private async Task<(FiscalConfig cfg, ConfiguracaoServico cfgServico, X509Certificate2 certificado, Estado estado, TipoAmbiente ambiente)>
        AbrirConfiguracaoSefazAsync()
    {
        var cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        if (cfg is null || !cfg.CertificadoConfigurado)
            throw new FiscalNaoConfiguradoException("Certificado digital ainda não configurado.");

        if (string.IsNullOrWhiteSpace(cfg.RazaoSocial) || string.IsNullOrWhiteSpace(cfg.Logradouro) ||
            string.IsNullOrWhiteSpace(cfg.CodigoMunicipioIbge) || string.IsNullOrWhiteSpace(cfg.Uf))
            throw new FiscalNaoConfiguradoException("Dados da empresa (razão social/endereço) incompletos em Admin > Fiscal.");

        var pfxBytes    = Convert.FromBase64String(_enc.Decrypt(cfg.CertificadoPfxEncrypted!));
        var senha       = _enc.Decrypt(cfg.CertificadoSenhaEncrypted!);
        var certificado = new X509Certificate2(pfxBytes, senha, X509KeyStorageFlags.Exportable);

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

        return (cfg, cfgServico, certificado, estado, ambiente);
    }

    private async Task TransmitirAsync(NotaFiscalEmitida nota, DadosEmissao dados)
    {
        var (cfg, cfgServico, certificado, estado, ambiente) = await AbrirConfiguracaoSefazAsync();
        using var _certDispose = certificado;

        var numero = cfg.ProximoNumeroNfce;
        var dhEmi  = DateTimeOffset.Now;
        var cNf    = Random.Shared.Next(10_000_000, 99_999_999);
        var chave  = ChaveFiscal.ObterChave(estado, dhEmi, cfg.Cnpj, ModeloDocumento.NFCe, cfg.SerieNfce, numero, (int)TipoEmissao.teNormal, cNf);

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
                    tpEmis  = TipoEmissao.teNormal,
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
                det = dados.Itens.Select((item, idx) => MontarItem(item, idx + 1)).ToList(),
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
                pag = new List<pag>
                {
                    new pag
                    {
                        detPag = new List<detPag>
                        {
                            new detPag { tPag = MapFormaPagamento(dados.FormaPagamento), vPag = valorTotal },
                        },
                    },
                },
            },
        };

        nfe.Assina(cfgServico, certificado);

        using var servico = new ServicosNFe(cfgServico, certificado);
        var retorno = servico.NFeAutorizacao(1, IndicadorSincronizacao.Sincrono, new List<NfeDocumento> { nfe }, false);

        var protInfo = retorno.Retorno?.protNFe?.infProt;

        // Número consumido nesta tentativa, autorizada ou não — a numeração da NFC-e
        // não pode ser reaproveitada sem um evento formal de inutilização.
        cfg.ProximoNumeroNfce = numero + 1;
        cfg.UpdatedAt         = DateTime.UtcNow;

        nota.Serie  = cfg.SerieNfce;
        nota.Numero = numero;

        if (protInfo is not null && protInfo.cStat == 100)
        {
            nota.Status         = NotaFiscalStatus.Autorizada;
            nota.ChaveAcesso    = protInfo.chNFe ?? chave.Chave;
            nota.Protocolo      = protInfo.nProt;
            nota.EmitidoEm      = DateTime.UtcNow;
            nota.XmlAutorizado  = retorno.EnvioStr;
        }
        else
        {
            nota.Status         = NotaFiscalStatus.Rejeitada;
            nota.MotivoRejeicao = protInfo?.xMotivo ?? retorno.RetornoStr ?? "SEFAZ não retornou motivo.";
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
        var retorno = servico.NfeInutilizacao(cfg.Cnpj, DateTime.Now.Year, ModeloDocumento.NFCe, cfg.SerieNfce, numero, numero, justificativa);

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
            ICMS   = new ICMS   { TipoICMS   = MontarIcmsSimplesNacional(item.Csosn) },
            PIS    = new PIS    { TipoPIS    = new PISOutr    { CST = CSTPIS.pis99,    vBC = 0, pPIS    = 0, vPIS    = 0 } },
            COFINS = new COFINS { TipoCOFINS = new COFINSOutr { CST = CSTCOFINS.cofins99, vBC = 0, pCOFINS = 0, vCOFINS = 0 } },
        },
    };

    /// <summary>
    /// CSOSN 500 (cobrança por substituição/antecipação, sem crédito) tem campos próprios;
    /// qualquer outro código cai no 102 ("sem permissão de crédito") — o mais comum e
    /// mais neutro pro Simples Nacional. Ajustar aqui se o contador indicar outro CSOSN.
    /// </summary>
    private static ICMSBasico MontarIcmsSimplesNacional(string? csosn) => csosn switch
    {
        "500" => new ICMSSN500 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn500 },
        _      => new ICMSSN102 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn102 },
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
        "Dinheiro"      => FormaPagamento.fpDinheiro,
        "Pix"           => FormaPagamento.fpPagamentoInstantaneoPIXDinamico,
        "CartaoCredito" => FormaPagamento.fpCartaoCredito,
        "CartaoDebito"  => FormaPagamento.fpCartaoDebito,
        _               => FormaPagamento.fpOutro,
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
