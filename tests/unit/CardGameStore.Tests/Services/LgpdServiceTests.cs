// =============================================================================
// LgpdServiceTests.cs — Testes unitários dos métodos LGPD do UserService
// Cobre: UpdateMeAsync e AnonimizarAsync
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Services;

public class LgpdServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static UserService CreateService(AppDbContext db) =>
        new(db, NullLogger<UserService>.Instance);

    private static User MakeCustomer(
        string name     = "João",
        string email    = "joao@test.com",
        string? cpf     = "12345678901",
        string? whatsApp = "11999990001") =>
        new()
        {
            Id           = Guid.NewGuid(),
            Name         = name,
            Email        = email,
            Cpf          = cpf,
            WhatsApp     = whatsApp,
            PasswordHash = "hash",
            Role         = UserRole.Customer,
            IsActive     = true,
        };

    // ── UpdateMeAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMe_AlteraApenasCamposInformados()
    {
        // Arrange
        var db      = CreateDb(nameof(UpdateMe_AlteraApenasCamposInformados));
        var service = CreateService(db);
        var user    = MakeCustomer(name: "João", email: "joao@test.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act — fornece apenas Name; Email = null (não deve sobrescrever)
        var result = await service.UpdateMeAsync(user.Id, new UpdateMeRequest
        {
            Name  = "João Silva",
            Email = null,
        });

        // Assert
        result.Name.Should().Be("João Silva",
            "o nome deve ser atualizado para o valor fornecido");
        result.Email.Should().Be("joao@test.com",
            "o email não foi fornecido, portanto não deve ser alterado");
    }

    [Fact]
    public async Task UpdateMe_IgnoraCamposNulos()
    {
        // Arrange
        var db      = CreateDb(nameof(UpdateMe_IgnoraCamposNulos));
        var service = CreateService(db);
        var user    = MakeCustomer(name: "Maria", email: "maria@test.com", whatsApp: "11911112222");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act — todos os campos null (request vazio)
        var result = await service.UpdateMeAsync(user.Id, new UpdateMeRequest
        {
            Name     = null,
            Email    = null,
            WhatsApp = null,
        });

        // Assert — nada deve ter mudado
        result.Name.Should().Be("Maria");
        result.Email.Should().Be("maria@test.com");
        result.WhatsApp.Should().Be("11911112222");
    }

    [Fact]
    public async Task UpdateMe_WhatsApp_AtualizaCorretamente()
    {
        // Arrange
        var db      = CreateDb(nameof(UpdateMe_WhatsApp_AtualizaCorretamente));
        var service = CreateService(db);
        var user    = MakeCustomer(whatsApp: "11999990001");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await service.UpdateMeAsync(user.Id, new UpdateMeRequest
        {
            WhatsApp = "11988880002",
        });

        // Assert
        result.WhatsApp.Should().Be("11988880002");
    }

    [Fact]
    public async Task UpdateMe_UsuarioInexistente_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(UpdateMe_UsuarioInexistente_DeveLancarExcecao));
        var service = CreateService(db);

        var act = async () => await service.UpdateMeAsync(Guid.NewGuid(), new UpdateMeRequest
        {
            Name = "Qualquer Nome",
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    // ── AnonimizarAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task AnonimizarAsync_SubstituiTodosDadosPessoais()
    {
        // Arrange
        var db      = CreateDb(nameof(AnonimizarAsync_SubstituiTodosDadosPessoais));
        var service = CreateService(db);
        var user    = MakeCustomer(
            name:     "Pedro Alves",
            email:    "pedro@test.com",
            cpf:      "52998224725",
            whatsApp: "17999990000");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        await service.AnonimizarAsync(user.Id);

        // Assert
        var anonimizado = await db.Users.FindAsync(user.Id);
        anonimizado.Should().NotBeNull();
        anonimizado!.Name.Should().Be("Usuário Removido");
        anonimizado.Email.Should().BeNull();
        anonimizado.Cpf.Should().BeNull();
        anonimizado.WhatsApp.Should().BeNull();
        anonimizado.IsActive.Should().BeFalse();
        anonimizado.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AnonimizarAsync_InvalidaTokensDeRefresh()
    {
        // Arrange
        var db      = CreateDb(nameof(AnonimizarAsync_InvalidaTokensDeRefresh));
        var service = CreateService(db);
        var user    = MakeCustomer();
        user.RefreshToken       = "token-ativo-abc123";
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        await service.AnonimizarAsync(user.Id);

        // Assert — tokens de sessão devem ser invalidados
        var anonimizado = await db.Users.FindAsync(user.Id);
        anonimizado!.RefreshToken.Should().BeNull();
        anonimizado.RefreshTokenExpiry.Should().BeNull();
    }

    [Fact]
    public async Task AnonimizarAsync_UsuarioInexistente_DeveLancarExcecao()
    {
        // Arrange
        var db      = CreateDb(nameof(AnonimizarAsync_UsuarioInexistente_DeveLancarExcecao));
        var service = CreateService(db);

        // Act — ID inexistente: UserService lança InvalidOperationException
        var act = async () => await service.AnonimizarAsync(Guid.NewGuid());

        // Assert — conforme implementação atual do UserService
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task AnonimizarAsync_DeletedAt_EPreenchidoComDataAtual()
    {
        // Arrange
        var db      = CreateDb(nameof(AnonimizarAsync_DeletedAt_EPreenchidoComDataAtual));
        var service = CreateService(db);
        var user    = MakeCustomer();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var antes = DateTime.UtcNow;

        // Act
        await service.AnonimizarAsync(user.Id);

        var depois = DateTime.UtcNow;

        // Assert
        var anonimizado = await db.Users.FindAsync(user.Id);
        anonimizado!.DeletedAt.Should().NotBeNull();
        anonimizado.DeletedAt!.Value.Should().BeOnOrAfter(antes).And.BeOnOrBefore(depois);
    }
}
