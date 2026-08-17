using System.Security.Cryptography;
using System.Text;
using CardGameStore.Controllers;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CardGameStore.Tests.Controllers;

public sealed class ReferralInvitationControllerTests
{
    [Fact]
    public async Task Accept_ConsomeConviteCriaParceiroERegistraAceite()
    {
        const string token = "convite-publico-de-teste";
        var schema = TestDbFactory.IsolatedSchemaName(nameof(Accept_ConsomeConviteCriaParceiroERegistraAceite));
        TestDbFactory.ResetSchema(schema);

        var connection = new NpgsqlConnectionStringBuilder(TestDbFactory.ConnectionString)
        {
            SearchPath = schema,
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options;

        try
        {
            await using var db = new CatalogDbContext(options);
            db.GetInfrastructure().GetRequiredService<IRelationalDatabaseCreator>().CreateTables();
            var invitation = new ReferralPartnerInvitation
            {
                TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
                Name = "Maria Vendedora",
                Email = "maria@example.com",
                PartnerKind = "Vendedor",
                SetupCommissionPercent = 30m,
                MonthlyCommissionPercent = 5m,
                PaymentGraceDays = 5,
                ContractVersion = "2026-08",
                ContractText = "Regulamento de teste",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            };
            db.ReferralPartnerInvitations.Add(invitation);
            await db.SaveChangesAsync();

            var controller = new ReferralInvitationController(db,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:IpHashSalt"] = "salt-exclusivo-do-teste",
                }).Build())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        Connection = { RemoteIpAddress = System.Net.IPAddress.Loopback },
                    },
                },
            };
            controller.Request.Headers.UserAgent = "Playwright/Test";

            var result = await controller.Accept(token, new AcceptReferralInvitationRequest
            {
                Name = "Maria Vendedora",
                Email = "maria@example.com",
                Document = "123.456.789-01",
                Phone = "(11) 99999-9999",
                PersonType = "PF",
                AcceptedTerms = true,
            });

            result.Should().BeOfType<OkObjectResult>();
            var partner = await db.ReferralPartners.SingleAsync();
            partner.Document.Should().Be("12345678901");
            partner.ContractAcceptedAt.Should().NotBeNull();
            partner.ContractText.Should().Be("Regulamento de teste");

            await db.Entry(invitation).ReloadAsync();
            invitation.AcceptedAt.Should().NotBeNull();
            invitation.AcceptedPartnerId.Should().Be(partner.Id);
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(TestDbFactory.ConnectionString);
            await cleanup.OpenAsync();
            await using var command = cleanup.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
    }
}
