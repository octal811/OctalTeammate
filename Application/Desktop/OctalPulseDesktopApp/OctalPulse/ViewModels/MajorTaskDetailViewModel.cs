using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;
using OctalPulse.Domain.ValueObjects;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class MajorTaskDetailViewModel : ObservableObject, INavigationAware
{
    private readonly ITaskService _taskService;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IUserSession _userSession;

    [ObservableProperty]
    private Guid _majorTaskId;

    [ObservableProperty]
    private string _taskTitle = "Task Details";

    [ObservableProperty]
    private string? _taskDescription;

    [ObservableProperty]
    private string? _taskLink;

    [ObservableProperty]
    private ExternalWorkLink? _parsedLink;

    [ObservableProperty]
    private int _progressPercentage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAddingMinor;

    // Create Minor Task
    [ObservableProperty]
    private string _newMinorTitle = string.Empty;

    [ObservableProperty]
    private string _newMinorTarget = string.Empty;

    [ObservableProperty]
    private string _newMinorDescription = string.Empty;

    [ObservableProperty]
    private string _newMinorNotes = string.Empty;

    [ObservableProperty]
    private string _newMinorLink = string.Empty;

    [ObservableProperty]
    private MinorTaskJobType? _newMinorJobType;

    public MinorTaskJobType[] AvailableJobTypes { get; } = Enum.GetValues<MinorTaskJobType>();

    public ObservableCollection<MinorTaskRowViewModel> MinorTaskRows { get; } = new();

    public MajorTaskDetailViewModel(
        ITaskService taskService,
        ISignalRRealtimeService signalRService,
        INavigationService navigationService,
        IDialogService dialogService,
        IUserSession userSession)
    {
        _taskService = taskService;
        _signalRService = signalRService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _userSession = userSession;

        _signalRService.MinorTaskChanged += OnMinorTaskChanged;
    }

    public void OnNavigatedTo(object? parameter)
    {
        switch (parameter)
        {
            case MajorTaskItem item:
                ApplyTask(item);
                break;

            case MajorTaskCardModel card:
                ApplyTask(card);
                break;

            case Guid id:
                MajorTaskId = id;
                _ = LoadMinorTasksAsync();
                break;
        }
    }

    private void ApplyTask(MajorTaskItem task)
    {
        MajorTaskId = task.Id;
        TaskTitle = task.Title;
        TaskDescription = task.Description;
        TaskLink = task.Link;
        ProgressPercentage = task.Progress;
        ParsedLink = !string.IsNullOrWhiteSpace(task.Link) ? ExternalWorkLink.FromUrl(task.Link) : null;

        _ = LoadMinorTasksAsync();
    }

    private void ApplyTask(MajorTaskCardModel card)
    {
        MajorTaskId = card.Id;
        TaskTitle = card.Title;
        TaskDescription = card.Description;
        TaskLink = card.Link;
        ProgressPercentage = card.Progress;
        ParsedLink = !string.IsNullOrWhiteSpace(card.Link) ? ExternalWorkLink.FromUrl(card.Link) : null;

        _ = LoadMinorTasksAsync();
    }

    private void OnMinorTaskChanged(Guid minorTaskId, Guid trackId)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
        {
            await LoadMinorTasksAsync();
            _dialogService.ShowToast("Progress Update", "Task progress updated in real time.", ToastType.Info);
        });
    }

    [RelayCommand]
    private async Task LoadMinorTasksAsync()
    {
        IsBusy = true;
        try
        {
            var res = await _taskService.GetMinorTasksByMajorTaskAsync(MajorTaskId);
            MinorTaskRows.Clear();

            var doneCount = 0;
            foreach (var m in res.MinorTasks)
            {
                MinorTaskRows.Add(new MinorTaskRowViewModel(m, m.CreatedByUserId == _userSession.UserId));
                if (m.State == MinorTaskState.Done)
                {
                    doneCount++;
                }
            }

            ProgressPercentage = res.MinorTasks.Count > 0 ? (doneCount * 100) / res.MinorTasks.Count : 0;

            if (!string.IsNullOrWhiteSpace(TaskLink))
            {
                ParsedLink = ExternalWorkLink.FromUrl(TaskLink);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Load Error", $"Could not load sub-tasks: {ex.Message}", ToastType.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleTaskDoneAsync(MinorTaskRowViewModel row)
    {
        if (row == null) return;
        var task = row.Item;

        var newState = task.State == MinorTaskState.Done ? MinorTaskState.InProgress : MinorTaskState.Done;

        IsBusy = true;
        try
        {
            await _taskService.UpdateMinorTaskAsync(new UpdateMinorTaskRequest(
                task.Id,
                task.Title,
                task.Description,
                task.Target,
                newState,
                task.Notes,
                task.Link,
                task.Order,
                task.AssignedUserId));

            await LoadMinorTasksAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Update Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateMinorTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMinorTitle))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a task title.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            await _taskService.CreateMinorTaskAsync(new CreateMinorTaskRequest(
                MajorTaskId,
                NewMinorTitle.Trim(),
                string.IsNullOrWhiteSpace(NewMinorDescription) ? null : NewMinorDescription.Trim(),
                string.IsNullOrWhiteSpace(NewMinorTarget) ? null : NewMinorTarget.Trim(),
                MinorTaskState.Todo,
                NewMinorJobType,
                string.IsNullOrWhiteSpace(NewMinorNotes) ? null : NewMinorNotes.Trim(),
                string.IsNullOrWhiteSpace(NewMinorLink) ? null : NewMinorLink.Trim(),
                MinorTaskRows.Count + 1,
                null));

            NewMinorTitle = string.Empty;
            NewMinorTarget = string.Empty;
            NewMinorDescription = string.Empty;
            NewMinorNotes = string.Empty;
            NewMinorLink = string.Empty;
            NewMinorJobType = null;

            _dialogService.ShowToast("Task Added", "Sub-task added to checklist.", ToastType.Success);
            IsAddingMinor = false;
            await LoadMinorTasksAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Create Sub-task Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteMinorTaskAsync(MinorTaskRowViewModel row)
    {
        if (row == null) return;

        IsBusy = true;
        try
        {
            await _taskService.DeleteMinorTaskAsync(row.Item.Id);
            _dialogService.ShowToast("Task Removed", "Sub-task deleted.", ToastType.Info);
            await LoadMinorTasksAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Delete Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleAddMinor()
    {
        IsAddingMinor = !IsAddingMinor;
    }

    [RelayCommand]
    private async Task AddWorkTimeAsync(MinorTaskRowViewModel row)
    {
        if (row == null || !row.IsOwner) return;

        IsBusy = true;
        try
        {
            var input = await _dialogService.ShowPromptAsync(
                "Add Work Time",
                $"Add work time to \"{row.Item.Title}\" in HH:MM format (e.g. 01:30 = 1h 30m).",
                "00:00");
            if (string.IsNullOrWhiteSpace(input)) return;

            var seconds = ParseTimeInput(input);
            if (seconds <= 0)
            {
                _dialogService.ShowToast("Invalid Value", "Enter a positive time in HH:MM format.", ToastType.Warning);
                return;
            }

            var res = await _taskService.AddMinorTaskWorkTimeAsync(new AddMinorTaskWorkTimeRequest(row.Item.Id, seconds));
            _dialogService.ShowToast(
                "Work Time Added",
                $"Added {MinorTaskRowViewModel.Format(seconds)}. Total: {MinorTaskRowViewModel.Format(res.WorkTimeSeconds)}.",
                ToastType.Success);
            await LoadMinorTasksAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Add Time Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static long ParseTimeInput(string input)
    {
        var s = input.Trim();
        if (s.Contains(':'))
        {
            var parts = s.Split(':');
            if (parts.Length is < 2 or > 3) return 0;
            if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return 0;
            var sec = parts.Length == 3 && int.TryParse(parts[2], out var ss) ? ss : 0;
            if (h < 0 || m < 0 || sec < 0 || m > 59 || sec > 59) return 0;
            return h * 3600L + m * 60L + sec;
        }

        if (int.TryParse(s, out var minutes) && minutes > 0) return minutes * 60L;
        return 0;
    }

    [RelayCommand]
    private void OpenExternalLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Open Link Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
