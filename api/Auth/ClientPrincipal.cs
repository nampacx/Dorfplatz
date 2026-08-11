using System.Text.Json.Serialization;

namespace BulletinBoard.Api.Auth;

/// <summary>
/// The identity Static Web Apps injects into the linked backend via the
/// <c>x-ms-client-principal</c> header (base64-encoded JSON).
/// </summary>
public class ClientPrincipal
{
    [JsonPropertyName("identityProvider")]
    public string? IdentityProvider { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("userDetails")]
    public string? UserDetails { get; set; }

    [JsonPropertyName("userRoles")]
    public string[] UserRoles { get; set; } = Array.Empty<string>();

    [JsonPropertyName("claims")]
    public List<ClientPrincipalClaim>? Claims { get; set; }
}

public class ClientPrincipalClaim
{
    [JsonPropertyName("typ")]
    public string? Type { get; set; }

    [JsonPropertyName("val")]
    public string? Value { get; set; }
}
