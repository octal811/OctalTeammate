using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OctalPulse.Domain.Enums;

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
