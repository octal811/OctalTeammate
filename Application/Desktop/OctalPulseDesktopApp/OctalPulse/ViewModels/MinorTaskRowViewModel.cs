using CommunityToolkit.Mvvm.ComponentModel;
using OctalPulse.Application.Contracts;

namespace OctalPulse.ViewModels;

public partial class MinorTaskRowViewModel : ObservableObject
{
    public MinorTaskItem Item { get; }
    public bool IsOwner { get; }
    public bool HasWorkTime { get; }
    public string WorkTimeDisplay { get; }

    public MinorTaskRowViewModel(MinorTaskItem item, bool isOwner)
    {
        Item = item;
        IsOwner = isOwner;
        var seconds = item.WorkTimeSeconds ?? 0;
        HasWorkTime = seconds > 0;
        WorkTimeDisplay = Format(seconds);
    }

    public static string Format(long totalSeconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return t.TotalHours >= 24
            ? $"{(int)t.TotalDays}d {t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}"
            : t.ToString(@"hh\:mm\:ss");
    }
}