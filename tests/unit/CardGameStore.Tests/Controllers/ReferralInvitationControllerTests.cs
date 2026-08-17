using System.Security.Cryptography;
using System.Text;
using CardGameStore.Controllers;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace CardGameStore.Tests.Controllers;

public sealed class ReferralInvitationControllerTests
{
    [Fact]
    public async Task ConfirmSignature_ValidaEmailCriaParceiroEGeraPdfComEvidencias()
    {
        const string token = "convite-publico-de-teste";
        var schema = TestDbFactory.IsolatedSchemaName(nameof(ConfirmSignature_ValidaEmailCriaParceiroEGeraPdfComEvidencias));
        TestDbFactory.ResetSchema(schema);
        var connection = new NpgsqlConnectionStringBuilder(TestDbFactory.ConnectionString) { SearchPath = schema }.ConnectionString;
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options;

        try
        {
            await using var db = new CatalogDbContext(options);
            db.GetInfrastructure().GetRequiredService<IRelationalDatabaseCreator>().CreateTables();
            var invitation = new ReferralPartnerInvitation
            {
                TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
                Name = "Maria Indicadora", Email = "maria@example.com", PartnerKind = "Parceiro de indicação",
                SetupCommissionPercent = 30m, MonthlyCommissionPercent = 5m, PaymentGraceDays = 5,
                ContractVersion = "2026-08-17-v2", ContractText = "Regulamento de teste com acentuação.",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            };
            db.ReferralPartnerInvitations.Add(invitation);
            await db.SaveChangesAsync();

            string? deliveredCode = null;
            var email = new Mock<IEmailService>();
            email.Setup(e => e.SendReferralSignatureCodeAsync("maria@example.com", "Maria Indicadora", It.IsAny<string>(), It.IsAny<DateTime>()))
                .Callback<string, string, string, DateTime>((_, _, code, _) => deliveredCode = code)
                .Returns(Task.CompletedTask);
            var controller = new ReferralInvitationController(db,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:IpHashSalt"] = "salt-exclusivo-do-teste",
                }).Build(), email.Object, new ReferralContractPdfService())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { Connection = { RemoteIpAddress = System.Net.IPAddress.Loopback } },
                },
            };
            controller.Request.Headers.UserAgent = "Playwright/Test";

            var requested = await controller.RequestSignature(token, new AcceptReferralInvitationRequest
            {
                Name = "Maria Indicadora", Email = "maria@example.com", Document = "123.456.789-01",
                Phone = "(11) 99999-9999", PersonType = "PF", AcceptedTerms = true,
            });
            requested.Should().BeOfType<OkObjectResult>();
            deliveredCode.Should().MatchRegex("^[0-9]{6}$");
            (await db.ReferralPartners.CountAsync()).Should().Be(0, "o parceiro não pode ficar ativo antes de confirmar o e-mail");

            var wrongCode = deliveredCode == "000000" ? "999999" : "000000";
            var rejected = await controller.ConfirmSignature(token, new ConfirmReferralSignatureRequest { Code = wrongCode });
            rejected.Should().BeOfType<BadRequestObjectResult>();
            (await db.ReferralPartners.CountAsync()).Should().Be(0, "um código incorreto não pode assinar o documento");

            var confirmed = await controller.ConfirmSignature(token, new ConfirmReferralSignatureRequest { Code = deliveredCode! });
            confirmed.Should().BeOfType<OkObjectResult>();
            var partner = await db.ReferralPartners.SingleAsync();
            partner.Document.Should().Be("12345678901");
            partner.ContractEmailVerifiedAt.Should().NotBeNull();
            partner.ContractEvidenceId.Should().NotBeNullOrWhiteSpace();
            partner.ContractEvidenceSha256.Should().HaveLength(64);
            partner.ContractPdf.Should().StartWith(Encoding.ASCII.GetBytes("%PDF-1.4"));
            partner.ContractPdfSha256.Should().Be(Convert.ToHexString(SHA256.HashData(partner.ContractPdf!)));

            await db.Entry(invitation).ReloadAsync();
            invitation.AcceptedPartnerId.Should().Be(partner.Id);
            invitation.SignatureCodeHash.Should().BeNull();
            invitation.PendingAcceptanceJson.Should().BeNull();

            var download = await controller.DownloadSignedDocument(token);
            download.Should().BeOfType<FileContentResult>();
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
