using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace BulletinBoard.Api;

public static class JsonExtensions
{
    /// <summary>
    /// Web defaults = camelCase naming + case-insensitive matching, so request bodies
    /// sent by the SPA (camelCase) bind to our PascalCase records. Raw JsonSerializer
    /// defaults are case-sensitive and would leave every property null.
    /// </summary>
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static ValueTask<T?> ReadJsonAsync<T>(this HttpRequest req) =>
        JsonSerializer.DeserializeAsync<T>(req.Body, Web);
}
