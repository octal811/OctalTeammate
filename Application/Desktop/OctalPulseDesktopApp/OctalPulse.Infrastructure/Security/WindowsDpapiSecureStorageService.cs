using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public class WindowsDpapiSecureStorageService : ISecureStorageService
{
    private readonly string _storageDirectory;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OctalPulse.SecureEntropy.2026");

    public WindowsDpapiSecureStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _storageDirectory = Path.Combine(appData, "OctalPulse", "SecureStore");
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public void SaveSecret(string key, string secret)
    {
        if (string.IsNullOrEmpty(key)) return;
        var filePath = GetFilePath(key);

        if (secret == null)
        {
            RemoveSecret(key);
            return;
        }

        var plainBytes = Encoding.UTF8.GetBytes(secret);
        var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(filePath, encryptedBytes);
    }

    public string? GetSecret(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        var filePath = GetFilePath(key);
        if (!File.Exists(filePath)) return null;

        try
        {
            var encryptedBytes = File.ReadAllBytes(filePath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    public void RemoveSecret(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var filePath = GetFilePath(key);
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Ignored if file cannot be deleted
            }
        }
    }

    private string GetFilePath(string key)
    {
        var safeKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(key))
            .Replace('/', '_')
            .Replace('+', '-');
        return Path.Combine(_storageDirectory, $"{safeKey}.dat");
    }
}
