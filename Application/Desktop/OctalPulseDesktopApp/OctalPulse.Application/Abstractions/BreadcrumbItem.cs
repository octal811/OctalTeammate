using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace OctalPulse.Application.Abstractions;

/// <summary>A single clickable segment in the header breadcrumb trail.</summary>
public class BreadcrumbItem
{
    private readonly Action? _navigate;

    public string Label { get; }

    public bool IsFirst { get; set; }

    public bool IsCurrent { get; set; }

    public bool IsNavigable => !IsCurrent && _navigate != null;

    public ICommand NavigateCommand { get; }

    public BreadcrumbItem(string label, Action? navigate = null)
    {
        Label = label;
        _navigate = navigate;
        NavigateCommand = new RelayCommand(Execute);
    }

    private void Execute()
    {
        if (IsNavigable)
        {
            _navigate?.Invoke();
        }
    }
}

/// <summary>Navigation payload that carries the names needed to build the breadcrumb trail.</summary>
public sealed record TrackNavigationPayload(
    Guid TrackId,
    string TrackName,
    Guid ProjectId,
    string ProjectName);

public sealed record MajorTaskNavigationPayload(
    Guid MajorTaskId,
    string MajorTaskTitle,
    Guid TrackId,
    string TrackName,
    Guid ProjectId,
    string ProjectName);