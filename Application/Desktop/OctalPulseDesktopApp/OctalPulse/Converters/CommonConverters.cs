using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OctalPulse.Domain.Enums;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolVal;
        if (value is bool b)
        {
            boolVal = b;
        }
        else if (value is int i)
        {
            boolVal = i > 0;
        }
        else if (value is long l)
        {
            boolVal = l > 0;
        }
        else if (value is string str)
        {
            boolVal = !string.IsNullOrWhiteSpace(str);
        }
        else
        {
            boolVal = value is not null;
        }

        var shouldInvert = Invert || (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase));
        if (shouldInvert) boolVal = !boolVal;
        return boolVal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var vis = value is Visibility v && v == Visibility.Visible;
        return Invert ? !vis : vis;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        if (Invert) isNull = !isNull;
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class NullToEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value != null && !(value is string s && string.IsNullOrWhiteSpace(s));
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase)) hasValue = !hasValue;
        return hasValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class StringToInitialConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s.Trim()[0].ToString().ToUpperInvariant();
        }
        return "U";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class StateToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MinorTaskState state)
        {
            return state == MinorTaskState.Done;
        }
        if (value is string str)
        {
            return string.Equals(str, "Done", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate 400

        var str = value.ToString();
        return str switch
        {
            "Active" or "InProgress" => new SolidColorBrush(Color.FromRgb(59, 130, 246)), // Blue 500
            "Review" or "InReview" => new SolidColorBrush(Color.FromRgb(168, 85, 247)), // Purple 500
            "Done" or "Completed" or "Approved" => new SolidColorBrush(Color.FromRgb(34, 197, 94)), // Green 500
            "OnHold" or "Pending" => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber 500
            "Canceled" or "Failed" or "Rejected" or "Archived" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red 500
            "Todo" => new SolidColorBrush(Color.FromRgb(148, 163, 184)), // Slate 400
            _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PriorityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Priority p) return new SolidColorBrush(Color.FromRgb(148, 163, 184));

        return p switch
        {
            Priority.Low => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Slate 500
            Priority.Medium => new SolidColorBrush(Color.FromRgb(59, 130, 246)), // Blue 500
            Priority.High => new SolidColorBrush(Color.FromRgb(249, 115, 22)), // Orange 500
            Priority.Critical => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red 500
            _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class DateFormattingConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            var format = parameter as string ?? "MMM dd, yyyy";
            return dt.ToString(format, CultureInfo.CurrentCulture);
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class RelativeUrlToImageSourceConverter : IValueConverter
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource> _cache = new();

    private static readonly Regex DriveFileIdRegex = new(@"/file/d/([^/?#]+)", RegexOptions.IgnoreCase);
    private static readonly Regex DriveOpenIdRegex = new(@"/open\?[^#]*?id=([^&?#]+)", RegexOptions.IgnoreCase);
    private static readonly Regex DriveUcIdRegex = new(@"/uc\?[^#]*?[?&]id=([^&?#]+)", RegexOptions.IgnoreCase);

    private static string? ToDriveDirectUrl(string url)
    {
        static string? Id(Regex r, string u)
        {
            var m = r.Match(u);
            return m.Success ? m.Groups[1].Value : null;
        }

        var driveId = Id(DriveFileIdRegex, url)
            ?? Id(DriveOpenIdRegex, url)
            ?? Id(DriveUcIdRegex, url);

        return driveId is null ? null : $"https://drive.google.com/uc?export=view&id={driveId}";
    }

    public static void SetCachedImage(string? relativeOrFullUrl, ImageSource image)
    {
        var resolved = ApiConfiguration.ResolveUrl(relativeOrFullUrl);
        if (!string.IsNullOrEmpty(resolved))
        {
            var url = ToDriveDirectUrl(resolved) ?? resolved;
            _cache[url] = image;
        }
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var rawUrl = value as string;
        var url = ApiConfiguration.ResolveUrl(rawUrl);
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        url = ToDriveDirectUrl(url) ?? url;

        if (_cache.TryGetValue(url, out var cached) && cached != null)
        {
            return cached;
        }

        try
        {
            var uri = new Uri(url, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.None;
            bitmap.EndInit();

            if (!bitmap.IsDownloading)
            {
                if (bitmap.CanFreeze)
                {
                    bitmap.Freeze();
                }
                _cache[url] = bitmap;
            }
            else
            {
                bitmap.DownloadCompleted += (s, e) =>
                {
                    try
                    {
                        if (bitmap.CanFreeze)
                        {
                            bitmap.Freeze();
                        }
                        _cache[url] = bitmap;
                    }
                    catch
                    {
                    }
                };

                bitmap.DownloadFailed += (s, e) =>
                {
                    _cache.TryRemove(url, out _);
                };
            }

            return bitmap;
        }
        catch
        {
            // Unreachable or unreadable image — callers fall back to a placeholder.
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

