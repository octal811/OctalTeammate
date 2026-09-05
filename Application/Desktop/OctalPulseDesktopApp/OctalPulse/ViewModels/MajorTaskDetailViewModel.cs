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
    private readonly ILocalCacheService _localCache;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

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

    // Create Minor Task
    [ObservableProperty]
    private string _newMinorTitle = string.Empty;

    [ObservableProperty]
    private string _newMinorTarget = string.Empty;

    [ObservableProperty]
    private string _newMinorLink = string.Empty;

    public ObservableCollection<MinorTaskItem> MinorTasks { get; } = new();

    public MajorTaskDetailViewModel(
        ITaskService taskService,
        ILocalCacheService localCache,
        ISignalRRealtimeService signalRService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _taskService = taskService;
        _localCache = localCache;
        _signalRService = signalRService;
        _navigationService = navigationService;
        _dialogService = dialogService;

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
            MinorTasks.Clear();

            var doneCount = 0;
            foreach (var m in res.MinorTasks)
            {
                MinorTasks.Add(m);
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

            // Cache in SQLite
            await _localCache.SaveMinorTasksAsync(MajorTaskId, res.MinorTasks.Select(m => new CachedMinorTask
            {
                Id = m.Id,
                MajorTaskId = MajorTaskId,
                Title = m.Title,
                Description = m.Description,
                Target = m.Target,
                State = m.State,
                Notes = m.Notes,
                Link = m.Link,
                Order = m.Order,
                AssignedUserId = m.AssignedUserId,
                CreatedByUserId = m.CreatedByUserId,
                CreatedDate = m.CreatedDate
            }));
        }
        catch
        {
            // SQLite cache
            var cached = await _localCache.GetCachedMinorTasksAsync(MajorTaskId);
            MinorTasks.Clear();
            var doneCount = 0;
            foreach (var c in cached)
            {
                MinorTasks.Add(new MinorTaskItem(
                    c.Id,
                    c.MajorTaskId,
                    c.Title,
                    c.Description,
                    c.Target,
                    c.State,
                    c.Notes,
                    c.Link,
                    c.Order,
                    c.AssignedUserId,
                    c.CreatedByUserId,
                    false,
                    c.CreatedDate));

                if (c.State == MinorTaskState.Done) doneCount++;
            }
            ProgressPercentage = cached.Count > 0 ? (doneCount * 100) / cached.Count : 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleTaskDoneAsync(MinorTaskItem task)
    {
        if (task == null) return;

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
                null,
                string.IsNullOrWhiteSpace(NewMinorTarget) ? null : NewMinorTarget.Trim(),
                MinorTaskState.Todo,
                null,
                string.IsNullOrWhiteSpace(NewMinorLink) ? null : NewMinorLink.Trim(),
                MinorTasks.Count + 1,
                null));

            NewMinorTitle = string.Empty;
            NewMinorTarget = string.Empty;
            NewMinorLink = string.Empty;

            _dialogService.ShowToast("Task Added", "Sub-task added to checklist.", ToastType.Success);
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
    private async Task DeleteMinorTaskAsync(MinorTaskItem task)
    {
        if (task == null) return;

        IsBusy = true;
        try
        {
            await _taskService.DeleteMinorTaskAsync(task.Id);
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
