// =============================================================================
// ErpTools.cs — Camada de ferramentas do ERP exposta via MCP (Model Context
// Protocol), para o tenant plugar a IA dele (Claude, ChatGPT, n8n...) no
// próprio ERP.
//
// POR QUE ESTA CAMADA EXISTE SEPARADA: a mesma lista de operações precisa ser
// consumida por dois canais diferentes — o MCP (IA do cliente) e, futuramente,
// o bot de WhatsApp. Implementar duas vezes garante que as duas versões
// divergem com o tempo (foi exatamente o que aconteceu com as duas cópias do
// manual do usuário). Um lugar só, dois transportes.
//
// ISOLAMENTO DE TENANT: nenhuma tool recebe tenant como parâmetro, de
// propósito. O AppDbContext injetado aqui já vem com o schema do tenant
// resolvido pelo TenantResolutionMiddleware (via ITenantContext +
// TenantConnectionInterceptor, que roda "SET search_path" ao abrir a conexão).
// Um parâmetro de tenant seria uma porta pra IA de um cliente ler dados de
// outro só mudando o argumento.
//
// SOMENTE LEITURA nesta versão: toda tool aqui consulta, nenhuma escreve. Dar
// poder de escrita (abrir comanda, baixar estoque, registrar pagamento) para
// um modelo de linguagem de terceiros exige confirmação explícita e trilha de
// auditoria — fica pra uma etapa posterior, deliberadamente.
// =============================================================================

using System.ComponentModel;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace CardGameStore.Mcp;

// Classe não-estática (com métodos estáticos) porque WithTools<T> exige um
// argumento de tipo, e C# não aceita tipo estático nessa posição.
[McpServerToolType]
public sealed class ErpTools
{
    // Valores monetários são gravados em centavos (int) no banco inteiro — as
    // tools convertem na saída porque o consumidor é um modelo de linguagem,
    // que erra menos lendo "R$ 1.234,50" do que "123450".
    private static string Reais(long centavos) => (centavos / 100m).ToString("C", Cultura);
    private static string Reais(int centavos)  => Reais((long)centavos);

    private static readonly System.Globalization.CultureInfo Cultura = new("pt-BR");

    [McpServerTool(Name = "consultar_faturamento_hoje")]
    [Description("Retorna o faturamento do dia de hoje da loja, somando comandas fechadas e vendas avulsas (frente de caixa), com a quantidade de cada uma.")]
    public static async Task<string> ConsultarFaturamentoHoje(
        AppDbContext db,
        IVendaAvulsaService vendas,
        CancellationToken ct = default)
    {
        var hoje = DateTime.UtcNow.Date;

        // (long) no Sum: o provider SQLite usado em dev não agrega decimal —
        // só o Postgres traduz. Mantém a mesma escolha do GeminiChatService.
        var totalComandas = await db.Comandas
            .Where(c => c.ClosedAt >= hoje && c.Status == ComandaStatus.Fechada)
            .SumAsync(c => (long)c.TotalInCents, ct);

        var qtdComandas = await db.Comandas
            .CountAsync(c => c.ClosedAt >= hoje && c.Status == ComandaStatus.Fechada, ct);

        var avulsas = (await vendas.GetRecentAsync(200))
            .Where(v => v.SoldAt >= hoje)
            .ToList();

        var totalAvulsas = avulsas.Sum(v => (long)v.TotalInCents);

        return $"""
            Faturamento de hoje ({hoje:dd/MM/yyyy}):
            - Total geral: {Reais(totalComandas + totalAvulsas)}
            - Comandas fechadas: {Reais(totalComandas)} ({qtdComandas} comanda(s))
            - Vendas avulsas (PDV): {Reais(totalAvulsas)} ({avulsas.Count} venda(s))
            """;
    }

    [McpServerTool(Name = "consultar_comandas_abertas")]
    [Description("Lista as comandas que estão abertas ou em andamento agora, com cliente, valor acumulado e há quanto tempo foram abertas.")]
    public static async Task<string> ConsultarComandasAbertas(
        AppDbContext db,
        CancellationToken ct = default)
    {
        var abertas = await db.Comandas
            .Include(c => c.User)
            .Where(c => c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento)
            .OrderBy(c => c.OpenedAt)
            .Select(c => new { Cliente = c.User != null ? c.User.Name : "(sem cliente)", c.TotalInCents, c.OpenedAt, c.Status })
            .ToListAsync(ct);

        if (abertas.Count == 0)
            return "Nenhuma comanda aberta no momento.";

        var agora = DateTime.UtcNow;
        var linhas = abertas.Select(c =>
        {
            var horas = (agora - c.OpenedAt).TotalHours;
            var tempo = horas >= 24 ? $"{horas / 24:F0} dia(s)" : $"{horas:F0}h";
            return $"- {c.Cliente}: {Reais(c.TotalInCents)} · {c.Status} · aberta há {tempo}";
        });

        return $"{abertas.Count} comanda(s) aberta(s):\n{string.Join("\n", linhas)}";
    }

    [McpServerTool(Name = "consultar_estoque_baixo")]
    [Description("Lista os produtos ativos cujo estoque está igual ou abaixo do estoque mínimo configurado, ordenados do mais crítico para o menos crítico. Use para saber o que precisa repor.")]
    public static async Task<string> ConsultarEstoqueBaixo(
        AppDbContext db,
        [Description("Quantidade máxima de produtos a retornar. Padrão 20.")] int limite = 20,
        CancellationToken ct = default)
    {
        limite = Math.Clamp(limite, 1, 100);

        var criticos = await db.Products
            .Where(p => p.IsActive && p.StockQuantity <= p.MinimumStock)
            .OrderBy(p => p.StockQuantity)
            .Take(limite)
            .Select(p => new { p.Name, p.Category, p.StockQuantity, p.MinimumStock })
            .ToListAsync(ct);

        if (criticos.Count == 0)
            return "Nenhum produto abaixo do estoque mínimo. Estoque saudável.";

        var linhas = criticos.Select(p =>
        {
            var alerta = p.StockQuantity == 0 ? " ⚠ ZERADO" : "";
            return $"- {p.Name} ({p.Category}): {p.StockQuantity} un., mínimo {p.MinimumStock}{alerta}";
        });

        return $"{criticos.Count} produto(s) precisando de reposição:\n{string.Join("\n", linhas)}";
    }

    [McpServerTool(Name = "buscar_produto")]
    [Description("Busca produtos ativos por nome, categoria ou código de barras. Retorna preço de venda, estoque e categoria de cada um.")]
    public static async Task<string> BuscarProduto(
        AppDbContext db,
        [Description("Texto a procurar no nome, categoria ou código de barras do produto.")] string termo,
        [Description("Quantidade máxima de resultados. Padrão 10.")] int limite = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(termo))
            return "Informe um termo de busca.";

        limite = Math.Clamp(limite, 1, 50);
        var t = termo.Trim();

        var achados = await db.Products
            .Where(p => p.IsActive && (
                EF.Functions.ILike(p.Name, $"%{t}%") ||
                EF.Functions.ILike(p.Category, $"%{t}%") ||
                (p.Barcode != null && p.Barcode == t)))
            .OrderBy(p => p.Name)
            .Take(limite)
            .Select(p => new { p.Name, p.Category, p.PriceInCents, p.StockQuantity })
            .ToListAsync(ct);

        if (achados.Count == 0)
            return $"Nenhum produto ativo encontrado para '{t}'.";

        var linhas = achados.Select(p =>
            $"- {p.Name} ({p.Category}): {Reais(p.PriceInCents)} · {p.StockQuantity} em estoque");

        return $"{achados.Count} produto(s) para '{t}':\n{string.Join("\n", linhas)}";
    }

    [McpServerTool(Name = "consultar_crediarios_abertos")]
    [Description("Lista os crediários (fiado) em aberto, com cliente, valor devido e vencimento, destacando os que já venceram. Use para saber quem cobrar.")]
    public static async Task<string> ConsultarCrediariosAbertos(
        AppDbContext db,
        CancellationToken ct = default)
    {
        var abertos = await db.Crediarios
            .Include(c => c.User)
            .Where(c => c.Status == CrediariosStatus.Aberto)
            .OrderBy(c => c.DataVencimento)
            .Select(c => new { Cliente = c.User.Name, c.ValorEmCentavos, c.DataVencimento })
            .ToListAsync(ct);

        if (abertos.Count == 0)
            return "Nenhum crediário em aberto.";

        var hoje    = DateTime.UtcNow.Date;
        var total   = abertos.Sum(c => (long)c.ValorEmCentavos);
        var vencidos = abertos.Count(c => c.DataVencimento.Date < hoje);

        var linhas = abertos.Select(c =>
        {
            var venceu = c.DataVencimento.Date < hoje;
            var marca  = venceu ? $" ⚠ VENCIDO há {(hoje - c.DataVencimento.Date).Days} dia(s)" : "";
            return $"- {c.Cliente}: {Reais(c.ValorEmCentavos)} · vence {c.DataVencimento:dd/MM/yyyy}{marca}";
        });

        return $"""
            {abertos.Count} crediário(s) em aberto, somando {Reais(total)} ({vencidos} vencido(s)):
            {string.Join("\n", linhas)}
            """;
    }

    [McpServerTool(Name = "consultar_produtos_mais_vendidos")]
    [Description("Lista os produtos mais vendidos em comandas num período recente, por quantidade. Use para entender o que gira na loja.")]
    public static async Task<string> ConsultarProdutosMaisVendidos(
        AppDbContext db,
        [Description("Quantos dias para trás considerar. Padrão 30.")] int dias = 30,
        [Description("Quantidade de produtos a retornar. Padrão 10.")] int limite = 10,
        CancellationToken ct = default)
    {
        dias   = Math.Clamp(dias, 1, 365);
        limite = Math.Clamp(limite, 1, 50);
        var desde = DateTime.UtcNow.Date.AddDays(-dias);

        var top = await db.ComandaItems
            .Where(i => i.AddedAt >= desde)
            .GroupBy(i => i.ItemNameSnapshot)
            .Select(g => new { Nome = g.Key, Qtd = g.Sum(i => i.Quantity) })
            .OrderByDescending(x => x.Qtd)
            .Take(limite)
            .ToListAsync(ct);

        if (top.Count == 0)
            return $"Nenhum item vendido em comandas nos últimos {dias} dias.";

        var linhas = top.Select((p, i) => $"{i + 1}. {p.Nome}: {p.Qtd} un.");
        return $"Mais vendidos nos últimos {dias} dias:\n{string.Join("\n", linhas)}";
    }
}
