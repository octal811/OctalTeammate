using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class TrackDetailViewModel : ObservableObject, INavigationAware, INavigationFromAware
{
    private readonly ITaskService _taskService;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IUserSession _userSession;

    [ObservableProperty]
    private Guid _trackId;

    [ObservableProperty]
    private string _trackName = "Track Tasks";

    private Guid _projectId;
    private string _projectName = "Projects";

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
    private string _newTaskDetails = string.Empty;

    [ObservableProperty]
    private Priority _newTaskPriority = Priority.Medium;

    [ObservableProperty]
    private DateTime _newTaskDueDate = DateTime.Today.AddDays(7);

    [ObservableProperty]
    private string _newTaskLink = string.Empty;

    // Edit Major Task Modal
    [ObservableProperty]
    private bool _isEditMajorTaskModalOpen;

    [ObservableProperty]
    private string _editTaskTitle = string.Empty;

    [ObservableProperty]
    private string _editTaskDescription = string.Empty;

    [ObservableProperty]
    private string _editTaskDetails = string.Empty;

    [ObservableProperty]
    private Priority _editTaskPriority = Priority.Medium;

    [ObservableProperty]
    private MajorTaskState _editTaskState = MajorTaskState.Todo;

    [ObservableProperty]
    private DateTime? _editTaskDueDate;

    [ObservableProperty]
    private string _editTaskLink = string.Empty;

    private MajorTaskItem? _editingMajorTaskItem;

    public ObservableCollection<MajorTaskRowViewModel> MajorTasks { get; } = new();
    public IReadOnlyList<Priority> AvailablePriorities { get; } = Enum.GetValues<Priority>();
    public IReadOnlyList<MajorTaskState> AvailableTaskStates { get; } = Enum.GetValues<MajorTaskState>();

    public TrackDetailViewModel(
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

        _signalRService.MajorTaskChanged += OnMajorTaskChanged;
        _signalRService.MinorTaskChanged += OnMinorTaskChanged;
    }

    public void OnNavigatedTo(object? parameter)
    {
        switch (parameter)
        {
            case TrackNavigationPayload payload:
                TrackId = payload.TrackId;
                TrackName = payload.TrackName;
                _projectId = payload.ProjectId;
                _projectName = payload.ProjectName;
                break;

            case Guid id:
                TrackId = id;
                TrackName = "Track Tasks";
                break;

            default:
                return;
        }

        UpdateBreadcrumbs();
        _ = _signalRService.JoinTrackAsync(TrackId);
        _ = LoadTasksAsync();
    }

    public void OnNavigatedFrom()
    {
        _signalRService.MajorTaskChanged -= OnMajorTaskChanged;
        _signalRService.MinorTaskChanged -= OnMinorTaskChanged;
    }

    private void UpdateBreadcrumbs()
    {
        _navigationService.SetBreadcrumbs(
            new BreadcrumbItem("Projects", () => _navigationService.NavigateTo<ProjectsViewModel>()),
            new BreadcrumbItem(_projectName, () => _navigationService.NavigateTo<ProjectDetailViewModel>(_projectId)),
            new BreadcrumbItem(TrackName));
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
                MajorTasks.Add(new MajorTaskRowViewModel(t, t.CreatedByUserId == _userSession.UserId));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
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
        NewTaskDetails = string.Empty;
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
                string.IsNullOrWhiteSpace(NewTaskDetails) ? null : NewTaskDetails.Trim(),
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
    private void OpenMajorTaskDetail(MajorTaskRowViewModel row)
    {
        if (row == null) return;
        var task = row.Item;
        _navigationService.NavigateTo<MajorTaskDetailViewModel>(new MajorTaskNavigationPayload(
            task.Id,
            task.Title,
            task.TrackId,
            TrackName,
            _projectId,
            _projectName));
    }

    [RelayCommand]
    private void OpenEditMajorTaskModal(MajorTaskRowViewModel row)
    {
        if (row == null || !row.IsOwner) return;

        var task = row.Item;
        _editingMajorTaskItem = task;
        EditTaskTitle = task.Title;
        EditTaskDescription = task.Description ?? string.Empty;
        EditTaskDetails = task.Details ?? string.Empty;
        EditTaskPriority = task.Priority;
        EditTaskState = task.State;
        EditTaskDueDate = task.DueDate;
        EditTaskLink = task.Link ?? string.Empty;
        IsEditMajorTaskModalOpen = true;
    }

    [RelayCommand]
    private void CloseEditMajorTaskModal()
    {
        IsEditMajorTaskModalOpen = false;
        _editingMajorTaskItem = null;
    }

    [RelayCommand]
    private async Task SubmitEditMajorTaskAsync()
    {
        if (_editingMajorTaskItem == null) return;

        if (string.IsNullOrWhiteSpace(EditTaskTitle))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a task title.", ToastType.Warning);
            return;
        }

        var task = _editingMajorTaskItem;
        IsBusy = true;
        try
        {
            var res = await _taskService.UpdateMajorTaskAsync(new UpdateMajorTaskRequest(
                task.Id,
                EditTaskTitle.Trim(),
                string.IsNullOrWhiteSpace(EditTaskDescription) ? null : EditTaskDescription.Trim(),
                string.IsNullOrWhiteSpace(EditTaskDetails) ? null : EditTaskDetails.Trim(),
                string.IsNullOrWhiteSpace(EditTaskLink) ? null : EditTaskLink.Trim(),
                EditTaskState,
                EditTaskPriority,
                EditTaskDueDate?.ToUniversalTime(),
                task.Order,
                task.AssignedUserId));

            _editingMajorTaskItem = null;
            IsEditMajorTaskModalOpen = false;
            _dialogService.ShowToast("Task Updated", "'" + res.Title + "' updated.", ToastType.Success);
            await LoadTasksAsync();
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
    private async Task DeleteMajorTaskAsync(MajorTaskRowViewModel row)
    {
        if (row == null) return;
        var task = row.Item;

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
