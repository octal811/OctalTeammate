using System.IO;
using System.Text.Json;

namespace OctalPulse.Services;

public record FloatWindowPosition(double Left, double Top, bool IsLocked);

/// <summary>
/// Persists floating window positions and lock states to a JSON file in local AppData.
/// </summary>
public sealed class FloatWindowPositionStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OctalPulse",
        "float_positions.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private Dictionary<string, FloatWindowPosition> _positions = new();

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = await File.ReadAllTextAsync(FilePath);
            _positions = JsonSerializer.Deserialize<Dictionary<string, FloatWindowPosition>>(json, JsonOptions)
                         ?? new Dictionary<string, FloatWindowPosition>();
        }
        catch
        {
            _positions = new Dictionary<string, FloatWindowPosition>();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(_positions, JsonOptions);
            await File.WriteAllTextAsync(FilePath, json);
        }
        catch
        {
            // Non-critical: position won't be persisted this session
        }
    }

    public FloatWindowPosition? Get(string windowKey) =>
        _positions.TryGetValue(windowKey, out var pos) ? pos : null;

    public void Set(string windowKey, double left, double top, bool isLocked)
    {
        _positions[windowKey] = new FloatWindowPosition(left, top, isLocked);
        _ = SaveAsync();
    }
}
