namespace OctalPulse.Domain.ValueObjects;

public enum ExternalLinkType
{
    GitHub,
    Jira,
    GoogleDrive,
    Generic
}

public record ExternalWorkLink(string Url, string? Title, ExternalLinkType Type)
{
    public static ExternalWorkLink FromUrl(string url, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new ExternalWorkLink(string.Empty, title ?? string.Empty, ExternalLinkType.Generic);

        var lower = url.ToLowerInvariant();
        var type = lower switch
        {
            _ when lower.Contains("github.com") => ExternalLinkType.GitHub,
            _ when lower.Contains("atlassian.net") || lower.Contains("jira") => ExternalLinkType.Jira,
            _ when lower.Contains("drive.google.com") || lower.Contains("docs.google.com") => ExternalLinkType.GoogleDrive,
            _ => ExternalLinkType.Generic
        };

        return new ExternalWorkLink(url, title ?? url, type);
    }
}
