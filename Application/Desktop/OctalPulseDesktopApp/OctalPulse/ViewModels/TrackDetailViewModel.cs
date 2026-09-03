using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class TrackDetailViewModel : ObservableObject, INavigationAware
{
    private readonly ITaskService _taskService;
    private readonly ILocalCacheService _localCache;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IUserSession _userSession;

    [ObservableProperty]
    private Guid _trackId;

    [ObservableProperty]
    private string _trackName = "Track Tasks";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    // Create Major Task Modal
    [ObservableProperty]
    private bool _isCreateMajorTaskModalOpen;

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private string _newTaskDescription = string.Empty;

    [ObservableProperty]
    private Priority _newTaskPriority = Priority.Medium;

    [ObservableProperty]
    private DateTime _newTaskDueDate = DateTime.Today.AddDays(7);

    [ObservableProperty]
    private string _newTaskLink = string.Empty;

    public ObservableCollection<MajorTaskItem> MajorTasks { get; } = new();
    public IReadOnlyList<Priority> AvailablePriorities { get; } = Enum.GetValues<Priority>();

    public TrackDetailViewModel(
        ITaskService taskService,
        ILocalCacheService localCache,
        ISignalRRealtimeService signalRService,
        INavigationService navigationService,
        IDialogService dialogService,
        IUserSession userSession)
    {
        _taskService = taskService;
        _localCache = localCache;
        _signalRService = signalRService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _userSession = userSession;

        _signalRService.MajorTaskChanged += OnMajorTaskChanged;
        _signalRService.MinorTaskChanged += OnMinorTaskChanged;
    }

    public void OnNavigatedTo(object? parameter)
    {
        if (parameter is Guid id)
        {
            TrackId = id;
            _ = _signalRService.JoinTrackAsync(id);
            _ = LoadTasksAsync();
        }
    }

    private void OnMajorTaskChanged(Guid taskId, Guid trackId)
    {
        if (trackId == TrackId)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
            {
                await LoadTasksAsync();
                _dialogService.ShowToast("Realtime Task Update", "Major tasks were updated.", ToastType.Info);
            });
        }
    }

    private void OnMinorTaskChanged(Guid minorTaskId, Guid trackId)
    {
        if (trackId == TrackId)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
            {
                await LoadTasksAsync();
            });
        }
    }

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var res = await _taskService.GetMajorTasksByTrackAsync(TrackId);
            MajorTasks.Clear();
            foreach (var t in res.MajorTasks)
            {
                MajorTasks.Add(t);
            }

            // Cache locally
            await _localCache.SaveMajorTasksAsync(TrackId, res.MajorTasks.Select(t => new CachedMajorTask
            {
                Id = t.Id,
                TrackId = TrackId,
                Title = t.Title,
                Description = t.Description,
                Details = t.Details,
                Link = t.Link,
                State = t.State,
                Priority = t.Priority,
                DueDate = t.DueDate,
                Order = t.Order,
                AssignedUserId = t.AssignedUserId,
                Progress = t.Progress,
                CreatedByUserId = t.CreatedByUserId,
                CreatedDate = t.CreatedDate
            }));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            // SQLite cache
            var cached = await _localCache.GetCachedMajorTasksAsync(TrackId);
            MajorTasks.Clear();
            foreach (var c in cached)
            {
                MajorTasks.Add(new MajorTaskItem(
                    c.Id,
                    c.TrackId,
                    c.Title,
                    c.Description,
                    c.Details,
                    c.Link,
                    c.State,
                    c.Priority,
                    c.DueDate,
                    c.Order,
                    c.AssignedUserId,
                    c.Progress,
                    c.CreatedByUserId,
                    null,
                    c.CreatedDate));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenCreateMajorTaskModal()
    {
        NewTaskTitle = string.Empty;
        NewTaskDescription = string.Empty;
        NewTaskPriority = Priority.Medium;
        NewTaskDueDate = DateTime.Today.AddDays(7);
        NewTaskLink = string.Empty;
        IsCreateMajorTaskModalOpen = true;
    }

    [RelayCommand]
    private void CloseCreateMajorTaskModal()
    {
        IsCreateMajorTaskModalOpen = false;
    }

    [RelayCommand]
    private async Task SubmitCreateMajorTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a task title.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _taskService.CreateMajorTaskAsync(new CreateMajorTaskRequest(
                TrackId,
                NewTaskTitle.Trim(),
                string.IsNullOrWhiteSpace(NewTaskDescription) ? null : NewTaskDescription.Trim(),
                null,
                string.IsNullOrWhiteSpace(NewTaskLink) ? null : NewTaskLink.Trim(),
                MajorTaskState.Todo,
                NewTaskPriority,
                NewTaskDueDate.ToUniversalTime(),
                MajorTasks.Count + 1,
                null));

            IsCreateMajorTaskModalOpen = false;
            _dialogService.ShowToast("Major Task Created", $"'{res.Title}' added to track.", ToastType.Success);
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Create Task Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenMajorTaskDetail(MajorTaskItem task)
    {
        if (task == null) return;
        _navigationService.NavigateTo<MajorTaskDetailViewModel>(task.Id);
    }

    [RelayCommand]
    private async Task DeleteMajorTaskAsync(MajorTaskItem task)
    {
        if (task == null) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Delete Major Task",
            $"Are you sure you want to delete '{task.Title}'?",
            "Delete",
            "Cancel");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            await _taskService.DeleteMajorTaskAsync(task.Id);
            _dialogService.ShowToast("Task Deleted", "Major task has been deleted.", ToastType.Info);
            await LoadTasksAsync();
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
    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
