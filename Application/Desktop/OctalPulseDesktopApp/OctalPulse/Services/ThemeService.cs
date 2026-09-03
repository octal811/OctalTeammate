using System.Windows;

namespace OctalPulse.Services;

public class ThemeService
{
    private string _currentTheme = "Light";

    public string CurrentTheme => _currentTheme;

    public void SetTheme(string theme)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        var themeUri = theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? new Uri("pack://application:,,,/OctalPulse;component/Styles/Light.xaml")
            : new Uri("pack://application:,,,/OctalPulse;component/Styles/Dark.xaml");

        var oldThemeDict = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null && (d.Source.OriginalString.Contains("Dark.xaml") || d.Source.OriginalString.Contains("Light.xaml")));

        if (oldThemeDict != null)
        {
            app.Resources.MergedDictionaries.Remove(oldThemeDict);
        }

        app.Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = themeUri });
        _currentTheme = theme.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
    }

    public void ToggleTheme()
    {
        SetTheme(_currentTheme == "Dark" ? "Light" : "Dark");
    }
}
