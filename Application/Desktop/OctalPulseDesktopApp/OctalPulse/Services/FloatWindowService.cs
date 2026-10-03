using System.Windows;
using OctalPulse.FloatWindows;

namespace OctalPulse.Services;

public enum FloatWindowType
{
    MajorTasks,
    MinorTasks,
    Stopwatch,
    Calendar,
    Media,
    Notes
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
    private NotesFloatWindow? _notes;

    // Topmost preference map (default matches historical defaults: Notes=true, others=false)
    private readonly Dictionary<FloatWindowType, bool> _topmostMap = new()
    {
        [FloatWindowType.MajorTasks] = false,
        [FloatWindowType.MinorTasks] = false,
        [FloatWindowType.Stopwatch] = false,
        [FloatWindowType.Calendar] = false,
        [FloatWindowType.Media] = false,
        [FloatWindowType.Notes] = true,
    };

    public FloatWindowService(IServiceProvider services)
    {
        _services = services;
    }

    public bool GetTopmost(FloatWindowType type) =>
        _topmostMap.TryGetValue(type, out var val) && val;

    public void SetTopmost(FloatWindowType type, bool topmost)
    {
        _topmostMap[type] = topmost;
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var win = GetExisting(type);
            if (win != null)
            {
                win.Topmost = topmost;
            }
        });
    }

    public void InitializePreferences(OctalPulse.Domain.Entities.UserPreferences pref)
    {
        _topmostMap[FloatWindowType.MajorTasks] = pref.FloatTopmostMajorTasks;
        _topmostMap[FloatWindowType.MinorTasks] = pref.FloatTopmostMinorTasks;
        _topmostMap[FloatWindowType.Stopwatch] = pref.FloatTopmostStopwatch;
        _topmostMap[FloatWindowType.Calendar] = pref.FloatTopmostCalendar;
        _topmostMap[FloatWindowType.Media] = pref.FloatTopmostMedia;
        _topmostMap[FloatWindowType.Notes] = pref.FloatTopmostNotes;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_majorTasks != null) _majorTasks.Topmost = pref.FloatTopmostMajorTasks;
            if (_minorTasks != null) _minorTasks.Topmost = pref.FloatTopmostMinorTasks;
            if (_stopwatch != null) _stopwatch.Topmost = pref.FloatTopmostStopwatch;
            if (_calendar != null) _calendar.Topmost = pref.FloatTopmostCalendar;
            if (_media != null) _media.Topmost = pref.FloatTopmostMedia;
            if (_notes != null) _notes.Topmost = pref.FloatTopmostNotes;
        });
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
            _notes?.Hide();
        });
    }

    public Window? GetExisting(FloatWindowType type) => type switch
    {
        FloatWindowType.MajorTasks => (Window?)_majorTasks,
        FloatWindowType.MinorTasks => (Window?)_minorTasks,
        FloatWindowType.Stopwatch  => (Window?)_stopwatch,
        FloatWindowType.Calendar   => (Window?)_calendar,
        FloatWindowType.Media      => (Window?)_media,
        FloatWindowType.Notes      => (Window?)_notes,
        _ => null
    };

    private Window GetOrCreate(FloatWindowType type)
    {
        Window win = type switch
        {
            FloatWindowType.MajorTasks  => (Window)(_majorTasks  ??= (MajorTasksFloatWindow) _services.GetService(typeof(MajorTasksFloatWindow))!),
            FloatWindowType.MinorTasks  => (Window)(_minorTasks  ??= (MinorTasksFloatWindow) _services.GetService(typeof(MinorTasksFloatWindow))!),
            FloatWindowType.Stopwatch   => (Window)(_stopwatch   ??= (StopwatchFloatWindow)  _services.GetService(typeof(StopwatchFloatWindow))!),
            FloatWindowType.Calendar    => (Window)(_calendar    ??= (CalendarFloatWindow)   _services.GetService(typeof(CalendarFloatWindow))!),
            FloatWindowType.Media       => (Window)(_media       ??= (MediaFloatWindow)      _services.GetService(typeof(MediaFloatWindow))!),
            FloatWindowType.Notes       => (Window)(_notes       ??= (NotesFloatWindow)      _services.GetService(typeof(NotesFloatWindow))!),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        win.Topmost = GetTopmost(type);
        return win;
    }
}
