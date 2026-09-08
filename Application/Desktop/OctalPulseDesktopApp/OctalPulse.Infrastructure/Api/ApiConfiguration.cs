namespace OctalPulse.Infrastructure.Api;

/// <summary>
/// Static holder for the API base URL so UI layers (e.g. converters that build
/// image sources from server-relative paths) can resolve full URLs without DI.
/// </summary>
public static class ApiConfiguration
{
    public static string BaseUrl { get; set; } = string.Empty;

    public static string? ResolveUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var baseUrl = BaseUrl.TrimEnd('/');
        return $"{baseUrl}/{url.TrimStart('/')}";
    }
}