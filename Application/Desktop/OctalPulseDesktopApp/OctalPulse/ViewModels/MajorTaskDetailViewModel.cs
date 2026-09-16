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

public partial class MajorTaskDetailViewModel : ObservableObject, INavigationAware, INavigationFromAware
{
    private readonly ITaskService _taskService;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IFastAddDialogService _fastAddDialogService;
    private readonly IUserSession _userSession;

    [ObservableProperty]
    private Guid _majorTaskId;

    private Guid _trackId;
    private string _trackName = "Track Tasks";
    private Guid _projectId;
    private string _projectName = "Projects";

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

    // Edit Minor Task
    [ObservableProperty]
    private bool _isEditingMinor;

    [ObservableProperty]
    private string _editMinorTitle = string.Empty;

    [ObservableProperty]
    private string _editMinorTarget = string.Empty;

    [ObservableProperty]
    private string _editMinorDescription = string.Empty;

    [ObservableProperty]
    private string _editMinorNotes = string.Empty;

    [ObservableProperty]
    private string _editMinorLink = string.Empty;

    [ObservableProperty]
    private MinorTaskJobType? _editMinorJobType;

    [ObservableProperty]
    private string _editMinorWorkTimeInput = "00:00";

    private MinorTaskItem? _editingMinorItem;

    public ObservableCollection<MinorTaskRowViewModel> MinorTaskRows { get; } = new();

    public MajorTaskDetailViewModel(
        ITaskService taskService,
        ISignalRRealtimeService signalRService,
        INavigationService navigationService,
        IDialogService dialogService,
        IFastAddDialogService fastAddDialogService,
        IUserSession userSession)
    {
        _taskService = taskService;
        _signalRService = signalRService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _fastAddDialogService = fastAddDialogService;
        _userSession = userSession;

        _signalRService.MinorTaskChanged += OnMinorTaskChanged;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _signalRService.MinorTaskChanged -= OnMinorTaskChanged;
        _signalRService.MinorTaskChanged += OnMinorTaskChanged;
        _signalRService.MajorTaskChanged -= OnMajorTaskChanged;
        _signalRService.MajorTaskChanged += OnMajorTaskChanged;

        switch (parameter)
        {
            case MajorTaskItem item:
                ApplyTask(item);
                break;

            case MajorTaskCardModel card:
                ApplyTask(card);
                break;

            case MajorTaskNavigationPayload payload:
                MajorTaskId = payload.MajorTaskId;
                TaskTitle = payload.MajorTaskTitle;
                _trackId = payload.TrackId;
                _trackName = payload.TrackName;
                _projectId = payload.ProjectId;
                _projectName = payload.ProjectName;
                UpdateBreadcrumbs();
                _ = _signalRService.JoinTrackAsync(_trackId);
                _ = LoadMinorTasksAsync();
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

        _navigationService.SetBreadcrumbs(
            new BreadcrumbItem("Tasks Center", () => _navigationService.NavigateTo<TasksViewModel>()),
            new BreadcrumbItem(TaskTitle));

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

        _navigationService.SetBreadcrumbs(
            new BreadcrumbItem("Tasks Center", () => _navigationService.NavigateTo<TasksViewModel>()),
            new BreadcrumbItem(card.Title));

        _ = LoadMinorTasksAsync();
    }

    private void UpdateBreadcrumbs()
    {
        _navigationService.SetBreadcrumbs(
            new BreadcrumbItem("Projects", () => _navigationService.NavigateTo<ProjectsViewModel>()),
            new BreadcrumbItem(_projectName, () => _navigationService.NavigateTo<ProjectDetailViewModel>(_projectId)),
            new BreadcrumbItem(_trackName, () => _navigationService.NavigateTo<TrackDetailViewModel>(
                new TrackNavigationPayload(_trackId, _trackName, _projectId, _projectName))),
            new BreadcrumbItem(TaskTitle));
    }

    public void OnNavigatedFrom()
    {
        _signalRService.MinorTaskChanged -= OnMinorTaskChanged;
        _signalRService.MajorTaskChanged -= OnMajorTaskChanged;
    }

    private void OnMajorTaskChanged(Guid majorTaskId, Guid trackId)
    {
        if (majorTaskId == MajorTaskId || (_trackId != default && trackId == _trackId))
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
            {
                await LoadMinorTasksAsync();
            });
        }
    }

    private void OnMinorTaskChanged(Guid minorTaskId, Guid trackId)
    {
        if (_trackId != default && trackId != _trackId) return;

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
                task.JobType,
                task.Notes,
                task.Link,
                task.Order,
                task.AssignedUserId,
                null));

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
    private async Task OpenFastAddMinorTasksAsync()
    {
        if (MajorTaskId == Guid.Empty) return;
        var imported = await _fastAddDialogService.OpenForMajorTaskAsync(MajorTaskId, TaskTitle);
        if (imported)
        {
            await LoadMinorTasksAsync();
        }
    }

    [RelayCommand]
    private void ToggleAddMinor()
    {
        IsAddingMinor = !IsAddingMinor;
    }

    [RelayCommand]
    private void OpenEditMinorTask(MinorTaskRowViewModel row)
    {
        if (row == null || !row.IsOwner) return;

        var task = row.Item;
        _editingMinorItem = task;
        EditMinorTitle = task.Title;
        EditMinorTarget = task.Target ?? string.Empty;
        EditMinorDescription = task.Description ?? string.Empty;
        EditMinorNotes = task.Notes ?? string.Empty;
        EditMinorLink = task.Link ?? string.Empty;
        EditMinorJobType = task.JobType;
        EditMinorWorkTimeInput = FormatWorkTimeInput(task.WorkTimeSeconds);
        IsEditingMinor = true;
    }

    [RelayCommand]
    private void CloseEditMinorTask()
    {
        IsEditingMinor = false;
        _editingMinorItem = null;
    }

    [RelayCommand]
    private async Task SubmitEditMinorTaskAsync()
    {
        if (_editingMinorItem == null) return;

        if (string.IsNullOrWhiteSpace(EditMinorTitle))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a task title.", ToastType.Warning);
            return;
        }

        long? workSeconds = null;
        var workInput = EditMinorWorkTimeInput.Trim();
        if (!string.IsNullOrWhiteSpace(workInput))
        {
            var parsed = ParseTimeInput(workInput);
            if (parsed <= 0)
            {
                _dialogService.ShowToast("Invalid Value", "Work time must be in HH:MM format.", ToastType.Warning);
                return;
            }
            workSeconds = parsed;
        }

        var task = _editingMinorItem;
        IsBusy = true;
        try
        {
            await _taskService.UpdateMinorTaskAsync(new UpdateMinorTaskRequest(
                task.Id,
                EditMinorTitle.Trim(),
                string.IsNullOrWhiteSpace(EditMinorDescription) ? null : EditMinorDescription.Trim(),
                string.IsNullOrWhiteSpace(EditMinorTarget) ? null : EditMinorTarget.Trim(),
                task.State,
                EditMinorJobType,
                string.IsNullOrWhiteSpace(EditMinorNotes) ? null : EditMinorNotes.Trim(),
                string.IsNullOrWhiteSpace(EditMinorLink) ? null : EditMinorLink.Trim(),
                task.Order,
                task.AssignedUserId,
                workSeconds));

            _editingMinorItem = null;
            IsEditingMinor = false;
            _dialogService.ShowToast("Task Updated", "Sub-task updated.", ToastType.Success);
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

    private static string FormatWorkTimeInput(long? seconds)
    {
        if (seconds is not long s || s <= 0) return "00:00";
        var totalMinutes = (long)Math.Round(s / 60d);
        return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
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
