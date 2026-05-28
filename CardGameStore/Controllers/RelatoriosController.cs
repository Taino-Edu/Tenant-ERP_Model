// =============================================================================
// RelatoriosController.cs — Relatórios de vendas por categoria e produto
//
// GET /api/relatorios/vendas?mes=5&ano=2026
//   Combina:
//   • Comanda items  (PostgreSQL) — comandas Fechadas no mês
//   • VendaAvulsa items (MongoDB) — vendas de balcão no mês
//   Agrupa por categoria → produto, soma quantidades e totais.
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.MongoDB;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/relatorios")]
[Authorize(Policy = "AdminOnly")]
public class RelatoriosController : ControllerBase
{
    private readonly AppDbContext                   _db;
    private readonly IMongoCollection<VendaAvulsa> _vendas;

    public RelatoriosController(AppDbContext db, IMongoDatabase mongo)
    {
        _db     = db;
        _vendas = mongo.GetCollection<VendaAvulsa>("vendas_avulsas");
    }

    // -------------------------------------------------------------------------
    // GET /api/relatorios/vendas?mes=5&ano=2026
    // -------------------------------------------------------------------------
    [HttpGet("vendas")]
    public async Task<ActionResult<RelatorioVendasDto>> Vendas(
        [FromQuery] int mes = 0,
        [FromQuery] int ano = 0)
    {
        var agora = DateTime.UtcNow;
        if (mes <= 0 || mes > 12) mes = agora.Month;
        if (ano <= 0)             ano = agora.Year;

        var inicio = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fim    = inicio.AddMonths(1);

        // Emojis das categorias cadastradas
        var categorias = await _db.ProductCategories
            .ToDictionaryAsync(c => c.Name, c => c.Emoji ?? "📦");

        // ── 1. Itens de Comandas Fechadas no mês ─────────────────────────────
        var comandaItems = await _db.ComandaItems
            .Include(i => i.Product)
            .Include(i => i.Comanda)
            .Where(i =>
                i.Comanda.Status == ComandaStatus.Fechada &&
                i.Comanda.ClosedAt.HasValue &&
                i.Comanda.ClosedAt.Value >= inicio &&
                i.Comanda.ClosedAt.Value < fim)
            .ToListAsync();

        // ── 2. VendasAvulsas no mês (MongoDB) ────────────────────────────────
        var filter = Builders<VendaAvulsa>.Filter.And(
            Builders<VendaAvulsa>.Filter.Gte(v => v.SoldAt, inicio),
            Builders<VendaAvulsa>.Filter.Lt(v => v.SoldAt, fim));
        var vendasAvulsas = await _vendas.Find(filter).ToListAsync();

        // ── 3. Acumula em dicionário categoria → produto → (qty, total) ───────
        // Estrutura: dict[categoria][produto] = (qty, totalCentavos)
        var dict = new Dictionary<string, Dictionary<string, (int qty, long totalCents)>>(StringComparer.OrdinalIgnoreCase);

        void Acumular(string cat, string nome, int qty, int unitCents)
        {
            if (!dict.TryGetValue(cat, out var prods))
            {
                prods = new Dictionary<string, (int, long)>(StringComparer.OrdinalIgnoreCase);
                dict[cat] = prods;
            }
            var subtotal = (long)qty * unitCents;
            if (prods.TryGetValue(nome, out var cur))
                prods[nome] = (cur.qty + qty, cur.totalCents + subtotal);
            else
                prods[nome] = (qty, subtotal);
        }

        // Comandas
        foreach (var item in comandaItems)
        {
            var cat = item.ProductId.HasValue
                ? (item.Product?.Category ?? "Outros")
                : (item.CardCacheId != null ? "Cartas TCG" : "Outros");

            Acumular(cat, item.ItemNameSnapshot, item.Quantity, item.UnitPriceInCents);
        }

        // Vendas avulsas (MongoDB)
        foreach (var venda in vendasAvulsas)
        foreach (var item in venda.Items)
        {
            var cat = string.IsNullOrWhiteSpace(item.ProductCategory)
                ? "Outros"
                : item.ProductCategory;

            Acumular(cat, item.ProductName, item.Quantity, item.UnitPriceInCents);
        }

        // ── 4. Projeta em DTO ─────────────────────────────────────────────────
        var porCategoria = dict
            .Select(kv =>
            {
                var produtos = kv.Value
                    .Select(p => new RelatorioProduto
                    {
                        Nome              = p.Key,
                        QuantidadeVendida = p.Value.qty,
                        TotalEmReais      = p.Value.totalCents / 100m,
                    })
                    .OrderByDescending(p => p.QuantidadeVendida)
                    .ToList();

                return new RelatorioCategoria
                {
                    Categoria         = kv.Key,
                    Emoji             = categorias.GetValueOrDefault(kv.Key, "📦"),
                    QuantidadeVendida = produtos.Sum(p => p.QuantidadeVendida),
                    TotalEmReais      = produtos.Sum(p => p.TotalEmReais),
                    Produtos          = produtos,
                };
            })
            .OrderByDescending(c => c.QuantidadeVendida)
            .ToList();

        return Ok(new RelatorioVendasDto
        {
            Mes                = mes,
            Ano                = ano,
            TotalGeralEmReais  = porCategoria.Sum(c => c.TotalEmReais),
            TotalItensVendidos = porCategoria.Sum(c => c.QuantidadeVendida),
            PorCategoria       = porCategoria,
        });
    }
}
