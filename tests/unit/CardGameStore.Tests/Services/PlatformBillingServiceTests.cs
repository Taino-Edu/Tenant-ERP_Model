// =============================================================================
// PlatformBillingServiceTests.cs — Regras do billing da plataforma.
//
// Cobre o que custa dinheiro se quebrar: cobrar duas vezes, cobrar quem está no
// mês grátis, perder o vencimento em mês curto, e reajuste de plano reescrevendo
// cobrança antiga.
//
// Usa InMemory (mesmo padrão de TenantResolutionMiddlewareTests). Vale notar que
// o provider InMemory NÃO enforça unique index — então o teste de idempotência
// aqui prova a checagem explícita do serviço, não a trava do banco. A unique
// index (ix_tenant_charges_tenant_kind_competencia) é a segunda linha de defesa
// e está verificada na migration.
// =============================================================================

using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class PlatformBillingServiceTests
{
    private static CatalogDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PlatformBillingService CreateService(CatalogDbContext db) =>
        new(db, NullLogger<PlatformBillingService>.Instance);

    private static Tenant NovoTenant(
        decimal mensalidade = 269m,
        DateTime? cobrancaComecaEm = null,
        TenantStatus status = TenantStatus.Active,
        string slug = "loja-teste")
        => new()
        {
            Slug            = slug,
            SchemaName      = "tenant_" + slug.Replace('-', '_'),
            Status          = status,
            MonthlyPrice    = mensalidade,
            SetupFee        = mensalidade * 2,
            BillingStartsOn = cobrancaComecaEm ?? new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
        };

    private static DateTime Competencia(int ano, int mes) =>
        new(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── Idempotência ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GerarMensalidades_RodandoDuasVezes_NaoDuplicaCobranca()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant());
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var primeira = await service.GerarMensalidadesAsync(Competencia(2026, 3));
        var segunda  = await service.GerarMensalidadesAsync(Competencia(2026, 3));

        primeira.Criadas.Should().Be(1);
        segunda.Criadas.Should().Be(0);
        segunda.JaExistiam.Should().Be(1);

        (await db.TenantCharges.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GerarMensalidades_QualquerDiaDoMes_ContaComoMesmaCompetencia()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant());
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Mesma competência pedida com datas diferentes dentro do mês: sem a
        // normalização pro dia 1, viravam duas competências e a loja seria
        // cobrada duas vezes por março.
        await service.GerarMensalidadesAsync(new DateTime(2026, 3, 3,  0, 0, 0, DateTimeKind.Utc));
        await service.GerarMensalidadesAsync(new DateTime(2026, 3, 27, 0, 0, 0, DateTimeKind.Utc));

        (await db.TenantCharges.CountAsync()).Should().Be(1);
    }

    // ── Primeiro mês de acesso grátis ────────────────────────────────────────

    [Fact]
    public async Task GerarMensalidades_LojaAindaNoMesGratis_NaoEhCobrada()
    {
        using var db = CreateDb();
        // Assinou em março, então BillingStartsOn é abril (mês 1 de acesso grátis).
        db.Tenants.Add(NovoTenant(cobrancaComecaEm: Competencia(2026, 4)));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var resultado = await service.GerarMensalidadesAsync(Competencia(2026, 3));

        resultado.Criadas.Should().Be(0);
        resultado.ForaDeCobranca.Should().Be(1);
        (await db.TenantCharges.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task GerarMensalidades_NoMesEmQueACobrancaComeca_EhCobrada()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant(cobrancaComecaEm: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var resultado = await service.GerarMensalidadesAsync(Competencia(2026, 4));

        resultado.Criadas.Should().Be(1);
    }

    // ── Quem não entra na régua ──────────────────────────────────────────────

    [Fact]
    public async Task GerarMensalidades_LojaSuspensa_NaoEhCobrada()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant(status: TenantStatus.Suspended));
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).GerarMensalidadesAsync(Competencia(2026, 3));

        resultado.Criadas.Should().Be(0);
    }

    [Fact]
    public async Task GerarMensalidades_LojaSemMensalidade_NaoEhCobrada()
    {
        using var db = CreateDb();
        // Cortesia/piloto, ou plano fora da tabela que entrou com 0.
        db.Tenants.Add(NovoTenant(mensalidade: 0m));
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).GerarMensalidadesAsync(Competencia(2026, 3));

        resultado.Criadas.Should().Be(0);
        resultado.ForaDeCobranca.Should().Be(1);
    }

    // ── Vencimento em mês curto ──────────────────────────────────────────────

    [Fact]
    public async Task GerarMensalidades_DiaDeVencimento31EmFevereiro_CaiNoUltimoDia()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant(cobrancaComecaEm: new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        // 2026 não é bissexto — fevereiro tem 28 dias. Sem o clamp isso lançava
        // ArgumentOutOfRangeException e derrubava a geração do mês inteiro.
        var resultado = await CreateService(db).GerarMensalidadesAsync(Competencia(2026, 2));

        resultado.Criadas.Should().Be(1);
        var cobranca = await db.TenantCharges.SingleAsync();
        cobranca.DueDate.Should().Be(new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GerarMensalidades_DiaDeVencimentoQueExisteNoMes_EhPreservado()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant(cobrancaComecaEm: new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        await CreateService(db).GerarMensalidadesAsync(Competencia(2026, 5));

        var cobranca = await db.TenantCharges.SingleAsync();
        cobranca.DueDate.Day.Should().Be(15);
    }

    // ── Valor é foto do momento, não referência ──────────────────────────────

    [Fact]
    public async Task ReajusteDePlano_NaoReescreveCobrancaJaEmitida()
    {
        using var db = CreateDb();
        var tenant = NovoTenant(mensalidade: 269m);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GerarMensalidadesAsync(Competencia(2026, 3));

        // Reajuste depois da cobrança de março já emitida.
        tenant.MonthlyPrice = 487m;
        await db.SaveChangesAsync();

        var marco = await db.TenantCharges.SingleAsync(c => c.ReferenceMonth == Competencia(2026, 3));
        marco.Amount.Should().Be(269m, "cobrança emitida é registro financeiro, não muda retroativamente");

        // E o mês seguinte já sai no valor novo.
        await service.GerarMensalidadesAsync(Competencia(2026, 4));
        var abril = await db.TenantCharges.SingleAsync(c => c.ReferenceMonth == Competencia(2026, 4));
        abril.Amount.Should().Be(487m);
    }

    // ── Baixa de pagamento ───────────────────────────────────────────────────

    [Fact]
    public async Task DefinirPagamento_ComDataFutura_EhRejeitado()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant());
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GerarMensalidadesAsync(Competencia(2026, 3));
        var cobranca = await db.TenantCharges.SingleAsync();

        var acao = () => service.DefinirPagamentoAsync(cobranca.Id, DateTime.UtcNow.AddDays(1));

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não pode ser futura*");
    }

    [Fact]
    public async Task DefinirPagamento_ComNull_ReabreACobranca()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant());
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GerarMensalidadesAsync(Competencia(2026, 3));
        var id = (await db.TenantCharges.SingleAsync()).Id;

        await service.DefinirPagamentoAsync(id, DateTime.UtcNow.Date);
        (await db.TenantCharges.SingleAsync()).PaidAt.Should().NotBeNull();

        await service.DefinirPagamentoAsync(id, null);
        (await db.TenantCharges.SingleAsync()).PaidAt.Should().BeNull();
    }

    // Os dois testes de baixa acima passavam com o bug em produção porque ambos
    // entregam a data já como UTC (DateTime.UtcNow). A requisição real manda só
    // "2026-08-14" no corpo, que o System.Text.Json desserializa com
    // Kind=Unspecified — e o Npgsql RECUSA gravar isso numa coluna timestamptz,
    // então toda baixa respondia 500. O InMemory aceita qualquer Kind, então o
    // teste não pode esperar exceção: tem que afirmar o INVARIANTE que o
    // Postgres exige (PaidAt sempre em UTC).
    [Fact]
    public async Task DefinirPagamento_ComDataSemFuso_GravaEmUtc()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant());
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GerarMensalidadesAsync(Competencia(2026, 3));
        var id = (await db.TenantCharges.SingleAsync()).Id;

        // Exatamente o que chega de `{"pagoEm":"2026-03-10"}`.
        var semFuso = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Unspecified);

        await service.DefinirPagamentoAsync(id, semFuso);

        var pago = (await db.TenantCharges.SingleAsync()).PaidAt;
        pago.Should().NotBeNull();
        pago!.Value.Kind.Should().Be(DateTimeKind.Utc);
        pago.Value.Should().Be(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task DefinirPagamento_ComHora_TruncaParaODia()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant());
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GerarMensalidadesAsync(Competencia(2026, 3));
        var id = (await db.TenantCharges.SingleAsync()).Id;

        // Baixa é um fato do dia: guardar a hora do clique faria a mesma
        // cobrança "mudar de dia" quando lida de outro fuso.
        await service.DefinirPagamentoAsync(id, new DateTime(2026, 3, 10, 22, 47, 13, DateTimeKind.Utc));

        var pago = (await db.TenantCharges.SingleAsync()).PaidAt;
        pago!.Value.Should().Be(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc));
        pago.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ── Resumo ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resumo_VencidoAcumulado_IncluiCompetenciasAnteriores()
    {
        using var db = CreateDb();
        var tenant = NovoTenant();
        db.Tenants.Add(tenant);

        // Dívida velha, vencida e nunca paga.
        db.TenantCharges.Add(new TenantCharge
        {
            TenantId       = tenant.Id,
            Kind           = TenantChargeKind.Mensalidade,
            Amount         = 269m,
            ReferenceMonth = Competencia(2026, 1),
            DueDate        = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();

        // Resumo pedido pra OUTRA competência: a dívida de janeiro tem que
        // continuar aparecendo. Um resumo que só olhasse o mês corrente mostraria
        // inadimplência zero logo depois da virada — o oposto da verdade.
        var resumo = await CreateService(db).ObterResumoAsync(Competencia(2026, 5));

        resumo.VencidoAcumulado.Should().Be(269m);
        resumo.Faturado.Should().Be(0m, "não há cobrança emitida na competência de maio");
    }

    [Fact]
    public async Task Resumo_SeparaContratadoDeRecebido()
    {
        using var db = CreateDb();
        db.Tenants.Add(NovoTenant(mensalidade: 269m, slug: "loja-a"));
        db.Tenants.Add(NovoTenant(mensalidade: 120m, slug: "loja-b"));
        db.Tenants.Add(NovoTenant(mensalidade: 0m,   slug: "loja-cortesia"));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GerarMensalidadesAsync(Competencia(2026, 3));

        // Só uma das duas pagou.
        var umaCobranca = await db.TenantCharges.FirstAsync(c => c.Amount == 269m);
        await service.DefinirPagamentoAsync(umaCobranca.Id, DateTime.UtcNow.Date);

        var resumo = await service.ObterResumoAsync(Competencia(2026, 3));

        resumo.MrrContratado.Should().Be(389m, "269 + 120, a cortesia não soma");
        resumo.LojasPagantes.Should().Be(2);
        resumo.LojasSemCobranca.Should().Be(1);
        resumo.Faturado.Should().Be(389m);
        resumo.Recebido.Should().Be(269m);
        resumo.EmAberto.Should().Be(120m);
    }
}
