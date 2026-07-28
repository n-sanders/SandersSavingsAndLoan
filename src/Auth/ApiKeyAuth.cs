using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SandersSavingsAndLoan.Data;

namespace SandersSavingsAndLoan;

public static partial class ApiKeyAuth
{
    public const string HeaderName = "X-Api-Key";
    public const string HttpContextItemKey = "IntegrationApiKey";

    private static readonly Regex SourceSlugPattern = SourceSlugRegex();

    public static string GenerateRawKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return "ssl_" + Base64UrlEncode(bytes);
    }

    public static string HashKey(string rawKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string KeyPrefix(string rawKey) =>
        rawKey.Length <= 12 ? rawKey : rawKey[..12];

    public static bool IsValidSourceSlug(string source) =>
        SourceSlugPattern.IsMatch(source);

    public static async Task<IntegrationApiKey?> FindActiveKeyAsync(AppDbContext db, string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            return null;

        var hash = HashKey(rawKey.Trim());
        return await db.IntegrationApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAt == null);
    }

    public static async Task<IntegrationApiKey?> RequireApiKeyAsync(HttpContext http, AppDbContext db)
    {
        if (!http.Request.Headers.TryGetValue(HeaderName, out var values))
            return null;

        var key = await FindActiveKeyAsync(db, values.ToString());
        if (key is not null)
            http.Items[HttpContextItemKey] = key;

        return key;
    }

    public static IntegrationApiKey? GetAuthenticatedKey(HttpContext http) =>
        http.Items.TryGetValue(HttpContextItemKey, out var value) && value is IntegrationApiKey key
            ? key
            : null;

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    [GeneratedRegex("^[a-z][a-z0-9_-]{1,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceSlugRegex();
}
