using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Contracts;

namespace OctalPulse.ViewModels;

public partial class MinorTaskRowViewModel : ObservableObject
{
    public MinorTaskItem Item { get; }
    public bool IsOwner { get; }
    public bool HasWorkTime { get; }
    public string WorkTimeDisplay { get; }
    public bool HasNotes { get; }
    public bool HasCreator { get; }
    public string CreatorName => Item.CreatedByUserName ?? "Unknown";
    public string CreatorEmail => Item.CreatedByUserEmail ?? string.Empty;
    public string CreatorDisplay => string.IsNullOrWhiteSpace(CreatorEmail) ? CreatorName : CreatorName;
    public string CreatorInitials => GetInitials(Item.CreatedByUserName);
    public string NotesToggleLabel => NotesExpanded ? "Hide notes" : "Show notes";

    [ObservableProperty]
    private bool _notesExpanded;

    public MinorTaskRowViewModel(MinorTaskItem item, bool isOwner)
    {
        Item = item;
        IsOwner = isOwner;
        var seconds = item.WorkTimeSeconds ?? 0;
        HasWorkTime = seconds > 0;
        WorkTimeDisplay = Format(seconds);
        HasNotes = !string.IsNullOrWhiteSpace(item.Notes);
        HasCreator = item.CreatedByUserId.HasValue;
    }

    [RelayCommand]
    private void ToggleNotes() => NotesExpanded = !NotesExpanded;

    public static string Format(long totalSeconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return t.TotalHours >= 24
            ? $"{(int)t.TotalDays}d {t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}"
            : t.ToString(@"hh\:mm\:ss");
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "A";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();

        return parts[0].Substring(0, 1).ToUpperInvariant();
    }
}