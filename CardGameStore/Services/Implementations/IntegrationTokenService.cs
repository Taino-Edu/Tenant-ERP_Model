using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CardGameStore.Configuration;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CardGameStore.Services.Implementations;

public sealed class IntegrationTokenService
{
    private readonly CatalogDbContext _catalog;
    private readonly JwtSettings _jwt;

    public IntegrationTokenService(CatalogDbContext catalog, IOptions<JwtSettings> jwt)
    {
        _catalog = catalog;
        _jwt = jwt.Value;
    }

    public async Task<(ApiIntegrationClient Client, string Secret)> CreateAsync(
        Guid tenantId, string name, IEnumerable<string> scopes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Informe um nome para a integracao.");
        var normalizedScopes = ValidateScopes(scopes);
        var secret = GenerateSecret();
        var client = new ApiIntegrationClient
        {
            TenantId = tenantId,
            Name = name.Trim(),
            ClientId = $"ti_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}",
            SecretHash = BCrypt.Net.BCrypt.HashPassword(secret, workFactor: 12),
            Scopes = normalizedScopes,
        };
        _catalog.ApiIntegrationClients.Add(client);
        await _catalog.SaveChangesAsync(ct);
        return (client, secret);
    }

    public async Task<(ApiIntegrationClient Client, string Secret)> RotateAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var client = await _catalog.ApiIntegrationClients
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Integracao nao encontrada.");
        var secret = GenerateSecret();
        client.SecretHash = BCrypt.Net.BCrypt.HashPassword(secret, workFactor: 12);
        client.CredentialVersion++;
        client.IsActive = true;
        client.UpdatedAt = DateTime.UtcNow;
        await _catalog.SaveChangesAsync(ct);
        return (client, secret);
    }

    public async Task RevokeAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var client = await _catalog.ApiIntegrationClients
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Integracao nao encontrada.");
        client.IsActive = false;
        client.CredentialVersion++;
        client.UpdatedAt = DateTime.UtcNow;
        await _catalog.SaveChangesAsync(ct);
    }

    public async Task<IntegrationTokenResponse?> IssueAsync(
        Guid tenantId, IntegrationTokenRequest request, CancellationToken ct)
    {
        if (!string.Equals(request.GrantType, IntegrationClaim.TokenTypeValue, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
            return null;

        var client = await _catalog.ApiIntegrationClients
            .SingleOrDefaultAsync(item => item.ClientId == request.ClientId && item.TenantId == tenantId, ct);
        if (client is null || !client.IsActive || !VerifySecret(request.ClientSecret, client.SecretHash))
            return null;

        var now = DateTime.UtcNow;
        var lifetime = TimeSpan.FromMinutes(Math.Clamp(_jwt.IntegrationTokenExpirationMinutes, 5, 60));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, client.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, client.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, UserRole.Integration),
            new(TenantConstants.TenantIdClaimType, tenantId.ToString()),
            new(IntegrationClaim.TokenType, IntegrationClaim.TokenTypeValue),
            new(IntegrationClaim.ClientRecordId, client.Id.ToString()),
            new(IntegrationClaim.CredentialVersion, client.CredentialVersion.ToString()),
            new(IntegrationClaim.Scope, string.Join(' ', client.Scopes)),
            new("client_id", client.ClientId),
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: credentials);

        client.LastUsedAt = now;
        client.UpdatedAt = now;
        await _catalog.SaveChangesAsync(ct);

        return new IntegrationTokenResponse(
            new JwtSecurityTokenHandler().WriteToken(jwt),
            "Bearer",
            (int)lifetime.TotalSeconds,
            string.Join(' ', client.Scopes));
    }

    public static string[] ValidateScopes(IEnumerable<string> scopes)
    {
        if (scopes is null)
            throw new ArgumentException("Informe somente escopos de integracao reconhecidos.");
        var normalized = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0 || normalized.Any(scope => !IntegrationScope.All.Contains(scope)))
            throw new ArgumentException("Informe somente escopos de integracao reconhecidos.");
        return normalized;
    }

    private static string GenerateSecret() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static bool VerifySecret(string secret, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(secret, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; }
    }
}
