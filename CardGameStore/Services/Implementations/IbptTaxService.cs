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
            catch (Exception ex) when (ex is not OperationCanceledException)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
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
    private static string MensagemSegura(Exception ex) => ex is IbptIntegrationException ? ex.Message : "Falha inesperada ao consultar o IBPT.";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}

public sealed class IbptIntegrationException(string message) : Exception(message);

public sealed record IbptStatusDto(
    bool Configurado, bool AutoSyncAtivo, DateTime? UltimaSincronizacao, string? UltimaVersao,
    DateTime? VigenciaInicio, DateTime? VigenciaFim, string? UltimoErro,
    int ProdutosAtivos, int ProdutosAutomaticos, int ProdutosPendentes, int ProdutosVencidos);

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
