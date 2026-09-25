using System.IO;

namespace OctalPulse.Installer.Services;

public static class InstallerLogger
{
    private static readonly object _lock = new();
    private static string? _logFilePath;

    public static string LogFilePath
    {
        get
        {
            if (_logFilePath == null)
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var logDir = Path.Combine(appData, "OctalPulse", "logs");
                Directory.CreateDirectory(logDir);
                _logFilePath = Path.Combine(logDir, "installer.log");
            }
            return _logFilePath;
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null) => Log("ERROR", ex != null ? $"{message} | {ex}" : message);

    private static void Log(string level, string message)
    {
        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        try
        {
            lock (_lock)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Fallback: ignore logging failures so the installer doesn't crash
        }
        System.Diagnostics.Debug.WriteLine(line);
    }
}
