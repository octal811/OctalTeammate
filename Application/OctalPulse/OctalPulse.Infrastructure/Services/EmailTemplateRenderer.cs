using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OctalPulse.Infrastructure.Services;

public class EmailTemplateRenderer
{
    private static readonly Regex TokenRegex =
        new(@"\{\{\s*([A-Za-z0-9_.]+)\s*\}\}", RegexOptions.Compiled);

    private static readonly Regex IfBlockRegex =
        new(@"\{\{\s*#if\s+([A-Za-z0-9_.]+)\s*\}\}(.*?)\{\{\s*/if\s*\}\}", RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly Assembly _assembly;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public EmailTemplateRenderer(Assembly? assembly = null)
    {
        _assembly = assembly ?? typeof(EmailTemplateRenderer).Assembly;
    }

    public string Render(string templateName, IReadOnlyDictionary<string, string> values)
    {
        var source = GetTemplate(templateName);
        source = RenderConditionals(source, values);
        source = RenderTokens(source, values);
        return source;
    }

    private string GetTemplate(string templateName)
    {
        return _cache.GetOrAdd(
            templateName,
            name =>
            {
                var resourceName = _assembly
                    .GetManifestResourceNames()
                    .FirstOrDefault(r => r.EndsWith(name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Email template '{name}' was not found as an embedded resource.");

                using var stream = _assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Email template '{name}' could not be opened.");
                using var reader = new StreamReader(stream);

                return reader.ReadToEnd();
            });
    }

    private static string RenderConditionals(string source, IReadOnlyDictionary<string, string> values)
    {
        return IfBlockRegex.Replace(
            source,
            match =>
            {
                var key = match.Groups[1].Value;
                var content = match.Groups[2].Value;

                var isPresent = values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

                return isPresent ? content : string.Empty;
            });
    }

    private static string RenderTokens(string source, IReadOnlyDictionary<string, string> values)
    {
        return TokenRegex.Replace(
            source,
            match =>
            {
                var key = match.Groups[1].Value;

                if (values.TryGetValue(key, out var value))
                    return WebUtility.HtmlEncode(value);

                return match.Value;
            });
    }
}