using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace OctalPulse.Services;

public static class MermaidDiagramService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OctalPulse", "Cache", "Diagrams");

    static MermaidDiagramService()
    {
        try
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }
        catch
        {
            // Ignore cache directory creation failures
        }

        if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }
    }

    public static string DetectDiagramType(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Diagram";
        var trimmed = code.TrimStart().ToLowerInvariant();

        if (trimmed.StartsWith("pie")) return "Pie Chart";
        if (trimmed.StartsWith("graph") || trimmed.StartsWith("flowchart")) return "Flowchart";
        if (trimmed.StartsWith("sequencediagram")) return "Sequence Diagram";
        if (trimmed.StartsWith("statediagram")) return "State Diagram";
        if (trimmed.StartsWith("erdiagram")) return "Entity Relationship";
        if (trimmed.StartsWith("gantt")) return "Gantt Timeline";
        if (trimmed.StartsWith("mindmap")) return "Mindmap";
        if (trimmed.StartsWith("classdiagram")) return "Class Diagram";
        if (trimmed.StartsWith("gitgraph")) return "Git Graph";
        if (trimmed.StartsWith("timeline")) return "Timeline";
        if (trimmed.StartsWith("quadrantchart")) return "Quadrant Chart";

        return "Mermaid Diagram";
    }

    public static string GetCacheFilePath(string code)
    {
        var hash = ComputeSha256(code.Trim());
        return Path.Combine(CacheDirectory, $"{hash}.png");
    }

    public static async Task<BitmapImage?> GetDiagramBitmapAsync(string mermaidCode)
    {
        if (string.IsNullOrWhiteSpace(mermaidCode)) return null;

        var cacheFile = GetCacheFilePath(mermaidCode);

        // 1. Check local cache first (Instant & Offline)
        if (File.Exists(cacheFile))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(cacheFile);
                if (bytes.Length > 0)
                {
                    return CreateBitmapFromBytes(bytes);
                }
            }
            catch
            {
                // Fallback to fetch if cached file is corrupted
            }
        }

        // 2. Fetch from Mermaid rendering endpoint
        try
        {
            var rawBytes = Encoding.UTF8.GetBytes(mermaidCode.Trim());
            var b64 = Convert.ToBase64String(rawBytes);
            var url = $"https://mermaid.ink/img/{b64}?type=png";

            var response = await HttpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(cacheFile, imageBytes);
                    }
                    catch
                    {
                        // Cache write failure is non-fatal
                    }

                    return CreateBitmapFromBytes(imageBytes);
                }
            }
        }
        catch
        {
            // Network failure or offline
        }

        return null;
    }

    private static BitmapImage CreateBitmapFromBytes(byte[] bytes)
    {
        var bitmap = new BitmapImage();
        using (var stream = new MemoryStream(bytes))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
        }
        bitmap.Freeze(); // Make thread-safe for WPF UI
        return bitmap;
    }

    private static string ComputeSha256(string raw)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var sb = new StringBuilder();
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
