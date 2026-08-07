using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

/// <summary>Integração tenant-aware com a API oficial De Olho no Imposto/IBPT.</summary>
public sealed class IbptTaxService
{
    private const string ClientName = "ibpt";
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly EncryptionService _encryption;
    private readonly ILogger<IbptTaxService> _logger;

    public IbptTaxService(
        AppDbContext db, IHttpClientFactory httpFactory, EncryptionService encryption,
        ILogger<IbptTaxService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<IbptStatusDto> ObterStatusAsync(CancellationToken ct = default)
    {
        var cfg = await _db.FiscalConfigs.FindAsync([FiscalConfig.SingletonId], ct);
        var hoje = BrazilTime.NowBr().Date;
        var produtos = await _db.Products.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);

        return new IbptStatusDto(
            Configurado: cfg?.IbptConfigurado == true,
            AutoSyncAtivo: cfg?.IbptAutoSyncEnabled == true,
            UltimaSincronizacao: cfg?.IbptUltimaSincronizacao,
            UltimaVersao: cfg?.IbptUltimaVersao,
            VigenciaInicio: cfg?.IbptVigenciaInicio,
            VigenciaFim: cfg?.IbptVigenciaFim,
            UltimoErro: cfg?.IbptUltimoErro,
            ProdutosAtivos: produtos.Count,
            ProdutosAutomaticos: produtos.Count(p => p.TributosPreenchidosAutomaticamente),
            ProdutosPendentes: produtos.Count(p => !TemTransparenciaCompleta(p)),
            ProdutosVencidos: produtos.Count(p => p.TributosPreenchidosAutomaticamente &&
                p.TributosVigenciaFim.HasValue && p.TributosVigenciaFim.Value.Date < hoje));
    }

    public async Task<IbptSyncResult> SincronizarTodosAsync(CancellationToken ct = default)
    {
        var cfg = await ObterConfiguracaoValidaAsync(ct);
        var padrao = await _db.NaturezasOperacao.AsNoTracking().FirstOrDefaultAsync(n => n.IsPadrao, ct);
        var produtos = await _db.Products
            .Include(p => p.NaturezaOperacao)
            .Where(p => p.IsActive && p.Ncm != null)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var atualizados = 0;
        var ignoradosManuais = 0;
        var erros = new List<string>();
        var cache = new Dictionary<(string Ncm, bool Importado), IbptProdutoResponse>();

        foreach (var produto in produtos)
        {
            ct.ThrowIfCancellationRequested();
            if (TemTransparenciaCompleta(produto) && !produto.TributosPreenchidosAutomaticamente)
            {
                ignoradosManuais++;
                continue;
            }

            try
            {
                var origem = produto.NaturezaOperacao?.OrigemMercadoria ?? padrao?.OrigemMercadoria ?? 0;
                var importado = OrigemUsaAliquotaImportada(origem);
                var ncm = SomenteDigitos(produto.Ncm!);
                if (!cache.TryGetValue((ncm, importado), out var resposta))
                {
                    resposta = await ConsultarApiAsync(cfg, produto, ncm, ct);
                    cache[(ncm, importado)] = resposta;
                }

                AplicarResposta(produto, resposta, importado);
                atualizados++;
            }
            // O filtro precisa da checagem do token, não só do tipo: HttpClient.Timeout
            // lança TaskCanceledException, que herda de OperationCanceledException e é
            // indistinguível de um cancelamento real pelo tipo. Sem `!ct.IsCancellationRequested`,
            // um IBPT lento escapava daqui e derrubava a sincronização inteira com 500 —
            // era o defeito que o usuário via como "Erro interno. Tente novamente em
            // instantes" ao clicar em "Sincronizar produtos agora".
            //
            // Com a checagem: timeout do IBPT vira erro DESTE produto e o laço segue;
            // cancelamento de verdade (usuário fechou a aba) continua abortando tudo.
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                var mensagem = MensagemSegura(ex);
                erros.Add($"{produto.Name}: {mensagem}");
                _logger.LogWarning("Falha IBPT no produto {ProductId}: {Message}", produto.Id, mensagem);
            }
        }

        AtualizarStatusConfiguracao(cfg, produtos.Where(p => p.TributosPreenchidosAutomaticamente), erros);
        await _db.SaveChangesAsync(ct);

        return new IbptSyncResult(produtos.Count, atualizados, ignoradosManuais, erros.Count, erros.Take(20).ToList());
    }

    /// <summary>
    /// IBPT-002 — passo do job diário: conversa com o IBPT e guarda o resultado na
    /// tabela local. É o único lugar do sistema que faz rede para o IBPT.
    ///
    /// Varre os NCMs distintos do catálogo, não os produtos: dez mil produtos de
    /// vinte NCMs são vinte consultas. E demorar aqui não custa nada, porque
    /// ninguém está esperando numa tela.
    /// </summary>
    public async Task<IbptSyncResult> AtualizarTabelaLocalAsync(CancellationToken ct = default)
    {
        var cfg = await ObterConfiguracaoValidaAsync(ct);
        var padrao = await _db.NaturezasOperacao.AsNoTracking().FirstOrDefaultAsync(n => n.IsPadrao, ct);
        var uf = cfg.Uf!.ToUpperInvariant();

        var produtos = await _db.Products
            .Include(p => p.NaturezaOperacao)
            .Where(p => p.IsActive && p.Ncm != null)
            .ToListAsync(ct);

        // (NCM, origem) e a unidade de consulta: a aliquota federal muda entre
        // nacional e importado, o resto nao depende do produto.
        var candidatos = produtos
            .Select(p => new
            {
                Ncm = SomenteDigitos(p.Ncm!),
                Importado = OrigemUsaAliquotaImportada(
                    p.NaturezaOperacao?.OrigemMercadoria ?? padrao?.OrigemMercadoria ?? 0),
                Produto = p,
            })
            .ToList();

        // NCM com tamanho errado era descartado em SILÊNCIO aqui: o job não
        // consultava, não registrava erro, e a aplicação da tabela depois dizia
        // apenas "NCM ainda não está na tabela local" — para sempre, sem que nada
        // no painel explicasse por quê. O produto ficava sem transparência
        // tributária e a NFC-e dele nunca era emitida.
        var erros = new List<string>();
        foreach (var invalido in candidatos.Where(c => c.Ncm.Length != 8))
            erros.Add(
                $"{invalido.Produto.Name}: NCM \"{invalido.Produto.Ncm}\" tem {invalido.Ncm.Length} " +
                "dígito(s); o IBPT exige exatamente 8. Corrija em Admin > Estoque.");

        var combinacoes = candidatos
            .Where(c => c.Ncm.Length == 8)
            .GroupBy(c => new { c.Ncm, c.Importado })
            .ToList();

        var existentes = await _db.IbptTabela
            .Where(e => e.Uf == uf)
            .ToDictionaryAsync(e => new EntradaChave(e.Ncm, e.Importado), ct);

        var atualizados = 0;

        foreach (var grupo in combinacoes)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Qualquer produto do grupo serve de amostra: a consulta leva
                // descricao/valor/GTIN dele, mas a aliquota e do NCM.
                var resposta = await ConsultarApiAsync(cfg, grupo.First().Produto, grupo.Key.Ncm, ct);
                UpsertEntrada(existentes, uf, grupo.Key.Ncm, grupo.Key.Importado, resposta);
                atualizados++;
            }
            catch (Exception ex) when (EhServicoIndisponivel(ex))
            {
                // O serviço não está no ar. Insistir nos demais NCMs só multiplica
                // a espera pelo mesmo resultado: em homologação foram 4 timeouts
                // idênticos por ciclo, a cada ciclo, cada um até o limite.
                //
                // O primeiro timeout já contou tudo o que havia para saber. Os
                // outros três eram só o job segurando um worker por minutos e
                // martelando um servidor que não responde.
                erros.Add(
                    "O IBPT não respondeu. A atualização foi interrompida neste ciclo e será " +
                    "retomada na próxima janela — a tabela anterior continua valendo.");
                _logger.LogWarning(ex,
                    "IBPT indisponível ao consultar o NCM {Ncm}. Ciclo interrompido após " +
                    "{Ok} de {Total} NCM(s).", grupo.Key.Ncm, atualizados, combinacoes.Count);
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Falha DESTE NCM (código recusado, resposta malformada): não diz
                // nada sobre os outros, então o laço segue. A linha antiga
                // continua valendo — tabela de ontem é melhor que nenhuma.
                var mensagem = MensagemSegura(ex);
                erros.Add($"NCM {grupo.Key.Ncm}: {mensagem}");
                _logger.LogWarning(
                    "Falha ao atualizar tabela IBPT do NCM {Ncm}: {Message}", grupo.Key.Ncm, mensagem);
            }
        }

        AtualizarStatusConfiguracao(cfg, produtos.Where(p => p.TributosPreenchidosAutomaticamente), erros);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tabela IBPT local atualizada: {Ok}/{Total} NCM(s), {Falhas} falha(s).",
            atualizados, combinacoes.Count, erros.Count);

        return new IbptSyncResult(combinacoes.Count, atualizados, 0, erros.Count, erros.Take(20).ToList());
    }

    private readonly record struct EntradaChave(string Ncm, bool Importado);

    private void UpsertEntrada(
        Dictionary<EntradaChave, IbptTabelaEntry> existentes,
        string uf, string ncm, bool importado, IbptProdutoResponse resposta)
    {
        var fonte = $"{resposta.Fonte} {resposta.Versao}".Trim();
        if (fonte.Length > 100)
            throw new IbptIntegrationException("Fonte e versao retornadas pelo IBPT ultrapassam 100 caracteres.");

        var chave = new EntradaChave(ncm, importado);
        if (!existentes.TryGetValue(chave, out var entrada))
        {
            entrada = new IbptTabelaEntry { Ncm = ncm, Uf = uf, Importado = importado };
            _db.IbptTabela.Add(entrada);
            existentes[chave] = entrada;
        }

        entrada.PercentualFederal   = importado ? resposta.Importado : resposta.Nacional;
        entrada.PercentualEstadual  = resposta.Estadual;
        entrada.PercentualMunicipal = resposta.Municipal;
        entrada.Fonte               = fonte;
        entrada.Versao              = resposta.Versao?.Trim();
        entrada.Chave               = resposta.Chave?.Trim();
        entrada.VigenciaInicio      = ParseData(resposta.VigenciaInicio, "inicio");
        entrada.VigenciaFim         = ParseData(resposta.VigenciaFim, "fim");
        entrada.AtualizadoEm        = DateTime.UtcNow;
    }

    /// <summary>
    /// Importa a tabela oficial a partir do CSV do pacote do IBPT, substituindo
    /// tudo o que houver para a UF da loja.
    ///
    /// Substituição total, e não mesclagem, de propósito: o arquivo é uma versão
    /// fechada e coerente (mesma vigência, mesma chave). Misturar linhas de
    /// versões diferentes produziria uma tabela que não corresponde a documento
    /// nenhum — e é exatamente isso que a fiscalização pediria para conferir.
    ///
    /// Cada NCM vira DUAS linhas, nacional e importada, porque a alíquota federal
    /// difere entre as duas e a origem é atributo do produto, não do NCM.
    /// </summary>
    public async Task<IbptImportacaoResult> ImportarTabelaCsvAsync(
        Stream conteudo, string? nomeArquivo, CancellationToken ct = default)
    {
        var cfg = await _db.FiscalConfigs.FindAsync([FiscalConfig.SingletonId], ct)
            ?? throw new IbptIntegrationException("Configure os dados fiscais da loja antes de importar a tabela.");
        if (string.IsNullOrWhiteSpace(cfg.Uf) || cfg.Uf.Length != 2)
            throw new IbptIntegrationException("Informe a UF da loja antes de importar a tabela do IBPT.");

        var uf = cfg.Uf.ToUpperInvariant();

        // A tabela é POR ESTADO e a UF não está no conteúdo do arquivo, só no
        // nome. Sem esta checagem, uma loja de MG importaria a tabela de SP e
        // passaria a emitir com alíquota estadual errada, sem nada denunciando.
        var ufDoArquivo = IbptTabelaCsvImporter.UfDoNomeDoArquivo(nomeArquivo);
        if (ufDoArquivo is not null && ufDoArquivo != uf)
            throw new IbptIntegrationException(
                $"Este arquivo é a tabela de {ufDoArquivo} e a loja está configurada em {uf}. " +
                "A alíquota estadual muda por UF — baixe o pacote do IBPT para a UF correta.");

        var leitura = IbptTabelaCsvImporter.Ler(conteudo);

        // Fora do change tracker: são ~24 mil linhas (dois registros por NCM), e
        // rastrear cada uma multiplicaria o tempo por nada.
        await _db.IbptTabela.Where(e => e.Uf == uf).ExecuteDeleteAsync(ct);

        var agora = DateTime.UtcNow;
        var entradas = new List<IbptTabelaEntry>(leitura.Linhas.Count * 2);
        foreach (var linha in leitura.Linhas)
        {
            foreach (var importado in new[] { false, true })
                entradas.Add(new IbptTabelaEntry
                {
                    Ncm                 = linha.Ncm,
                    Uf                  = uf,
                    Importado           = importado,
                    PercentualFederal   = importado ? linha.ImportadoFederal : linha.NacionalFederal,
                    PercentualEstadual  = linha.Estadual,
                    PercentualMunicipal = linha.Municipal,
                    Fonte               = Limitar($"{linha.Fonte} {linha.Versao}".Trim(), 100),
                    Versao              = linha.Versao,
                    Chave               = linha.Chave,
                    VigenciaInicio      = linha.VigenciaInicio,
                    VigenciaFim         = linha.VigenciaFim,
                    AtualizadoEm        = agora,
                });
        }

        var rastreamento = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            await _db.IbptTabela.AddRangeAsync(entradas, ct);
            cfg.IbptUltimaSincronizacao = agora;
            cfg.IbptUltimaVersao        = leitura.Versao;
            cfg.IbptVigenciaInicio      = leitura.VigenciaInicio;
            cfg.IbptVigenciaFim         = leitura.VigenciaFim;
            cfg.IbptUltimoErro          = null;
            cfg.UpdatedAt               = agora;
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = rastreamento;
        }

        _logger.LogInformation(
            "Tabela IBPT importada por arquivo: {Ncms} NCM(s), versão {Versao}, UF {Uf}.",
            leitura.Linhas.Count, leitura.Versao, uf);

        // Aplicar já: o motivo de importar é destravar produto e emissão agora.
        var aplicacao = await AplicarTabelaLocalAsync(ct);

        return new IbptImportacaoResult(
            NcmsImportados: leitura.Linhas.Count,
            LinhasIgnoradas: leitura.LinhasIgnoradas,
            Versao: leitura.Versao,
            VigenciaInicio: leitura.VigenciaInicio,
            VigenciaFim: leitura.VigenciaFim,
            ProdutosAtualizados: aplicacao.Atualizados,
            ProdutosSemTabela: aplicacao.Falhas);
    }

    private static string? Limitar(string valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor[..Math.Min(valor.Length, max)];

    /// <summary>
    /// IBPT-002 — o caminho do usuário: preenche o produto pela tabela LOCAL, sem
    /// tocar na rede. É isto que torna o cadastro instantâneo e imune a
    /// indisponibilidade do IBPT.
    ///
    /// Devolve false quando não há linha para o NCM — situação normal para um NCM
    /// novo, que o próximo ciclo do job resolve. Até lá o produto fica sem
    /// transparência tributária, exatamente como ficava quando a consulta falhava.
    /// </summary>
    public async Task<bool> PreencherProdutoDaTabelaLocalAsync(Guid productId, CancellationToken ct = default)
    {
        var cfg = await _db.FiscalConfigs.FindAsync([FiscalConfig.SingletonId], ct);
        if (cfg is null || !cfg.IbptAutoSyncEnabled || string.IsNullOrWhiteSpace(cfg.Uf)) return false;

        var produto = await _db.Products.Include(p => p.NaturezaOperacao)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (produto is null || string.IsNullOrWhiteSpace(produto.Ncm)) return false;

        // Preenchimento manual do contador continua tendo precedencia.
        if (TemTransparenciaCompleta(produto) && !produto.TributosPreenchidosAutomaticamente) return false;

        var origem = produto.NaturezaOperacao?.OrigemMercadoria ??
            (await _db.NaturezasOperacao.AsNoTracking().FirstOrDefaultAsync(n => n.IsPadrao, ct))?.OrigemMercadoria ?? 0;
        var importado = OrigemUsaAliquotaImportada(origem);
        var ncm = SomenteDigitos(produto.Ncm);
        var uf = cfg.Uf!.ToUpperInvariant();

        var entrada = await _db.IbptTabela.AsNoTracking().FirstOrDefaultAsync(
            e => e.Ncm == ncm && e.Uf == uf && e.Importado == importado, ct);
        if (entrada is null) return false;

        AplicarEntrada(produto, entrada);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Garante que o NCM de UM produto esteja na tabela local, buscando só ele se
    /// faltar — e então preenche o produto.
    ///
    /// Existe porque tirar a rede da requisição não podia significar tirar o
    /// preenchimento: cadastrar um produto com NCM novo deixava-o sem
    /// transparência tributária até o job do dia seguinte, e sem transparência a
    /// NFC-e daquele produto não é emitida. O usuário via o cadastro salvar e a
    /// venda falhar depois, sem ligação óbvia entre as duas coisas.
    ///
    /// Roda SEMPRE fora da requisição (ver ProductController): uma consulta só,
    /// para um NCM só, mas ainda assim rede — e rede não fica na frente de quem
    /// está esperando.
    ///
    /// Diferente do job, aqui a falha é registrada na configuração: sem isso, o
    /// lojista não teria como saber por que o produto continua sem tributos.
    /// </summary>
    public async Task<bool> GarantirNcmNaTabelaEPreencherAsync(
        Guid productId, CancellationToken ct = default)
    {
        if (await PreencherProdutoDaTabelaLocalAsync(productId, ct)) return true;

        var cfg = await _db.FiscalConfigs.FindAsync([FiscalConfig.SingletonId], ct);
        if (cfg is null || !cfg.IbptAutoSyncEnabled || !cfg.IbptConfigurado) return false;

        var produto = await _db.Products.Include(p => p.NaturezaOperacao)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (produto is null || string.IsNullOrWhiteSpace(produto.Ncm)) return false;
        if (TemTransparenciaCompleta(produto) && !produto.TributosPreenchidosAutomaticamente) return false;

        try
        {
            ValidarConfiguracao(cfg);
            var padrao = await _db.NaturezasOperacao.AsNoTracking()
                .FirstOrDefaultAsync(n => n.IsPadrao, ct);
            var importado = OrigemUsaAliquotaImportada(
                produto.NaturezaOperacao?.OrigemMercadoria ?? padrao?.OrigemMercadoria ?? 0);
            var ncm = SomenteDigitos(produto.Ncm);
            var uf = cfg.Uf!.ToUpperInvariant();

            var existentes = await _db.IbptTabela
                .Where(e => e.Uf == uf && e.Ncm == ncm)
                .ToDictionaryAsync(e => new EntradaChave(e.Ncm, e.Importado), ct);

            var resposta = await ConsultarApiAsync(cfg, produto, ncm, ct);
            UpsertEntrada(existentes, uf, ncm, importado, resposta);
            cfg.IbptUltimoErro = null;
            await _db.SaveChangesAsync(ct);

            return await PreencherProdutoDaTabelaLocalAsync(productId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Fica visível no painel fiscal, não só no log do servidor.
            cfg.IbptUltimoErro = $"NCM {produto.Ncm}: {MensagemSegura(ex)}";
            cfg.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Falha ao buscar o NCM {Ncm} sob demanda: {Message}", produto.Ncm, cfg.IbptUltimoErro);
            return false;
        }
    }

    private static void AplicarEntrada(Product produto, IbptTabelaEntry entrada)
    {
        produto.PercentualTributosFederais   = entrada.PercentualFederal;
        produto.PercentualTributosEstaduais  = entrada.PercentualEstadual;
        produto.PercentualTributosMunicipais = entrada.PercentualMunicipal;
        produto.FonteTributos                = entrada.Fonte;
        produto.TributosPreenchidosAutomaticamente = true;
        produto.TributosAtualizadosEm        = DateTime.UtcNow;
        produto.TributosVigenciaInicio       = entrada.VigenciaInicio;
        produto.TributosVigenciaFim          = entrada.VigenciaFim;
        produto.IbptVersao                   = entrada.Versao;
        produto.IbptChave                    = entrada.Chave;
        produto.UpdatedAt                    = DateTime.UtcNow;
    }

    /// <summary>
    /// Reaplica a tabela LOCAL a todos os produtos — é o que o botão da tela passa
    /// a fazer. Sem rede, então não estoura timeout nem com catálogo grande.
    /// </summary>
    public async Task<IbptSyncResult> AplicarTabelaLocalAsync(CancellationToken ct = default)
    {
        var cfg = await ObterConfiguracaoValidaAsync(ct);
        var uf = cfg.Uf!.ToUpperInvariant();
        var padrao = await _db.NaturezasOperacao.AsNoTracking().FirstOrDefaultAsync(n => n.IsPadrao, ct);

        var tabela = await _db.IbptTabela.AsNoTracking()
            .Where(e => e.Uf == uf)
            .ToDictionaryAsync(e => new EntradaChave(e.Ncm, e.Importado), ct);

        var produtos = await _db.Products
            .Include(p => p.NaturezaOperacao)
            .Where(p => p.IsActive && p.Ncm != null)
            .ToListAsync(ct);

        var atualizados = 0;
        var ignoradosManuais = 0;
        var semTabela = new List<string>();

        foreach (var produto in produtos)
        {
            if (TemTransparenciaCompleta(produto) && !produto.TributosPreenchidosAutomaticamente)
            {
                ignoradosManuais++;
                continue;
            }

            var importado = OrigemUsaAliquotaImportada(
                produto.NaturezaOperacao?.OrigemMercadoria ?? padrao?.OrigemMercadoria ?? 0);
            var chave = new EntradaChave(SomenteDigitos(produto.Ncm!), importado);
            if (!tabela.TryGetValue(chave, out var entrada))
            {
                semTabela.Add($"{produto.Name}: NCM ainda nao esta na tabela local.");
                continue;
            }

            AplicarEntrada(produto, entrada);
            atualizados++;
        }

        await _db.SaveChangesAsync(ct);
        return new IbptSyncResult(
            produtos.Count, atualizados, ignoradosManuais, semTabela.Count, semTabela.Take(20).ToList());
    }

    /// <summary>Preenche um produto apenas se estiver incompleto ou já for gerenciado pelo IBPT.</summary>
    public async Task<bool> TentarSincronizarProdutoAsync(Guid productId, CancellationToken ct = default)
    {
        var cfg = await _db.FiscalConfigs.FindAsync([FiscalConfig.SingletonId], ct);
        if (cfg is null || !cfg.IbptAutoSyncEnabled || !cfg.IbptConfigurado) return false;

        var produto = await _db.Products.Include(p => p.NaturezaOperacao)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (produto is null || string.IsNullOrWhiteSpace(produto.Ncm)) return false;
        if (TemTransparenciaCompleta(produto) && !produto.TributosPreenchidosAutomaticamente) return false;
        if (TemTransparenciaCompleta(produto) && produto.TributosPreenchidosAutomaticamente &&
            produto.TributosVigenciaFim is { } fim && fim.Date >= BrazilTime.NowBr().Date)
            return false;

        try
        {
            ValidarConfiguracao(cfg);
            var origem = produto.NaturezaOperacao?.OrigemMercadoria ??
                (await _db.NaturezasOperacao.AsNoTracking().FirstOrDefaultAsync(n => n.IsPadrao, ct))?.OrigemMercadoria ?? 0;
            var resposta = await ConsultarApiAsync(cfg, produto, SomenteDigitos(produto.Ncm), ct);
            AplicarResposta(produto, resposta, OrigemUsaAliquotaImportada(origem));
            AtualizarStatusConfiguracao(cfg, [produto], []);
            await _db.SaveChangesAsync(ct);
            return true;
        }
        // Mesma armadilha do laço acima: timeout do HttpClient chega como
        // OperationCanceledException e não pode ser confundido com cancelamento.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            cfg.IbptUltimoErro = MensagemSegura(ex);
            cfg.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Preenchimento automático IBPT falhou no produto {ProductId}: {Message}",
                productId, cfg.IbptUltimoErro);
            return false;
        }
    }

    private async Task<FiscalConfig> ObterConfiguracaoValidaAsync(CancellationToken ct)
    {
        var cfg = await _db.FiscalConfigs.FindAsync([FiscalConfig.SingletonId], ct)
            ?? throw new IbptIntegrationException("Configure os dados fiscais da loja antes de usar o IBPT.");
        ValidarConfiguracao(cfg);
        return cfg;
    }

    private static void ValidarConfiguracao(FiscalConfig cfg)
    {
        if (!cfg.IbptConfigurado)
            throw new IbptIntegrationException("Token IBPT não configurado.");
        if (SomenteDigitos(cfg.Cnpj).Length != 14)
            throw new IbptIntegrationException("CNPJ da loja deve conter 14 dígitos para consultar o IBPT.");
        if (string.IsNullOrWhiteSpace(cfg.Uf) || cfg.Uf.Length != 2)
            throw new IbptIntegrationException("UF da loja não configurada.");
    }

    private async Task<IbptProdutoResponse> ConsultarApiAsync(
        FiscalConfig cfg, Product produto, string ncm, CancellationToken ct)
    {
        if (ncm.Length != 8)
            throw new IbptIntegrationException("NCM deve conter 8 dígitos.");

        string token;
        try { token = _encryption.Decrypt(cfg.IbptTokenEncrypted!); }
        catch (Exception) { throw new IbptIntegrationException("Token IBPT armazenado não pôde ser descriptografado."); }

        var parametros = new Dictionary<string, string?>
        {
            ["token"] = token,
            ["cnpj"] = SomenteDigitos(cfg.Cnpj),
            ["codigo"] = ncm,
            ["uf"] = cfg.Uf!.ToUpperInvariant(),
            ["ex"] = "0",
            ["descricao"] = produto.Name,
            ["unidadeMedida"] = "UN",
            ["valor"] = (produto.PriceInCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
            ["gtin"] = string.IsNullOrWhiteSpace(produto.Barcode) ? "SEM GTIN" : produto.Barcode,
        };

        var uri = QueryHelpers.AddQueryString("api/v1/produtos", parametros);
        using var respostaHttp = await _httpFactory.CreateClient(ClientName).GetAsync(uri, ct);
        var corpo = await respostaHttp.Content.ReadAsStringAsync(ct);
        if (!respostaHttp.IsSuccessStatusCode)
            throw new IbptIntegrationException(respostaHttp.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Token IBPT recusado para o CNPJ da loja.",
                HttpStatusCode.TooManyRequests => "Limite de consultas do IBPT atingido; tente novamente mais tarde.",
                _ => $"IBPT indisponível (HTTP {(int)respostaHttp.StatusCode}).",
            });

        var resposta = DesserializarResposta(corpo);
        ValidarResposta(resposta, ncm);
        return resposta;
    }

    private static IbptProdutoResponse DesserializarResposta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var elemento = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().FirstOrDefault()
                : doc.RootElement;
            if (elemento.ValueKind != JsonValueKind.Object)
                throw new IbptIntegrationException("IBPT retornou uma resposta vazia.");
            return elemento.Deserialize<IbptProdutoResponse>(JsonOptions)
                ?? throw new IbptIntegrationException("IBPT retornou uma resposta vazia.");
        }
        catch (JsonException)
        {
            throw new IbptIntegrationException("IBPT retornou JSON inválido.");
        }
    }

    private static void ValidarResposta(IbptProdutoResponse resposta, string ncm)
    {
        if (SomenteDigitos(resposta.Codigo ?? "") != ncm)
            throw new IbptIntegrationException("IBPT não encontrou o NCM informado.");
        ValidarPercentual(resposta.Nacional, "nacional");
        ValidarPercentual(resposta.Importado, "importado");
        ValidarPercentual(resposta.Estadual, "estadual");
        ValidarPercentual(resposta.Municipal, "municipal");
        if (string.IsNullOrWhiteSpace(resposta.Fonte) || string.IsNullOrWhiteSpace(resposta.Versao))
            throw new IbptIntegrationException("Resposta IBPT sem fonte ou versão.");

        var fim = ParseData(resposta.VigenciaFim, "fim");
        if (fim.Date < BrazilTime.NowBr().Date)
            throw new IbptIntegrationException($"Tabela IBPT {resposta.Versao} vencida em {fim:dd/MM/yyyy}.");
    }

    private static void AplicarResposta(Product produto, IbptProdutoResponse resposta, bool importado)
    {
        // Calcula e valida tudo antes de tocar na entidade rastreada. Assim uma resposta
        // malformada não deixa alterações parciais serem persistidas pelo lote.
        var fonte = $"{resposta.Fonte} {resposta.Versao}".Trim();
        if (fonte.Length > 100)
            throw new IbptIntegrationException("Fonte e versão retornadas pelo IBPT ultrapassam 100 caracteres.");
        var vigenciaInicio = ParseData(resposta.VigenciaInicio, "início");
        var vigenciaFim = ParseData(resposta.VigenciaFim, "fim");
        var percentualFederal = importado ? resposta.Importado : resposta.Nacional;

        produto.PercentualTributosFederais = percentualFederal;
        produto.PercentualTributosEstaduais = resposta.Estadual;
        produto.PercentualTributosMunicipais = resposta.Municipal;
        produto.FonteTributos = fonte;
        produto.TributosPreenchidosAutomaticamente = true;
        produto.TributosAtualizadosEm = DateTime.UtcNow;
        produto.TributosVigenciaInicio = vigenciaInicio;
        produto.TributosVigenciaFim = vigenciaFim;
        produto.IbptVersao = resposta.Versao?.Trim();
        produto.IbptChave = resposta.Chave?.Trim();
        produto.UpdatedAt = DateTime.UtcNow;
    }

    private static void AtualizarStatusConfiguracao(
        FiscalConfig cfg, IEnumerable<Product> atualizados, IReadOnlyCollection<string> erros)
    {
        var lista = atualizados.ToList();
        cfg.IbptUltimaSincronizacao = DateTime.UtcNow;
        if (lista.Count > 0)
        {
            cfg.IbptUltimaVersao = lista.Select(p => p.IbptVersao)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? cfg.IbptUltimaVersao;
            cfg.IbptVigenciaInicio = lista.Where(p => p.TributosVigenciaInicio.HasValue)
                .Select(p => p.TributosVigenciaInicio).Min() ?? cfg.IbptVigenciaInicio;
            cfg.IbptVigenciaFim = lista.Where(p => p.TributosVigenciaFim.HasValue)
                .Select(p => p.TributosVigenciaFim).Min() ?? cfg.IbptVigenciaFim;
        }
        var resumoErros = string.Join(" | ", erros.Take(3));
        cfg.IbptUltimoErro = resumoErros.Length == 0 ? null : resumoErros[..Math.Min(500, resumoErros.Length)];
        cfg.UpdatedAt = DateTime.UtcNow;
    }

    private static bool TemTransparenciaCompleta(Product p) =>
        p.PercentualTributosFederais.HasValue && p.PercentualTributosEstaduais.HasValue &&
        p.PercentualTributosMunicipais.HasValue && !string.IsNullOrWhiteSpace(p.FonteTributos);

    private static bool OrigemUsaAliquotaImportada(int origem) => origem is not (0 or 3 or 4 or 5);

    private static void ValidarPercentual(decimal valor, string nome)
    {
        if (valor is < 0 or > 100)
            throw new IbptIntegrationException($"Percentual {nome} inválido na resposta IBPT.");
    }

    private static DateTime ParseData(string? valor, string campo)
    {
        var texto = valor?.Trim();
        var formatosData = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };
        if (DateOnly.TryParseExact(texto, formatosData, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dataSomente))
            return DateTime.SpecifyKind(dataSomente.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        if (DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var dataComFuso))
            return DateTime.SpecifyKind(dataComFuso.Date, DateTimeKind.Utc);

        throw new IbptIntegrationException($"Data de vigência ({campo}) inválida na resposta IBPT.");
    }

    private static string SomenteDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());
    /// <summary>
    /// Mensagem que vai para a tela. Nunca expõe detalhe interno — mas também não
    /// pode achatar tudo em "falha inesperada": timeout é o caso mais comum da
    /// integração e merece dizer o que houve, senão o lojista fica sem saber se o
    /// problema é o token dele ou o servidor do IBPT.
    /// </summary>
    /// <summary>
    /// Distingue "o serviço está fora" de "este NCM deu problema". A diferença
    /// decide se vale continuar o ciclo: um NCM recusado não diz nada sobre os
    /// outros; um serviço fora do ar diz tudo sobre todos.
    ///
    /// Timeout do HttpClient chega como TaskCanceledException — o mesmo tipo de
    /// um cancelamento real —, então a checagem é por tipo E por causa interna.
    /// </summary>
    private static bool EhServicoIndisponivel(Exception ex) =>
        ex is TimeoutException
        || ex is HttpRequestException
        || (ex is TaskCanceledException && ex.InnerException is TimeoutException)
        || (ex.InnerException is not null && EhServicoIndisponivel(ex.InnerException));

    private static string MensagemSegura(Exception ex) => ex switch
    {
        IbptIntegrationException => ex.Message,
        OperationCanceledException or TimeoutException =>
            "O IBPT não respondeu dentro do tempo limite. A tabela anterior continua valendo; " +
            "a próxima tentativa é automática.",
        _ => "Falha inesperada ao consultar o IBPT.",
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}

public sealed class IbptIntegrationException(string message) : Exception(message);

public sealed record IbptStatusDto(
    bool Configurado, bool AutoSyncAtivo, DateTime? UltimaSincronizacao, string? UltimaVersao,
    DateTime? VigenciaInicio, DateTime? VigenciaFim, string? UltimoErro,
    int ProdutosAtivos, int ProdutosAutomaticos, int ProdutosPendentes, int ProdutosVencidos);

public sealed record IbptImportacaoResult(
    int NcmsImportados, int LinhasIgnoradas, string? Versao,
    DateTime? VigenciaInicio, DateTime? VigenciaFim,
    int ProdutosAtualizados, int ProdutosSemTabela);

public sealed record IbptSyncResult(
    int Total, int Atualizados, int IgnoradosManuais, int Falhas, List<string> Erros);

internal sealed class IbptProdutoResponse
{
    public string? Codigo { get; init; }
    public string? UF { get; init; }
    public int EX { get; init; }
    public string? Descricao { get; init; }
    public decimal Nacional { get; init; }
    public decimal Estadual { get; init; }
    public decimal Importado { get; init; }
    public decimal Municipal { get; init; }
    public string? Tipo { get; init; }
    public string? VigenciaInicio { get; init; }
    public string? VigenciaFim { get; init; }
    public string? Chave { get; init; }
    public string? Versao { get; init; }
    public string? Fonte { get; init; }
}
