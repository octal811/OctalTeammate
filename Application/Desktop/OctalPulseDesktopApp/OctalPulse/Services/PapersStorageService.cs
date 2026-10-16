using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OctalPulse.Models;

namespace OctalPulse.Services;

public class PapersStorageService : IPapersStorageService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OctalPulse",
        "papers_config.json");

    private string? _customDirectory;

    public PapersStorageService()
    {
        LoadConfig();
    }

    private string GetDefaultDirectory()
    {
        try
        {
            var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(myDocs) && Directory.Exists(myDocs))
            {
                return Path.Combine(myDocs, "OctalPulse", "Papers");
            }
        }
        catch { }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OctalPulse",
            "Papers");
    }

    public string GetPapersDirectory()
    {
        return EnsureDirectoryExists();
    }

    public void SetPapersDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            _customDirectory = Path.GetFullPath(path);
            SaveConfig();
        }
        catch (Exception)
        {
            // If failed to set custom path, fallback to default
            _customDirectory = null;
            SaveConfig();
            EnsureDirectoryExists();
            throw;
        }
    }

    public string EnsureDirectoryExists()
    {
        var target = !string.IsNullOrWhiteSpace(_customDirectory) ? _customDirectory : GetDefaultDirectory();

        try
        {
            if (!Directory.Exists(target))
            {
                Directory.CreateDirectory(target);
            }
            return target;
        }
        catch
        {
            // If custom folder was on deleted drive / lost, fall back safely to default
            var fallback = GetDefaultDirectory();
            try
            {
                if (!Directory.Exists(fallback))
                {
                    Directory.CreateDirectory(fallback);
                }
                _customDirectory = null;
                SaveConfig();
                return fallback;
            }
            catch
            {
                // Ultimate fallback
                var localApp = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OctalPulse",
                    "Papers");
                Directory.CreateDirectory(localApp);
                _customDirectory = null;
                return localApp;
            }
        }
    }

    public async Task<IReadOnlyList<PaperItem>> LoadPapersAsync()
    {
        var dir = EnsureDirectoryExists();
        SeedDefaultPaperIfEmpty(dir);

        var list = new List<PaperItem>();
        var files = Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                var title = Path.GetFileNameWithoutExtension(file);

                string snippet = string.Empty;
                try
                {
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    var raw = await reader.ReadToEndAsync();
                    var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var firstMeaningful = lines.FirstOrDefault(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l));
                    snippet = firstMeaningful ?? (lines.FirstOrDefault() ?? string.Empty);
                    if (snippet.Length > 90) snippet = snippet[..87] + "...";
                }
                catch
                {
                    snippet = "Markdown Document";
                }

                list.Add(new PaperItem
                {
                    FilePath = file,
                    FileName = fileInfo.Name,
                    Title = title,
                    LastModified = fileInfo.LastWriteTime,
                    FileSizeDisplay = FormatBytes(fileInfo.Length),
                    Snippet = snippet
                });
            }
            catch
            {
                // Skip problematic file without throwing
            }
        }

        return list.OrderByDescending(p => p.LastModified).ToList();
    }

    public async Task<string> ReadPaperContentAsync(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public async Task SavePaperContentAsync(string filePath, string content)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8);
        await writer.WriteAsync(content);
    }

    public async Task<PaperItem> CreatePaperAsync(string title, string? initialContent = null)
    {
        var dir = EnsureDirectoryExists();
        var safeTitle = SanitizeFileName(string.IsNullOrWhiteSpace(title) ? "Untitled Paper" : title.Trim());
        var fileName = $"{safeTitle}.md";
        var fullPath = Path.Combine(dir, fileName);

        var counter = 1;
        while (File.Exists(fullPath))
        {
            fileName = $"{safeTitle} ({counter++}).md";
            fullPath = Path.Combine(dir, fileName);
        }

        var content = initialContent ?? $"# {Path.GetFileNameWithoutExtension(fileName)}\n\n*Created on {DateTime.Now:MMMM d, yyyy}*\n\nStart writing your research, notes, and documentation here...\n";
        await SavePaperContentAsync(fullPath, content);

        var fileInfo = new FileInfo(fullPath);
        return new PaperItem
        {
            FilePath = fullPath,
            FileName = fileInfo.Name,
            Title = Path.GetFileNameWithoutExtension(fullPath),
            LastModified = fileInfo.LastWriteTime,
            FileSizeDisplay = FormatBytes(fileInfo.Length),
            Snippet = "Start writing your research, notes..."
        };
    }

    public Task<PaperItem> RenamePaperAsync(string filePath, string newTitle)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("File not found", filePath);

        var dir = Path.GetDirectoryName(filePath) ?? EnsureDirectoryExists();
        var safeTitle = SanitizeFileName(newTitle.Trim());
        var newFileName = $"{safeTitle}.md";
        var newPath = Path.Combine(dir, newFileName);

        if (!filePath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
        {
            var counter = 1;
            while (File.Exists(newPath))
            {
                newFileName = $"{safeTitle} ({counter++}).md";
                newPath = Path.Combine(dir, newFileName);
            }

            File.Move(filePath, newPath);
        }

        var fileInfo = new FileInfo(newPath);
        return Task.FromResult(new PaperItem
        {
            FilePath = newPath,
            FileName = fileInfo.Name,
            Title = Path.GetFileNameWithoutExtension(newPath),
            LastModified = fileInfo.LastWriteTime,
            FileSizeDisplay = FormatBytes(fileInfo.Length),
            Snippet = string.Empty
        });
    }

    public Task DeletePaperAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    public void OpenFolderInExplorer()
    {
        var dir = EnsureDirectoryExists();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void SeedDefaultPaperIfEmpty(string dir)
    {
        try
        {
            if (Directory.GetFiles(dir, "*.md").Length == 0)
            {
                var samplePath = Path.Combine(dir, "Welcome to Papers.md");
                var sample = @"# Welcome to OctalPulse Papers 📑

Papers is your dedicated local research and documentation notebook.
All documents are written in **Markdown** and saved offline on your computer.

---

## 🚀 Key Features

- **GitHub-style Modes**: Seamlessly toggle between **Edit Mode** (raw Markdown with syntax) and **Read Mode** (beautiful rich visual rendering).
- **Offline & Private**: Stored locally in your personal folder. You can change the folder location anytime.
- **Auto-Crash Recovery**: If your saved papers folder is ever moved or deleted, Papers automatically re-creates it without crashing!
- **Markdown Tools**: Fast toolbar helpers for Headings, Bold, Italic, Strikethrough, Code Blocks, Quotes, Lists, Tables, and Photo insertion from your PC.

---

## 📝 Markdown Cheatsheet

### Headings
# Heading 1
## Heading 2
### Heading 3

### Typography
- **Bold text** with `**bold**`
- *Italic text* with `*italic*`
- ~~Strikethrough~~ with `~~strikethrough~~`
- `Inline code` with backticks

### Checklists & Lists
- [x] Create first paper
- [x] Test Read and Edit mode
- [ ] Add screenshots or research links

### Blockquotes
> ""Research is to see what everybody else has seen, and to think what nobody else has thought."" — Albert Szent-Gyorgyi

### Code Blocks
```csharp
// Clean C# Code Example
public class Paper 
{
    public string Title { get; set; } = ""Research Paper"";
    public bool IsOffline { get; set; } = true;
}
```

### Tables
| Feature | Supported | Mode |
| :--- | :---: | :--- |
| Markdown Editing | Yes | Edit Mode |
| Flow Document Preview | Yes | Read Mode |
| Local PC Photos | Yes | Both |
| Folder Backup | Yes | Both |

---

Enjoy writing with OctalPulse Papers!
";
                File.WriteAllText(samplePath, sample);
            }
        }
        catch { }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Paper" : cleaned;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("PapersDirectory", out var prop))
                {
                    _customDirectory = prop.GetString();
                }
            }
        }
        catch { }
    }

    private void SaveConfig()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(new { PapersDirectory = _customDirectory }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}
