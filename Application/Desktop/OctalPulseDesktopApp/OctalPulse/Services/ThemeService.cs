using System.Windows;

namespace OctalPulse.Services;

public class ThemeService
{
    private string _currentTheme = "Light";

    public string CurrentTheme => _currentTheme;

    private static readonly HashSet<string> _knownThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Light", "Dark", "OledDark"
    };

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