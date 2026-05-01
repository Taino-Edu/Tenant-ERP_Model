using Microsoft.EntityFrameworkCore;

namespace VendasMTG.Api.Data;

public class VendasDbContext : DbContext
{
    public VendasDbContext(DbContextOptions<VendasDbContext> options) : base(options)
    {
    }

    // O pessoal da sua equipe vai colocar as tabelas aqui depois!
    // Ex: public DbSet<Comanda> Comandas { get; set; }
}