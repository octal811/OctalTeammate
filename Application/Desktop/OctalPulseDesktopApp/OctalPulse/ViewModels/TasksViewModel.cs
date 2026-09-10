using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public record TrackOption(Guid TrackId, Guid ProjectId, string ProjectTitle, string TrackName)
{
    public string DisplayName => $"{ProjectTitle} → {TrackName}";
}

public class MajorTaskCardModel : ObservableObject
{
    public Guid Id { get; set; }
    public Guid TrackId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Details { get; set; }
    public string? Link { get; set; }
    public MajorTaskState State { get; set; }
    public Priority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public int Order { get; set; }
    public Guid? AssignedUserId { get; set; }
    public int Progress { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsOwner { get; set; }

    public string ProjectTitle { get; set; } = string.Empty;
    public string TrackTitle { get; set; } = string.Empty;
    public string ProjectAndTrack => string.IsNullOrWhiteSpace(ProjectTitle) 
        ? TrackTitle 
        : $"{ProjectTitle} • {TrackTitle}";

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasDueDate => DueDate.HasValue;
    public bool HasLink => !string.IsNullOrWhiteSpace(Link);
}

public partial class TasksViewModel : ObservableObject, INavigationAware
{
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IUserSession _userSession;
    private readonly List<MajorTaskCardModel> _allLoadedCards = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedProjectFilter = "All Projects";

    // Columns
    public ObservableCollection<MajorTaskCardModel> TodoTasks { get; } = new();
    public ObservableCollection<MajorTaskCardModel> InProgressTasks { get; } = new();
    public ObservableCollection<MajorTaskCardModel> ReviewTasks { get; } = new();
    public ObservableCollection<MajorTaskCardModel> DoneTasks { get; } = new();

    // Filters & Metadata
    public ObservableCollection<string> AvailableProjects { get; } = new();
    public ObservableCollection<TrackOption> AvailableTracks { get; } = new();
    public IReadOnlyList<Priority> AvailablePriorities { get; } = Enum.GetValues<Priority>();
    public IReadOnlyList<MajorTaskState> AvailableStates { get; } = new[]
    {
        MajorTaskState.Todo,
        MajorTaskState.InProgress,
        MajorTaskState.Review,
        MajorTaskState.Done
    };

    // Modal: Create New Card
    [ObservableProperty]
    private bool _isCreateTaskModalOpen;

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private string _newTaskDescription = string.Empty;

    [ObservableProperty]
    private Priority _newTaskPriority = Priority.Medium;

    [ObservableProperty]
    private MajorTaskState _newTaskState = MajorTaskState.Todo;

    [ObservableProperty]
    private DateTime _newTaskDueDate = DateTime.Today.AddDays(7);

    [ObservableProperty]
    private string _newTaskLink = string.Empty;

    [ObservableProperty]
    private TrackOption? _selectedTrackOption;

    public TasksViewModel(
        IProjectService projectService,
        ITaskService taskService,
        INavigationService navigationService,
        IDialogService dialogService,
        IUserSession userSession)
    {
        _projectService = projectService;
        _taskService = taskService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _userSession = userSession;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _ = LoadAllTasksAsync();
    }

    [RelayCommand]
    public async Task LoadAllTasksAsync()
    {
        IsBusy = true;
        _allLoadedCards.Clear();
        AvailableTracks.Clear();

        var projectNames = new HashSet<string> { "All Projects" };

        try
        {
            var paged = await _projectService.GetAllProjectsAsync(1, 50);
            foreach (var p in paged.Items)
            {
                projectNames.Add(p.Title);

                var tracksRes = await _projectService.GetTracksByProjectAsync(p.Id);
                foreach (var t in tracksRes.Tracks)
                {
                    var opt = new TrackOption(t.Id, p.Id, p.Title, t.Name);
                    AvailableTracks.Add(opt);

                    var taskRes = await _taskService.GetMajorTasksByTrackAsync(t.Id);
                    foreach (var m in taskRes.MajorTasks)
                    {
                        _allLoadedCards.Add(new MajorTaskCardModel
                        {
                            Id = m.Id,
                            TrackId = m.TrackId,
                            Title = m.Title,
                            Description = m.Description,
                            Details = m.Details,
                            Link = m.Link,
                            State = m.State,
                            Priority = m.Priority,
                            DueDate = m.DueDate,
                            Order = m.Order,
                            AssignedUserId = m.AssignedUserId,
                            Progress = m.Progress,
                            CreatedByUserId = m.CreatedByUserId,
                            IsOwner = _userSession.UserId.HasValue && m.CreatedByUserId == _userSession.UserId,
                            CreatedDate = m.CreatedDate,
                            ProjectTitle = p.Title,
                            TrackTitle = t.Name
                        });
                    }
                }
            }

            AvailableProjects.Clear();
            foreach (var name in projectNames)
            {
                AvailableProjects.Add(name);
            }

            if (!AvailableProjects.Contains(SelectedProjectFilter))
            {
                SelectedProjectFilter = "All Projects";
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Load Tasks Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedProjectFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allLoadedCards.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedProjectFilter) && SelectedProjectFilter != "All Projects")
        {
            filtered = filtered.Where(c => string.Equals(c.ProjectTitle, SelectedProjectFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.Trim();
            filtered = filtered.Where(c =>
                c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(c.Description) && c.Description.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                c.ProjectTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.TrackTitle.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        TodoTasks.Clear();
        InProgressTasks.Clear();
        ReviewTasks.Clear();
        DoneTasks.Clear();

        foreach (var card in filtered)
        {
            switch (card.State)
            {
                case MajorTaskState.Todo:
                    TodoTasks.Add(card);
                    break;
                case MajorTaskState.InProgress:
                    InProgressTasks.Add(card);
                    break;
                case MajorTaskState.Review:
                    ReviewTasks.Add(card);
                    break;
                case MajorTaskState.Done:
                case MajorTaskState.OnHold:
                    DoneTasks.Add(card);
                    break;
                default:
                    TodoTasks.Add(card);
                    break;
            }
        }
    }

    // Modal Operations
    [RelayCommand]
    private void OpenCreateTaskModal(string? initialColumn = null)
    {
        NewTaskTitle = string.Empty;
        NewTaskDescription = string.Empty;
        NewTaskPriority = Priority.Medium;
        NewTaskDueDate = DateTime.Today.AddDays(7);
        NewTaskLink = string.Empty;

        if (!string.IsNullOrWhiteSpace(initialColumn) && Enum.TryParse<MajorTaskState>(initialColumn, true, out var parsedState))
        {
            NewTaskState = parsedState;
        }
        else
        {
            NewTaskState = MajorTaskState.Todo;
        }

        if (SelectedTrackOption == null && AvailableTracks.Count > 0)
        {
            SelectedTrackOption = AvailableTracks[0];
        }

        IsCreateTaskModalOpen = true;
    }

    [RelayCommand]
    private void CloseCreateTaskModal()
    {
        IsCreateTaskModalOpen = false;
    }

    [RelayCommand]
    private async Task SubmitCreateTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a task title.", ToastType.Warning);
            return;
        }

        if (SelectedTrackOption == null)
        {
            _dialogService.ShowToast("Validation Error", "Please select a Project and Track for this task.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _taskService.CreateMajorTaskAsync(new CreateMajorTaskRequest(
                SelectedTrackOption.TrackId,
                NewTaskTitle.Trim(),
                string.IsNullOrWhiteSpace(NewTaskDescription) ? null : NewTaskDescription.Trim(),
                null,
                string.IsNullOrWhiteSpace(NewTaskLink) ? null : NewTaskLink.Trim(),
                NewTaskState,
                NewTaskPriority,
                NewTaskDueDate.ToUniversalTime(),
                0,
                null));

            IsCreateTaskModalOpen = false;
            _dialogService.ShowToast("Card Added", $"'{res.Title}' added to {NewTaskState}.", ToastType.Success);
            await LoadAllTasksAsync();
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

    // State Transition Commands
    [RelayCommand]
    private async Task MoveToTodoAsync(MajorTaskCardModel card) => await TransitionCardStateAsync(card, MajorTaskState.Todo);

    [RelayCommand]
    private async Task MoveToInProgressAsync(MajorTaskCardModel card) => await TransitionCardStateAsync(card, MajorTaskState.InProgress);

    [RelayCommand]
    private async Task MoveToReviewAsync(MajorTaskCardModel card) => await TransitionCardStateAsync(card, MajorTaskState.Review);

    [RelayCommand]
    private async Task MoveToDoneAsync(MajorTaskCardModel card) => await TransitionCardStateAsync(card, MajorTaskState.Done);

    private async Task TransitionCardStateAsync(MajorTaskCardModel card, MajorTaskState newState)
    {
        if (card == null || card.State == newState) return;

        IsBusy = true;
        try
        {
            await _taskService.UpdateMajorTaskAsync(new UpdateMajorTaskRequest(
                card.Id,
                card.Title,
                card.Description,
                card.Details,
                card.Link,
                newState,
                card.Priority,
                card.DueDate,
                card.Order,
                card.AssignedUserId));

            _dialogService.ShowToast("Task Updated", $"'{card.Title}' moved to {newState}.", ToastType.Info);
            await LoadAllTasksAsync();
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

    // Card Actions
    [RelayCommand]
    private void OpenTaskDetail(MajorTaskCardModel card)
    {
        if (card == null) return;
        _navigationService.NavigateTo<MajorTaskDetailViewModel>(card);
    }

    [RelayCommand]
    private async Task DeleteTaskAsync(MajorTaskCardModel card)
    {
        if (card == null) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Delete Task Card",
            $"Are you sure you want to delete '{card.Title}'?",
            "Delete",
            "Cancel");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            await _taskService.DeleteMajorTaskAsync(card.Id);
            _dialogService.ShowToast("Card Deleted", $"'{card.Title}' was deleted.", ToastType.Info);
            await LoadAllTasksAsync();
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
}
