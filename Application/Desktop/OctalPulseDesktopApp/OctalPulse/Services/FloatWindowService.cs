using System.Windows;
using OctalPulse.FloatWindows;

namespace OctalPulse.Services;

public enum FloatWindowType
{
    MajorTasks,
    MinorTasks,
    Stopwatch,
    Calendar,
    Media
}

/// <summary>
/// Manages the lifecycle of all floating overlay windows.
/// </summary>
public sealed class FloatWindowService
{
    private readonly IServiceProvider _services;

    // Lazy singletons — created on first access
    private MajorTasksFloatWindow? _majorTasks;
    private MinorTasksFloatWindow? _minorTasks;
    private StopwatchFloatWindow? _stopwatch;
    private CalendarFloatWindow? _calendar;
    private MediaFloatWindow? _media;

    public FloatWindowService(IServiceProvider services)
    {
        _services = services;
    }

    public void Toggle(FloatWindowType type)
    {
        var window = GetOrCreate(type);
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (window.IsVisible)
                window.Hide();
            else
                window.Show();
        });
    }

    public void HideAll()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            _majorTasks?.Hide();
            _minorTasks?.Hide();
            _stopwatch?.Hide();
            _calendar?.Hide();
            _media?.Hide();
        });
    }

    private Window GetOrCreate(FloatWindowType type)
    {
        return type switch
        {
            FloatWindowType.MajorTasks  => _majorTasks  ??= (MajorTasksFloatWindow) _services.GetService(typeof(MajorTasksFloatWindow))!,
            FloatWindowType.MinorTasks  => _minorTasks  ??= (MinorTasksFloatWindow) _services.GetService(typeof(MinorTasksFloatWindow))!,
            FloatWindowType.Stopwatch   => _stopwatch   ??= (StopwatchFloatWindow)  _services.GetService(typeof(StopwatchFloatWindow))!,
            FloatWindowType.Calendar    => _calendar    ??= (CalendarFloatWindow)   _services.GetService(typeof(CalendarFloatWindow))!,
            FloatWindowType.Media       => _media       ??= (MediaFloatWindow)      _services.GetService(typeof(MediaFloatWindow))!,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
