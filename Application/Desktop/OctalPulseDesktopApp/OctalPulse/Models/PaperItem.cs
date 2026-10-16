using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OctalPulse.Models;

public partial class PaperItem : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private DateTime _lastModified = DateTime.Now;

    [ObservableProperty]
    private string _fileSizeDisplay = "0 B";

    [ObservableProperty]
    private string _snippet = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public string FormattedLastModified
    {
        get
        {
            var diff = DateTime.Now - LastModified;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24 && LastModified.Date == DateTime.Today) return $"Today at {LastModified:h:mm tt}";
            if (LastModified.Date == DateTime.Today.AddDays(-1)) return $"Yesterday at {LastModified:h:mm tt}";
            return LastModified.ToString("MMM d, yyyy");
        }
    }
}
