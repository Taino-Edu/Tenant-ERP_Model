// =============================================================================
// AssinaturaControllerTests.cs — A loja vendo a própria assinatura.
//
// O que se protege aqui é, em ordem de gravidade: uma loja enxergar a fatura de
// outra, o documento de faturamento mudar sem descartar o cliente antigo no
// gateway (fatura sairia no CNPJ errado), e documento inválido ser aceito — que
// foi o que fez a primeira cobrança real falhar em silêncio dentro de um job.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using CardGameStore.Controllers;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CardGameStore.Tests.Controllers;

public class AssinaturaControllerTests
{
    private static CatalogDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AssinaturaController CreateController(CatalogDbContext db, Guid tenantId)
    {
        var contexto = new Mock<ITenantContext>();
        contexto.SetupGet(t => t.TenantId).Returns(tenantId);

        return new AssinaturaController(db, contexto.Object, NullLogger<AssinaturaController>.Instance);
    }

    /// <summary>O controller devolve Ok(dto), então o objeto vai em .Result e
    /// não em .Value — desembrulhar aqui evita repetir o cast em cada teste.</summary>
    private static AssinaturaDto Dto(ActionResult<AssinaturaDto> resposta) =>
        (AssinaturaDto)((OkObjectResult)resposta.Result!).Value!;

    private static Tenant NovoTenant(string slug, decimal mensalidade = 269m) => new()
    {
        Slug         = slug,
        SchemaName   = "tenant_" + slug.Replace('-', '_'),
        PlanName     = "Rio",
        MonthlyPrice = mensalidade,
        Status       = TenantStatus.Active,
    };

    private static TenantCharge NovaCobranca(Guid tenantId, decimal valor, string? link = null) => new()
    {
        TenantId       = tenantId,
        Kind           = TenantChargeKind.Mensalidade,
        Amount         = valor,
        ReferenceMonth = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        DueDate        = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
        PaymentUrl     = link,
    };

    // ── Isolamento entre lojas ───────────────────────────────────────────────

    [Fact]
    public async Task Obter_NaoDevolveFaturaDeOutraLoja()
    {
        // A gravidade aqui é máxima: fatura carrega valor negociado, e o preço
        // de cada loja é confidencial em relação às outras.
        using var db = CreateDb();
        var minha  = NovoTenant("minha-loja");
        var outra  = NovoTenant("outra-loja", 487m);
        db.Tenants.AddRange(minha, outra);
        db.TenantCharges.Add(NovaCobranca(minha.Id, 269m));
        db.TenantCharges.Add(NovaCobranca(outra.Id, 487m));
        await db.SaveChangesAsync();

        var resposta = await CreateController(db, minha.Id).Obter(default);

        var dto = Dto(resposta);
        dto.Faturas.Should().ContainSingle();
        dto.Faturas[0].Valor.Should().Be(269m);
        dto.Mensalidade.Should().Be(269m);
    }

    [Fact]
    public async Task Obter_MostraPlanoSituacaoELinkDePagamento()
    {
        using var db = CreateDb();
        var tenant = NovoTenant("minha-loja");
        tenant.BillingCnpj  = "68381935000107";
        tenant.BillingEmail = "financeiro@loja.com";
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(NovaCobranca(tenant.Id, 269m, link: "https://asaas/fatura/1"));
        await db.SaveChangesAsync();

        var dto = Dto(await CreateController(db, tenant.Id).Obter(default));

        dto.Plano.Should().Be("Rio");
        dto.Situacao.Should().Be("Ativa");
        dto.DadosCompletos.Should().BeTrue();
        dto.Faturas[0].LinkDePagamento.Should().Be("https://asaas/fatura/1");
        dto.Faturas[0].Vencida.Should().BeTrue("venceu em 10/08/2026 e não foi paga");
    }

    [Fact]
    public async Task Obter_SemDadosDeFaturamento_MarcaComoIncompleto()
    {
        // É esse flag que faz o frontend avisar. Sem ele o lojista só descobre
        // que faltava algo quando a mensalidade vence sem nunca ter chegado.
        using var db = CreateDb();
        var tenant = NovoTenant("minha-loja");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var dto = Dto(await CreateController(db, tenant.Id).Obter(default));

        dto.DadosCompletos.Should().BeFalse();
    }

    // ── Troca de documento ───────────────────────────────────────────────────

    [Fact]
    public async Task AtualizarFaturamento_TrocandoODocumento_DescartaOClienteAntigo()
    {
        // Sem isso, a próxima cobrança sairia no cliente do CNPJ velho e o
        // lojista receberia fatura em nome de outra empresa.
        using var db = CreateDb();
        var tenant = NovoTenant("minha-loja");
        tenant.BillingCnpj       = "68381935000107";
        tenant.BillingCustomerId = "cus_antigo";
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await CreateController(db, tenant.Id).AtualizarFaturamento(
            new AtualizarFaturamentoRequest { Documento = "11222333000181", Email = "novo@loja.com" },
            default);

        var atualizado = await db.Tenants.FirstAsync();
        atualizado.BillingCnpj.Should().Be("11222333000181");
        atualizado.BillingCustomerId.Should().BeNull();
    }

    [Fact]
    public async Task AtualizarFaturamento_MesmoDocumento_PreservaOCliente()
    {
        // Corrigir só o e-mail não pode custar um cliente novo no gateway.
        using var db = CreateDb();
        var tenant = NovoTenant("minha-loja");
        tenant.BillingCnpj       = "68381935000107";
        tenant.BillingCustomerId = "cus_existente";
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await CreateController(db, tenant.Id).AtualizarFaturamento(
            new AtualizarFaturamentoRequest { Documento = "68.381.935/0001-07", Email = "outro@loja.com" },
            default);

        var atualizado = await db.Tenants.FirstAsync();
        atualizado.BillingCustomerId.Should().Be("cus_existente",
            "o documento é o mesmo, só estava formatado");
        atualizado.BillingEmail.Should().Be("outro@loja.com");
    }

    [Fact]
    public async Task AtualizarFaturamento_GravaSomenteDigitos()
    {
        using var db = CreateDb();
        var tenant = NovoTenant("minha-loja");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await CreateController(db, tenant.Id).AtualizarFaturamento(
            new AtualizarFaturamentoRequest { Documento = "68.381.935/0001-07", Email = "a@b.com" },
            default);

        (await db.Tenants.FirstAsync()).BillingCnpj.Should().Be("68381935000107");
    }
}

// =============================================================================

public class CnpjValidAttributeTests
{
    [Theory]
    [InlineData("68381935000107")]      // real, dígitos conferidos à mão
    [InlineData("11222333000181")]
    [InlineData("68.381.935/0001-07")]  // formatado
    public void CnpjValido_Aceita(string cnpj) =>
        CnpjValidAttribute.ValidarCpfOuCnpj(cnpj).Should().BeTrue();

    [Theory]
    [InlineData("12345678000190")]  // o CNPJ "de exemplo" que circula: DV não fecha
    [InlineData("68381935000108")]  // último dígito trocado
    [InlineData("00000000000000")]  // passa no módulo 11, mas não existe
    [InlineData("11111111111111")]
    [InlineData("683819350001")]    // curto
    [InlineData("SEU_CNPJ")]        // placeholder colado por engano
    [InlineData("")]
    public void CnpjInvalido_Recusa(string cnpj) =>
        CnpjValidAttribute.ValidarCpfOuCnpj(cnpj).Should().BeFalse();

    [Fact]
    public void AceitaCpf_PorqueMeiEAutonomoFaturamNele() =>
        CnpjValidAttribute.ValidarCpfOuCnpj("52998224725").Should().BeTrue();

    [Fact]
    public void RequestComDocumentoInvalido_NaoPassaNaValidacaoDoModelo()
    {
        var request = new AtualizarFaturamentoRequest { Documento = "12345678000190", Email = "a@b.com" };
        var resultados = new List<ValidationResult>();

        Validator.TryValidateObject(request, new ValidationContext(request), resultados, true)
            .Should().BeFalse();

        resultados.Should().Contain(r => r.ErrorMessage!.Contains("CNPJ"));
    }
}
