// =============================================================================
// ProductService.cs — Implementação de Produtos (estoque físico)
// =============================================================================
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ProductService : IProductService
{
    private readonly AppDbContext  _db;
    private readonly IPushService  _push;
    private readonly IEmailService _email;
    private readonly ILogger<ProductService> _logger;

    public ProductService(AppDbContext db, IPushService push, IEmailService email, ILogger<ProductService> logger)
    { _db = db; _push = push; _email = email; _logger = logger; }

    public IAsyncEnumerable<ProductPublicDto> StreamAllActivePublicAsync(string? category = null) =>
        BuildPublicQuery(includeHidden: false, category).AsAsyncEnumerable();

    public IAsyncEnumerable<ProductPublicDto> StreamAllStorePublicAsync() =>
        BuildPublicQuery(includeHidden: true, category: null).AsAsyncEnumerable();

    // Projeta somente os campos do contrato público e deixa o serializer
    // consumir o cursor do EF de forma assíncrona. Antes, 50 mil entidades com
    // todos os campos fiscais/internos eram materializadas numa List<Product>
    // antes de o primeiro byte da resposta ser escrito.
    private IQueryable<ProductPublicDto> BuildPublicQuery(bool includeHidden, string? category)
    {
        var query = _db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!includeHidden)
            query = query.Where(p => p.ShowOnMarketplace);
        if (category is not null)
            query = query.Where(p => p.Category == category);

        return query
            .OrderBy(p => p.Name)
            .Select(p => new ProductPublicDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Category = p.Category,
                Barcode = p.Barcode,
                PriceInCents = p.PriceInCents,
                StockQuantity = p.HasVariants
                    ? _db.Set<ProductVariant>()
                        .Where(v => v.ProductId == p.Id)
                        .Sum(v => (int?)v.StockQuantity) ?? 0
                    : p.StockQuantity,
                Ncm = p.Ncm,
                ImageUrl = p.ImageUrl,
                ImageUrls = p.ImageUrls,
                FullDescription = p.FullDescription,
                IsActive = p.IsActive,
                IsFeatured = p.IsFeatured,
                ShowOnSite = p.ShowOnSite,
                ShowOnMarketplace = p.ShowOnMarketplace,
                DiscountPriceInCents = p.DiscountPriceInCents,
                IsPreVenda = p.IsPreVenda,
                HasVariants = p.HasVariants,
                RestaurantProductionAreaId = p.RestaurantProductionAreaId,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            });
    }

    public async Task<IEnumerable<Product>> GetAllActiveAsync()
    {
        var list = await _db.Products
            .Where(p => p.IsActive && p.ShowOnMarketplace)
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync();
        await ApplyVariantStockAsync(list);
        return list;
    }

    public async Task<IEnumerable<Product>> GetAllForAdminAsync()
    {
        var list = await _db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync();
        await ApplyVariantStockAsync(list);
        return list;
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        var list = await _db.Products
            .Where(p => p.IsActive && p.ShowOnMarketplace && p.Category == category)
            .AsNoTracking()
            .ToListAsync();
        await ApplyVariantStockAsync(list);
        return list;
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (p?.HasVariants == true)
            p.StockQuantity = await _db.Set<ProductVariant>()
                .Where(v => v.ProductId == id)
                .SumAsync(v => v.StockQuantity);
        return p;
    }

    // Busca soma de estoque por variante em query agrupada — evita Include que causaria
    // referência circular ProductVariant→Product→Variants na serialização JSON.
    private async Task ApplyVariantStockAsync(List<Product> products)
    {
        var ids = products.Where(p => p.HasVariants).Select(p => p.Id).ToList();
        if (ids.Count == 0) return;

        var sums = await _db.Set<ProductVariant>()
            .Where(v => ids.Contains(v.ProductId))
            .GroupBy(v => v.ProductId)
            .Select(g => new { g.Key, Total = g.Sum(v => v.StockQuantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);

        foreach (var p in products.Where(p => p.HasVariants))
            if (sums.TryGetValue(p.Id, out var sum))
                p.StockQuantity = sum;
    }

    public async Task<Product> CreateAsync(Product product)
    {
        ValidarDadosComerciais(product);
        NormalizarDadosFiscais(product);
        LimparMetadadosIbpt(product);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<Product> UpdateAsync(Product updated)
    {
        ValidarDadosComerciais(updated);
        NormalizarDadosFiscais(updated);
        var existing = await _db.Products.FindAsync(updated.Id)
            ?? throw new KeyNotFoundException($"Produto {updated.Id} não encontrado.");

        var estoqueAntes = existing.StockQuantity;
        var ncmMudou = !string.Equals(existing.Ncm, updated.Ncm, StringComparison.Ordinal);
        var transparenciaMudou =
            existing.PercentualTributosFederais != updated.PercentualTributosFederais ||
            existing.PercentualTributosEstaduais != updated.PercentualTributosEstaduais ||
            existing.PercentualTributosMunicipais != updated.PercentualTributosMunicipais ||
            !string.Equals(existing.FonteTributos, updated.FonteTributos, StringComparison.Ordinal);

        // Atualização campo a campo — evita sobrescrever com null/0 campos não enviados pelo frontend.
        existing.Name                 = updated.Name;
        existing.Description          = updated.Description;
        existing.Category             = updated.Category;
        existing.Barcode              = updated.Barcode;
        existing.CostPriceInCents     = updated.CostPriceInCents;
        existing.PriceInCents         = updated.PriceInCents;
        existing.DiscountPriceInCents = updated.DiscountPriceInCents;
        existing.StockQuantity        = updated.StockQuantity;
        existing.MinimumStock         = updated.MinimumStock;
        existing.ImageUrl             = updated.ImageUrl;
        existing.ImageUrls            = updated.ImageUrls;
        existing.FullDescription      = updated.FullDescription;
        existing.IsActive             = updated.IsActive;
        existing.IsFeatured           = updated.IsFeatured;
        existing.ShowOnSite           = updated.ShowOnSite;
        existing.ShowOnMarketplace    = updated.ShowOnMarketplace;
        existing.IsPreVenda           = updated.IsPreVenda;
        existing.Ncm                  = updated.Ncm;
        existing.Cest                 = updated.Cest;
        existing.PercentualTributosFederais  = updated.PercentualTributosFederais;
        existing.PercentualTributosEstaduais = updated.PercentualTributosEstaduais;
        existing.PercentualTributosMunicipais = updated.PercentualTributosMunicipais;
        existing.FonteTributos        = updated.FonteTributos;
        existing.NaturezaOperacaoId   = updated.NaturezaOperacaoId;
        if (ncmMudou && !transparenciaMudou)
        {
            existing.PercentualTributosFederais = null;
            existing.PercentualTributosEstaduais = null;
            existing.PercentualTributosMunicipais = null;
            existing.FonteTributos = null;
            LimparMetadadosIbpt(existing);
        }
        else if (transparenciaMudou)
        {
            LimparMetadadosIbpt(existing);
        }
        existing.UpdatedAt            = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Reestoque (0 → positivo): avisa quem está na fila de espera. Nunca
        // derruba o update do produto — notificação é melhor-esforço.
        if (estoqueAntes <= 0 && existing.StockQuantity > 0)
        {
            try { await NotificarFilaDeEsperaAsync(existing); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao notificar fila de espera do produto {ProductId}", existing.Id);
            }
        }

        return existing;
    }

    // Sem isto a API aceitava preco negativo e a venda avulsa registrava um total
    // negativo no caixa (produto de -R$ 999 vendido => totalInCents -99900). O
    // frontend ja avisava, mas quem chama a API direto (import, MCP, integracao)
    // passava reto.
    private static void ValidarDadosComerciais(Product product)
    {
        if (product.PriceInCents < 0)
            throw new ArgumentException("Preco de venda nao pode ser negativo.");
        if (product.CostPriceInCents < 0)
            throw new ArgumentException("Preco de custo nao pode ser negativo.");
        if (product.DiscountPriceInCents is < 0)
            throw new ArgumentException("Preco promocional nao pode ser negativo.");
        if (product.StockQuantity < 0)
            throw new ArgumentException("Estoque nao pode ser negativo.");
        if (product.MinimumStock < 0)
            throw new ArgumentException("Estoque minimo nao pode ser negativo.");
        // Teto do estoque: sem ele um produto podia nascer com int.MaxValue e o
        // primeiro ajuste de +1 estourava o `integer` do Postgres do mesmo jeito.
        if (product.StockQuantity > MaxEstoque)
            throw new ArgumentException($"Estoque limitado a {MaxEstoque:N0} unidades.");
        if (product.MinimumStock > MaxEstoque)
            throw new ArgumentException($"Estoque minimo limitado a {MaxEstoque:N0} unidades.");
    }

    private static void NormalizarDadosFiscais(Product product)
    {
        product.Ncm = SomenteDigitosOuNull(product.Ncm);
        product.Cest = SomenteDigitosOuNull(product.Cest);
        product.FonteTributos = string.IsNullOrWhiteSpace(product.FonteTributos)
            ? null
            : product.FonteTributos.Trim();

        if (product.Ncm is not null && product.Ncm.Length != 8)
            throw new ArgumentException(
                $"NCM deve conter exatamente 8 digitos — foram informados {product.Ncm.Length}. Digite so os numeros, sem pontos.");
        if (product.Cest is not null && product.Cest.Length != 7)
            throw new ArgumentException(
                $"CEST deve conter exatamente 7 digitos — foram informados {product.Cest.Length}. Digite so os numeros, sem pontos.");

        ValidarPercentual(product.PercentualTributosFederais, "tributos federais");
        ValidarPercentual(product.PercentualTributosEstaduais, "tributos estaduais");
        ValidarPercentual(product.PercentualTributosMunicipais, "tributos municipais");
    }

    private static string? SomenteDigitosOuNull(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return new string(valor.Where(char.IsDigit).ToArray());
    }

    // ArgumentException (e nao ArgumentOutOfRangeException) porque a mensagem vai direto
    // pro usuario no 400 do controller — sem o sufixo "(Parameter 'percentual')".
    private static void ValidarPercentual(decimal? percentual, string campo)
    {
        if (percentual is < 0 or > 100)
            throw new ArgumentException($"Percentual de {campo} deve ficar entre 0 e 100.");
    }

    private static void LimparMetadadosIbpt(Product product)
    {
        product.TributosPreenchidosAutomaticamente = false;
        product.TributosAtualizadosEm = null;
        product.TributosVigenciaInicio = null;
        product.TributosVigenciaFim = null;
        product.IbptVersao = null;
        product.IbptChave = null;
    }

    /// <summary>
    /// Notifica todos da fila que ainda não foram avisados (in-app + push + email)
    /// e marca NotifiedAt — quem entrar na fila depois é avisado no próximo reestoque.
    /// </summary>
    private async Task NotificarFilaDeEsperaAsync(Product p)
    {
        var fila = await _db.ProductWaitLists
            .Include(w => w.User)
            .Where(w => w.ProductId == p.Id && w.NotifiedAt == null)
            .OrderBy(w => w.Position)
            .ToListAsync();
        if (fila.Count == 0) return;

        var titulo = "Chegou! 🎉";
        var corpo  = $"{p.Name} está disponível — você estava na fila de espera. Garanta o seu!";
        var link   = $"/produtos/{p.Id}";

        foreach (var w in fila)
        {
            if (w.UserId is Guid uid)
                _db.Notifications.Add(new Notification
                {
                    UserId = uid, Title = titulo, Body = corpo, Link = link, ImageUrl = p.ImageUrl,
                });
            w.NotifiedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        var userIds = fila.Where(w => w.UserId != null).Select(w => w.UserId!.Value).Distinct().ToList();
        if (userIds.Count > 0)
            await _push.SendToManyAsync(userIds, titulo, corpo, link, p.ImageUrl);

        var comEmail = fila
            .Where(w => !string.IsNullOrWhiteSpace(w.User?.Email))
            .Select(w => (w.User!.Email!, w.User.Name))
            .Distinct()
            .ToList();
        if (comEmail.Count > 0)
            await _email.SendAnuncioAsync(comEmail, $"Chegou: {p.Name}", corpo, p.ImageUrl, link);

        _logger.LogInformation("Fila de espera de {Produto}: {Qtd} pessoa(s) avisada(s) do reestoque.", p.Name, fila.Count);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product != null) { product.IsActive = false; await _db.SaveChangesAsync(); }
    }

    public async Task<IEnumerable<Product>> GetLowStockAsync() =>
        await _db.Products.Where(p => p.IsActive && p.StockQuantity <= p.MinimumStock).ToListAsync();

    public async Task<Product?> GetByBarcodeAsync(string barcode) =>
        await _db.Products.FirstOrDefaultAsync(p => p.IsActive && p.Barcode == barcode);

    /// <summary>Teto do ajuste manual de estoque numa unica chamada. Existe pra
    /// evitar que `stock_quantity + delta` estoure o `integer` do Postgres: com
    /// delta perto de int.MaxValue a soma virava "22003: integer out of range",
    /// que subia como 500 generico em vez de erro de validacao.</summary>
    private const int MaxAjusteEstoque = 1_000_000;

    /// <summary>Teto do estoque de um produto. Com este teto e o do ajuste, a soma
    /// `estoque + delta` nunca chega perto de int.MaxValue.</summary>
    internal const int MaxEstoque = 100_000_000;

    public async Task<bool> AdjustStockAsync(Guid id, int quantityDelta)
    {
        if (quantityDelta == 0) return true;
        if (Math.Abs((long)quantityDelta) > MaxAjusteEstoque)
            throw new ArgumentException(
                $"Ajuste de estoque limitado a {MaxAjusteEstoque:N0} unidades por vez — valor informado: {quantityDelta:N0}.");

        // Os dois limites viram comparação contra um valor já calculado aqui, em
        // vez de somar dentro do SQL: `estoque + delta` no WHERE é justamente a
        // conta que estourava o `integer` do Postgres antes de qualquer filtro.
        // Com o WHERE garantindo estoque <= teto - delta, a soma do SET nunca
        // passa de MaxEstoque.
        var estoqueMinimoNecessario = -(long)quantityDelta;   // estoque + delta >= 0
        var estoqueMaximoPermitido  = MaxEstoque - (long)quantityDelta;

        // UPDATE atômico — garante que estoque nunca fica negativo mesmo sob carga concorrente.
        // Retorna 0 rows se o produto não existe, não está ativo ou o delta resultaria em negativo.
        var rows = await _db.Products
            .Where(p => p.Id == id && p.IsActive &&
                        p.StockQuantity >= estoqueMinimoNecessario &&
                        p.StockQuantity <= estoqueMaximoPermitido)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantityDelta)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

        // 0 linhas engloba "não existe", "estoque insuficiente" e "passou do teto",
        // e quem chama traduz tudo pra "Estoque insuficiente" — que seria mentira no
        // caso do teto. A leitura extra só acontece nesse caminho de falha.
        if (rows == 0 && quantityDelta > 0)
        {
            var estoqueAtual = await _db.Products
                .Where(p => p.Id == id && p.IsActive)
                .Select(p => (int?)p.StockQuantity)
                .FirstOrDefaultAsync();

            if (estoqueAtual is int atual && atual > estoqueMaximoPermitido)
                throw new ArgumentException(
                    $"Estoque ficaria em {atual + (long)quantityDelta:N0}, acima do limite de {MaxEstoque:N0} unidades.");
        }

        return rows > 0;
    }
}
