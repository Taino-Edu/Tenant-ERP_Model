// =============================================================================
// RelatoriosDtos.cs — DTOs para relatórios de vendas por categoria/produto
// =============================================================================

namespace CardGameStore.DTOs;

public class RelatorioVendasDto
{
    public int     Mes                { get; set; }
    public int     Ano                { get; set; }
    public decimal TotalGeralEmReais  { get; set; }
    public int     TotalItensVendidos { get; set; }
    public List<RelatorioCategoria> PorCategoria { get; set; } = new();
}

public class RelatorioCategoria
{
    public string Categoria          { get; set; } = string.Empty;
    public string Emoji              { get; set; } = "📦";
    public int    QuantidadeVendida  { get; set; }
    public decimal TotalEmReais      { get; set; }
    public List<RelatorioProduto> Produtos { get; set; } = new();
}

public class RelatorioProduto
{
    public string  Nome              { get; set; } = string.Empty;
    public int     QuantidadeVendida { get; set; }
    public decimal TotalEmReais      { get; set; }
}
