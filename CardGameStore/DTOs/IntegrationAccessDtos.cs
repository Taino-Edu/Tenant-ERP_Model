using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CardGameStore.DTOs;

public sealed record CreateIntegrationClientRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MinLength(1)] string[] Scopes);

public sealed record IntegrationClientDto(
    Guid Id,
    string Name,
    string ClientId,
    string[] Scopes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastUsedAt);

public sealed record IntegrationClientCreatedDto(
    Guid Id,
    string Name,
    string ClientId,
    string ClientSecret,
    string[] Scopes,
    DateTime CreatedAt);

public sealed record IntegrationTokenRequest(
    [property: JsonPropertyName("grant_type")]
    [Required] string GrantType,
    [property: JsonPropertyName("client_id")]
    [Required] string ClientId,
    [property: JsonPropertyName("client_secret")]
    [Required] string ClientSecret);

public sealed record IntegrationTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope);
