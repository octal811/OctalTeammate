using System.Collections.Generic;
using System.Threading.Tasks;
using OctalPulse.Models;

namespace OctalPulse.Services;

public interface IPapersStorageService
{
    string GetPapersDirectory();
    void SetPapersDirectory(string path);
    string EnsureDirectoryExists();
    Task<IReadOnlyList<PaperItem>> LoadPapersAsync();
    Task<string> ReadPaperContentAsync(string filePath);
    Task SavePaperContentAsync(string filePath, string content);
    Task<PaperItem> CreatePaperAsync(string title, string? initialContent = null);
    Task<PaperItem> RenamePaperAsync(string filePath, string newTitle);
    Task DeletePaperAsync(string filePath);
    void OpenFolderInExplorer();
}
