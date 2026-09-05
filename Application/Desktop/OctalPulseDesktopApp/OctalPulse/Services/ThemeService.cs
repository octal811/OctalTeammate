using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OctalPulse.Services;

public class ThemeService
{
    private string _currentTheme = "Light";

    public string CurrentTheme => _currentTheme;

    /// <summary>Fires whenever the theme changes, passing the new theme name.</summary>
    public event Action<string>? ThemeChanged;

    private static readonly HashSet<string> _knownThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Light", "Dark", "OledDark"
    };

    // ── DWM P/Invokes ─────────────────────────────────────────────
    // DWMWA_USE_IMMERSIVE_DARK_MODE (20) — makes X/min/max icons white on dark
    // DWMWA_CAPTION_COLOR (35)           — paints the caption bar background
    // Both require Windows 11 build 22000+; silently ignored on older builds.

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private static int HexToColorRef(string hex)
    {
        hex = hex.TrimStart('#');
        int r = Convert.ToInt32(hex[..2], 16);
        int g = Convert.ToInt32(hex[2..4], 16);
        int b = Convert.ToInt32(hex[4..6], 16);
        return r | (g << 8) | (b << 16); // COLORREF = 0x00BBGGRR
    }

    /// <summary>
    /// Applies the title bar color + dark-mode icons for the given theme.
    /// Must be called AFTER the window's HWND is created (SourceInitialized / Loaded).
    /// </summary>
    public static void ApplyTitleBarForTheme(IntPtr hwnd, string theme)
    {
        if (hwnd == IntPtr.Zero) return;

        try
        {
            bool isDark = theme is "Dark" or "OledDark";

            // 1. Make caption-button icons white (dark mode) or black (light mode)
            int darkMode = isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // 2. Paint the caption bar background
            string hexColor = theme.ToLowerInvariant() switch
            {
                "oleddark" => "#000000",   // Pure black for OLED
                "dark"     => "#0B0F19",   // Deep navy for Dark
                _          => "#FFFFFF"    // White for Light
            };
            int colorRef = HexToColorRef(hexColor);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref colorRef, sizeof(int));

            // Force the non-client caption area to repaint immediately with the new
            // attributes. Without a frame-changed invalidation DWM defers the repaint
            // until the window is resized/maximized, which is why the title bar color
            // previously looked stuck until the window was maximized.
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
        catch
        {
            // API not available on this Windows version — silently ignore
        }
    }

    private static Uri ThemeUri(string theme) => theme.ToLowerInvariant() switch
    {
        "light"    => new Uri("pack://application:,,,/OctalPulse;component/Styles/Light.xaml"),
        "oleddark" => new Uri("pack://application:,,,/OctalPulse;component/Styles/OledDark.xaml"),
        _          => new Uri("pack://application:,,,/OctalPulse;component/Styles/Dark.xaml")
    };

    public void SetTheme(string theme)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        // Swap theme resource dictionaries
        var toRemove = app.Resources.MergedDictionaries
            .Where(d => d.Source != null && (
                d.Source.OriginalString.Contains("Dark.xaml")    ||
                d.Source.OriginalString.Contains("Light.xaml")   ||
                d.Source.OriginalString.Contains("OledDark.xaml")))
            .ToList();

        foreach (var old in toRemove)
            app.Resources.MergedDictionaries.Remove(old);

        app.Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = ThemeUri(theme) });

        _currentTheme = _knownThemes.Contains(theme) ? theme : "Dark";

        // Notify MainWindow (and any other subscriber) to repaint its title bar
        ThemeChanged?.Invoke(_currentTheme);
    }

    public void ToggleTheme()
    {
        SetTheme(_currentTheme switch
        {
            "Light"    => "Dark",
            "Dark"     => "OledDark",
            "OledDark" => "Light",
            _          => "Light"
        });
    }
}
