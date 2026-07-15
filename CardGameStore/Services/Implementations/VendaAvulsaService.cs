using System.Text.Json;
using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class VendaAvulsaService : IVendaAvulsaService
{
    private readonly AppDbContext                    _db;
    private readonly ILogger<VendaAvulsaService>    _logger;
    private readonly IServiceScopeFactory           _scopeFactory;
    private readonly ITenantContext                 _tenantContext;

    public VendaAvulsaService(
        AppDbContext db, ILogger<VendaAvulsaService> logger, IServiceScopeFactory scopeFactory,
        ITenantContext tenantContext)
    {
        _db            = db;
        _logger        = logger;
        _scopeFactory  = scopeFactory;
        _tenantContext = tenantContext;
    }

    public async Task<VendaAvulsaDto> RegisterAsync(VendaAvulsaRequest request, Guid adminId, string adminName)
    {
        // Pontos exige dois "sim": o módulo pago habilitado pra este tenant
        // (EnabledModules, decisão da plataforma) E o toggle operacional da loja
        // (SiteConfig.PontosFidelidadeAtivo, decisão do próprio admin do tenant —
        // sem linha ainda = default true). Defesa em profundidade: rejeita aqui
        // mesmo que um request forjado tente usar/resgatar pontos sem um dos dois.
        var usaPontosNestaVenda = request.PaymentMethod == PaymentMethod.Pontos || request.SecondPaymentMethod == PaymentMethod.Pontos;
        if (usaPontosNestaVenda && !_tenantContext.EnabledModules.Contains("pontos"))
            throw new InvalidOperationException("O módulo de fidelidade não está habilitado para esta loja.");

        var pontosAtivo = (await _db.SiteConfigs.FindAsync(SiteConfig.SingletonId))?.PontosFidelidadeAtivo ?? true;
        if (!pontosAtivo && usaPontosNestaVenda)
            throw new InvalidOperationException("O programa de pontos está desativado nesta loja.");

        // Valida tudo antes de qualquer escrita: falha rápida evita decremento parcial de estoque
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products   = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToListAsync();

        // Pré-carrega variantes necessárias
        var variantIds = request.Items.Where(i => i.VariantId.HasValue).Select(i => i.VariantId!.Value).ToList();
        var variants   = variantIds.Count > 0
            ? await _db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToListAsync()
            : new List<ProductVariant>();

        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Produto '{item.ProductId}' não encontrado ou inativo.");

            if (product.HasVariants)
            {
                if (!item.VariantId.HasValue)
                    throw new InvalidOperationException($"Produto '{product.Name}' tem grade — selecione tamanho/cor.");
                var variant = variants.FirstOrDefault(v => v.Id == item.VariantId && v.ProductId == product.Id)
                    ?? throw new InvalidOperationException($"Variante inválida para '{product.Name}'.");
                if (variant.StockQuantity < item.Quantity)
                    throw new InvalidOperationException(
                        $"Estoque insuficiente para '{product.Name} — {variant.Label}'. Disponível: {variant.StockQuantity}, solicitado: {item.Quantity}.");
            }
            else
            {
                if (product.StockQuantity < item.Quantity)
                    throw new InvalidOperationException(
                        $"Estoque insuficiente para '{product.Name}'. Disponível: {product.StockQuantity}, solicitado: {item.Quantity}.");
            }
        }

        // ── 2. Decrementar estoque no PostgreSQL (única transação relacional) ────
        var vendaItems = new List<VendaAvulsaItem>();
        var total      = 0;

        foreach (var reqItem in request.Items)
        {
            var product = products.First(p => p.Id == reqItem.ProductId);
            var effectivePrice = product.IsOnPromo ? product.DiscountPriceInCents!.Value : product.PriceInCents;
            string? variantLabel = null;

            if (product.HasVariants && reqItem.VariantId.HasValue)
            {
                var variant = variants.First(v => v.Id == reqItem.VariantId);
                // Preço específico da variante sobrepõe o produto pai
                if (variant.PriceInCents.HasValue) effectivePrice = variant.PriceInCents.Value;
                variantLabel = variant.Label;

                var updated = await _db.ProductVariants
                    .Where(v => v.Id == variant.Id && v.StockQuantity >= reqItem.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - reqItem.Quantity));
                if (updated == 0)
                    throw new InvalidOperationException($"Estoque insuficiente para '{product.Name} — {variant.Label}' (venda simultânea detectada).");
            }
            else
            {
                // Decremento atômico via ExecuteUpdateAsync — evita race condition em vendas simultâneas
                var updated = await _db.Products
                    .Where(p => p.Id == product.Id && p.StockQuantity >= reqItem.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        p => p.StockQuantity, p => p.StockQuantity - reqItem.Quantity));
                if (updated == 0)
                    throw new InvalidOperationException($"Estoque insuficiente para '{product.Name}' (venda simultânea detectada).");
            }

            var subtotal = effectivePrice * reqItem.Quantity;
            total += subtotal;

            vendaItems.Add(new VendaAvulsaItem
            {
                ProductId        = product.Id,
                ProductName      = product.Name,
                ProductCategory  = product.Category,
                Quantity         = reqItem.Quantity,
                UnitPriceInCents = effectivePrice,
                SubtotalInCents  = subtotal,
                UnitCostInCents  = product.CostPriceInCents,
                VariantId        = reqItem.VariantId,
                VariantLabel     = variantLabel,
            });
        }

        // Desconto em R$ sobrepõe percentual quando informado — mesmo padrão do EditarPagamentoAsync.
        var discountInCents = request.DiscountInCents.HasValue
            ? Math.Min(request.DiscountInCents.Value, total)
            : (int)Math.Round(total * request.DiscountPercent / 100.0);
        var discountPercentStored = request.DiscountInCents.HasValue ? 0 : request.DiscountPercent;
        var finalTotal = total - discountInCents;

        // ── Validação do segundo método de pagamento ──────────────────────────
        var secondPm  = string.IsNullOrWhiteSpace(request.SecondPaymentMethod) ? null : request.SecondPaymentMethod;
        var secondAmt = secondPm != null ? request.SecondPaymentAmountInCents : 0;

        if (secondPm != null)
        {
            if (secondAmt <= 0 || secondAmt >= finalTotal)
                throw new InvalidOperationException("Valor do segundo pagamento deve ser positivo e menor que o total.");
            if (secondPm == request.PaymentMethod)
                throw new InvalidOperationException("O segundo método de pagamento não pode ser igual ao principal.");
            if (secondPm is PaymentMethod.Cashback or PaymentMethod.Pontos && !request.UserId.HasValue)
                throw new InvalidOperationException("Cashback e Pontos como segundo pagamento exigem um cliente cadastrado selecionado.");
        }

        // Valor cobrado pelo método principal (total menos a parcela do segundo método)
        var primaryAmt = finalTotal - secondAmt;

        // ── 3. Persistir evento de caixa (PostgreSQL) ──────────────────────────────
        // Resolve nome do cliente: prioriza nome explícito, depois busca no banco pelo userId
        string? clientNameResolved = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim();
        if (clientNameResolved == null && request.UserId.HasValue)
        {
            var usr = await _db.Users.FindAsync(request.UserId.Value);
            clientNameResolved = usr?.Name;
        }

        var venda = new VendaAvulsa
        {
            Items                      = vendaItems,
            TotalInCents               = finalTotal,
            DiscountPercent            = discountPercentStored,
            DiscountInCents            = discountInCents,
            PaymentMethod              = request.PaymentMethod,
            SecondPaymentMethod        = secondPm,
            SecondPaymentAmountInCents = secondAmt,
            ClientName                 = clientNameResolved,
            UserId                     = request.UserId,
            UserName                   = clientNameResolved,
            SoldAt                     = DateTime.UtcNow,
            SoldByAdminId              = adminId,
            SoldByAdminName            = adminName,
        };

        _db.VendasAvulsas.Add(venda);
        await _db.SaveChangesAsync();

        // Emite a NFC-e referente a esta venda avulsa — só quando o admin escolheu
        // explicitamente emitir no fechamento (a loja não quer nota emitida sem antes
        // perguntar). Aguarda o resultado (em vez de fire-and-forget) pra devolver o status
        // pro caixa na hora e permitir abrir o cupom automaticamente quando autorizar — a
        // chamada nunca lança exceção (garantia do NfceEmissionService). Se não marcou,
        // nenhuma NotaFiscalEmitida é criada; a emissão pode ser feita depois manualmente
        // pelo histórico.
        // Defesa em profundidade: se a loja não contratou o módulo fiscal, ignora a
        // flag silenciosamente mesmo que um request forjado tente forçar EmitirNotaFiscal=true.
        NotaFiscalEmitida? nota = null;
        if (request.EmitirNotaFiscal && _tenantContext.EnabledModules.Contains("fiscal", StringComparer.OrdinalIgnoreCase))
        {
            using var scope = _scopeFactory.CreateScope();
            // O novo escopo tem seu próprio ITenantContext (default = tenant-zero) —
            // sem propagar o tenant resolvido pela requisição, o AppDbContext deste
            // escopo conecta no schema errado (nunca acha a venda que acabou de
            // gravar, ou grava a nota no schema de outro tenant).
            scope.ServiceProvider.GetRequiredService<ITenantContext>()
                .Set(_tenantContext.TenantId, _tenantContext.SchemaName, _tenantContext.EnabledModules);
            var emissao = scope.ServiceProvider.GetRequiredService<INfceEmissionService>();
            nota = await emissao.EmitirParaVendaAvulsaAsync(venda.Id);
        }

        var paymentSummary = secondPm != null
            ? $"{request.PaymentMethod} + {secondPm} (R$ {secondAmt / 100m:N2})"
            : request.PaymentMethod;
        _logger.LogInformation(
            "Venda avulsa {Id} registrada por {Admin}: {Count} item(ns), R$ {Total:F2} (desconto R$ {Desc:F2}), {Payment}",
            venda.Id, adminName, vendaItems.Count, finalTotal / 100m, discountInCents / 100m, paymentSummary);

        // ── Pós-venda: operações que dependem de cliente cadastrado ──────────────
        var pm = request.PaymentMethod;
        if (pm is PaymentMethod.Crediario or PaymentMethod.Pontos or PaymentMethod.Cashback)
        {
            if (!request.UserId.HasValue)
                throw new InvalidOperationException(
                    "Crediário, Pontos e Cashback exigem um cliente cadastrado selecionado.");

            var userId = request.UserId.Value;
            var user   = await _db.Users.FindAsync(userId)
                ?? throw new InvalidOperationException("Cliente não encontrado.");

            if (pm == PaymentMethod.Crediario)
            {
                var crediarioExistente = await _db.Crediarios
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto);
                var vencimento = DateTime.UtcNow.AddDays(30);

                // Snapshot dos itens desta venda para registrar no crediário
                var novosItens = vendaItems.Select(i => new ItemCrediarioDto
                {
                    ItemName         = i.ProductName,
                    Quantity         = i.Quantity,
                    UnitPriceInReais = i.UnitPriceInCents / 100m,
                    SubtotalInReais  = i.SubtotalInCents  / 100m,
                }).ToList();

                if (crediarioExistente != null)
                {
                    var itensAtuais = string.IsNullOrWhiteSpace(crediarioExistente.ItensJson)
                        ? new List<ItemCrediarioDto>()
                        : JsonSerializer.Deserialize<List<ItemCrediarioDto>>(crediarioExistente.ItensJson)
                          ?? new List<ItemCrediarioDto>();

                    itensAtuais.AddRange(novosItens);
                    crediarioExistente.ItensJson        = JsonSerializer.Serialize(itensAtuais);
                    crediarioExistente.ValorEmCentavos += primaryAmt;
                    crediarioExistente.DataVencimento   = vencimento;
                    _logger.LogInformation(
                        "Venda avulsa acumulada no crediário {CredId} do usuário {UserId} — novo total R$ {Valor:N2}",
                        crediarioExistente.Id, userId, crediarioExistente.ValorEmCentavos / 100m);
                }
                else
                {
                    var crediario = new Crediario
                    {
                        UserId           = userId,
                        ComandaId        = null,
                        ValorEmCentavos  = primaryAmt,
                        DataAbertura     = DateTime.UtcNow,
                        DataVencimento   = vencimento,
                        Status           = CrediariosStatus.Aberto,
                        AbertoPorAdminId = adminId,
                        Observacao       = "Venda avulsa no balcão",
                        ItensJson        = JsonSerializer.Serialize(novosItens),
                    };
                    _db.Crediarios.Add(crediario);
                    _logger.LogInformation(
                        "Crediário {CredId} criado para usuário {UserId} via venda avulsa — R$ {Valor:N2}",
                        crediario.Id, userId, primaryAmt / 100m);
                }
            }
            else if (pm == PaymentMethod.Pontos)
            {
                if (user.PointsExpiresAt.HasValue && user.PointsExpiresAt.Value < DateTime.UtcNow)
                    throw new InvalidOperationException("Os pontos deste cliente estão expirados.");

                // UPDATE atômico: debita pontos somente se o saldo for suficiente (evita race condition)
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.PointsBalance >= primaryAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.PointsBalance, u => u.PointsBalance - primaryAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente. Cliente tem {user.PointsBalance} pts, método principal custa {primaryAmt} pts.");
                _logger.LogInformation(
                    "Usuário {UserId} usou {Pts} pontos (principal) em venda avulsa.", userId, primaryAmt);
            }
            else if (pm == PaymentMethod.Cashback)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.BalanceInCents >= primaryAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.BalanceInCents, u => u.BalanceInCents - primaryAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo insuficiente. Cliente tem R$ {user.BalanceInCents / 100m:N2}, método principal custa R$ {primaryAmt / 100m:N2}.");
                _logger.LogInformation(
                    "Usuário {UserId} usou R$ {Valor:N2} de cashback (principal) em venda avulsa.", userId, primaryAmt / 100m);
            }

            // Aplica o segundo método de pagamento (Cashback ou Pontos como complemento)
            if (secondPm == PaymentMethod.Cashback)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.BalanceInCents >= secondAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.BalanceInCents, u => u.BalanceInCents - secondAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo cashback insuficiente para o segundo pagamento. Disponível: R$ {user.BalanceInCents / 100m:N2}.");
                _logger.LogInformation("Usuário {UserId} usou R$ {Amt:N2} de cashback como segundo pagamento.", userId, secondAmt / 100m);
            }
            else if (secondPm == PaymentMethod.Pontos)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.PointsBalance >= secondAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.PointsBalance, u => u.PointsBalance - secondAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente para o segundo pagamento. Disponível: {user.PointsBalance} pts.");
                _logger.LogInformation("Usuário {UserId} usou {Pts} pontos como segundo pagamento.", userId, secondAmt);
            }

            await _db.SaveChangesAsync();
        }
        else if (request.UserId.HasValue)
        {
            // Pagamento normal (Pix / Dinheiro / Cartão) com cliente identificado
            var userId = request.UserId.Value;
            var user   = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new InvalidOperationException("Cliente não encontrado.");

            // Aplica o segundo método de pagamento se houver (UPDATE atômico)
            if (secondPm == PaymentMethod.Cashback)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.BalanceInCents >= secondAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.BalanceInCents, u => u.BalanceInCents - secondAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo cashback insuficiente para o segundo pagamento. Disponível: R$ {user.BalanceInCents / 100m:N2}.");
                _logger.LogInformation("Usuário {UserId} usou R$ {Amt:N2} de cashback como segundo pagamento.", userId, secondAmt / 100m);
            }
            else if (secondPm == PaymentMethod.Pontos)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.PointsBalance >= secondAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.PointsBalance, u => u.PointsBalance - secondAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente para o segundo pagamento. Disponível: {user.PointsBalance} pts.");
                _logger.LogInformation("Usuário {UserId} usou {Pts} pontos como segundo pagamento.", userId, secondAmt);
            }

            // Acumula pontos de fidelidade: 1 ponto por R$1 gasto (só se o programa estiver ativo)
            var pontosGanhos = pontosAtivo ? finalTotal / 100 : 0;
            if (pontosGanhos > 0)
            {
                var expirado = user.PointsExpiresAt.HasValue && user.PointsExpiresAt.Value < DateTime.UtcNow;
                if (expirado)
                    await _db.Users
                        .Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(u => u.PointsBalance, pontosGanhos)
                            .SetProperty(u => u.PointsExpiresAt, DateTime.UtcNow.AddDays(30))
                            .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                else
                    await _db.Users
                        .Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(u => u.PointsBalance, u => u.PointsBalance + pontosGanhos)
                            .SetProperty(u => u.PointsExpiresAt, DateTime.UtcNow.AddDays(30))
                            .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                _logger.LogInformation(
                    "Usuário {UserId} ganhou {Pontos} pontos em venda avulsa {VendaId}.",
                    userId, pontosGanhos, venda.Id);
            }
        }

        var dto = MapToDto(venda);
        dto.NotaFiscalId             = nota?.Id;
        dto.NotaFiscalStatus         = nota?.Status.ToString();
        dto.NotaFiscalMotivoRejeicao = nota?.MotivoRejeicao;
        return dto;
    }

    public async Task<IEnumerable<VendaAvulsaDto>> GetRecentAsync(int limit = 50, DateTime? desde = null)
    {
        var query = _db.VendasAvulsas.AsNoTracking().AsQueryable();
        if (desde.HasValue)
            query = query.Where(v => v.SoldAt >= desde.Value);

        var vendas = await query
            .OrderByDescending(v => v.SoldAt)
            .Take(limit)
            .ToListAsync();

        return vendas.Select(MapToDto);
    }

    public async Task<IEnumerable<VendaAvulsaDto>> GetByDateAsync(DateTime? date = null)
    {
        // Converte data BR → intervalo UTC para evitar o bug de timezone:
        // uma venda às 22h BR (= 01h UTC do dia seguinte) aparecia como "hoje".
        var (inicio, fim) = BrazilTime.Dia(date);

        var vendas = await _db.VendasAvulsas.AsNoTracking()
            .Where(v => v.SoldAt >= inicio && v.SoldAt < fim)
            .OrderByDescending(v => v.SoldAt)
            .ToListAsync();

        return vendas.Select(MapToDto);
    }

    public async Task<IEnumerable<VendaAvulsaDto>> GetByUserAsync(Guid userId)
    {
        var vendas = await _db.VendasAvulsas.AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.SoldAt)
            .ToListAsync();
        return vendas.Select(MapToDto);
    }

    public async Task<int> BackfillCostsAsync()
    {
        // Carrega todos os produtos com custo > 0 de uma vez para evitar N queries
        var produtos = await _db.Products
            .Where(p => p.CostPriceInCents > 0)
            .Select(p => new { p.Id, p.CostPriceInCents })
            .ToListAsync();

        var custoMap = produtos.ToDictionary(p => p.Id, p => p.CostPriceInCents);

        // Busca todas as vendas avulsas (sem limite — backfill é operação administrativa)
        var todasVendas = await _db.VendasAvulsas.ToListAsync();

        var totalAtualizados = 0;

        foreach (var venda in todasVendas)
        {
            // Reatribui a lista inteira (não só os itens) para que o value comparer do
            // conversor JSONB detecte a mudança — mutar item.UnitCostInCents in-place
            // não seria percebido pelo change tracker.
            var novosItens = venda.Items;
            var modificou  = false;

            foreach (var item in novosItens)
            {
                if (custoMap.TryGetValue(item.ProductId, out var custo) && item.UnitCostInCents != custo)
                {
                    item.UnitCostInCents = custo;
                    totalAtualizados++;
                    modificou = true;
                }
            }

            if (modificou)
                venda.Items = new List<VendaAvulsaItem>(novosItens);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("BackfillCosts: {N} item(s) de venda avulsa atualizados com custo.", totalAtualizados);
        return totalAtualizados;
    }

    public async Task<VendaAvulsaDto> EditarPagamentoAsync(Guid id, EditarPagamentoVendaAvulsaRequest request)
    {
        if (!PaymentMethod.IsValid(request.PaymentMethod))
            throw new ArgumentException($"Forma de pagamento inválida: {request.PaymentMethod}");

        if (request.SecondPaymentMethod != null && !PaymentMethod.IsValid(request.SecondPaymentMethod))
            throw new ArgumentException($"Segundo pagamento inválido: {request.SecondPaymentMethod}");

        var venda = await _db.VendasAvulsas.FindAsync(id)
            ?? throw new KeyNotFoundException($"Venda avulsa {id} não encontrada.");

        venda.PaymentMethod              = request.PaymentMethod;
        venda.SecondPaymentMethod        = request.SecondPaymentMethod;
        venda.SecondPaymentAmountInCents = request.SecondPaymentAmountInCents;

        // Nome do cliente
        if (request.ClearClientName)
            venda.ClientName = null;
        else if (!string.IsNullOrWhiteSpace(request.ClientName))
            venda.ClientName = request.ClientName.Trim();

        // Desconto — recalcula TotalInCents se mudar
        if (request.DiscountInCents.HasValue)
        {
            var originalTotal = venda.TotalInCents + venda.DiscountInCents;
            var newDiscount   = Math.Min(request.DiscountInCents.Value, originalTotal);
            venda.DiscountInCents = newDiscount;
            venda.DiscountPercent = 0;
            venda.TotalInCents    = originalTotal - newDiscount;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Venda avulsa {Id} atualizada: pagamento={PM}, cliente={CN}, desconto={Desc}.",
            id, request.PaymentMethod, venda.ClientName, venda.DiscountInCents);
        return MapToDto(venda);
    }

    private static VendaAvulsaDto MapToDto(VendaAvulsa v) => new()
    {
        Id                         = v.Id,
        ClientName                 = v.ClientName,
        PaymentMethod              = v.PaymentMethod,
        SecondPaymentMethod        = v.SecondPaymentMethod,
        SecondPaymentAmountInCents = v.SecondPaymentAmountInCents,
        TotalInReais               = v.TotalInReais,
        DiscountPercent            = v.DiscountPercent,
        DiscountInReais            = v.DiscountInReais,
        SoldAt                     = v.SoldAt,
        SoldByAdminName            = v.SoldByAdminName,
        Items                      = v.Items.Select(i => new VendaAvulsaItemDto
        {
            ProductName      = i.ProductName,
            ProductCategory  = i.ProductCategory,
            Quantity         = i.Quantity,
            UnitPriceInReais = i.UnitPriceInCents / 100m,
            SubtotalInReais  = i.SubtotalInReais,
            UnitCostInCents  = i.UnitCostInCents,
        }).ToList(),
    };
}
