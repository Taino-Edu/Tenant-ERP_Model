// =============================================================================
// AnalyticsDtos.cs — DTOs para o módulo de analytics do dashboard admin
// =============================================================================

namespace CardGameStore.DTOs;

public class DashboardAnalyticsDto
{
    // KPIs do dia
    public decimal VendasHoje          { get; set; }
    public decimal VendasOntem         { get; set; }
    public decimal VariacaoPercDia     { get; set; }  // ex: +12.5 ou -5.0
    public int     ComandasAbertas     { get; set; }
    public int     VendasAvulsasHoje   { get; set; }

    // Ticket médio (últimos 30 dias)
    public decimal TicketMedio         { get; set; }
    public decimal TicketMedioAnterior { get; set; }

    // Clientes
    public int TotalClientes           { get; set; }
    public int ClientesAtivos30Dias    { get; set; }
    public int ClientesInativos30Dias  { get; set; }
    public int NovosClientesMes        { get; set; }

    // Curva de vendas do dia (por hora)
    public List<HourlyRevenueDto> CurvaVendasDia { get; set; } = new();

    // Top produtos vendidos (últimos 30 dias)
    public List<TopProductDto> TopProdutos { get; set; } = new();

    // Forma de pagamento (últimas vendas avulsas com pagamento registrado)
    public int PagamentosPix      { get; set; }
    public int PagamentosCartao   { get; set; }
    public int PagamentosDinheiro { get; set; }
}

public class HourlyRevenueDto
{
    public string Hora    { get; set; } = string.Empty; // ex: "14h"
    public decimal Valor  { get; set; }
}

public class TopProductDto
{
    public string Nome         { get; set; } = string.Empty;
    public int    QuantVendida { get; set; }
    public decimal Receita     { get; set; }
}

public class ClienteInsightDto
{
    public Guid    UserId       { get; set; }
    public string  Nome         { get; set; } = string.Empty;
    public string? Email        { get; set; }
    public decimal GastoTotal   { get; set; }
    public decimal TicketMedio  { get; set; }
    public int     NumVisitas   { get; set; }
    public DateTime? UltimaVisita { get; set; }
    public bool    Inativo30    { get; set; }  // sem visita nos últimos 30 dias
    public int     Pontos       { get; set; }
}
