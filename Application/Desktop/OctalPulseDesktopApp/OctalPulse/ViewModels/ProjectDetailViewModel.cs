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

public partial class ProjectDetailViewModel : ObservableObject, INavigationAware
{
    private readonly IProjectService _projectService;
    private readonly ITrackService _trackService;
    private readonly IEventService _eventService;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IUserSession _userSession;

    [ObservableProperty]
    private Guid _projectId;

    [ObservableProperty]
    private GetProjectByIdResponse? _project;

    [ObservableProperty]
    private string _selectedTab = "Tracks"; // "Tracks", "Members", "Events"

    [ObservableProperty]
    private bool _isCreator;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ProjectStatus _currentStatus = ProjectStatus.Active;

    public IReadOnlyList<ProjectStatus> AvailableStatuses { get; } = Enum.GetValues<ProjectStatus>();

    // Edit Project Modal
    [ObservableProperty]
    private bool _isEditProjectModalOpen;

    [ObservableProperty]
    private string _editProjectTitle = string.Empty;

    [ObservableProperty]
    private string _editProjectDescription = string.Empty;

    [ObservableProperty]
    private ProjectStatus _editProjectStatus = ProjectStatus.Active;

    // Create Track Modal
    [ObservableProperty]
    private bool _isCreateTrackModalOpen;

    [ObservableProperty]
    private string _newTrackName = string.Empty;

    [ObservableProperty]
    private string _newTrackDescription = string.Empty;

    // Create Event Modal
    [ObservableProperty]
    private bool _isCreateEventModalOpen;

    [ObservableProperty]
    private string _newEventTitle = string.Empty;

    [ObservableProperty]
    private string _newEventDescription = string.Empty;

    [ObservableProperty]
    private EventType _newEventType = EventType.Meeting;

    [ObservableProperty]
    private DateTime _newEventDate = DateTime.Today.AddDays(1);

    public ObservableCollection<TrackSummaryItem> Tracks { get; } = new();
    public ObservableCollection<ProjectMemberItem> Members { get; } = new();
    public ObservableCollection<EventItem> Events { get; } = new();

    public IReadOnlyList<EventType> AvailableEventTypes { get; } = Enum.GetValues<EventType>();

    public ProjectDetailViewModel(
        IProjectService projectService,
        ITrackService trackService,
        IEventService eventService,
        ISignalRRealtimeService signalRService,
        INavigationService navigationService,
        IDialogService dialogService,
        IUserSession userSession)
    {
        _projectService = projectService;
        _trackService = trackService;
        _eventService = eventService;
        _signalRService = signalRService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _userSession = userSession;

        _signalRService.ProjectChanged += OnProjectChanged;
        _signalRService.TrackChanged += OnTrackChanged;
        _signalRService.EventChanged += OnEventChanged;
    }

    public void OnNavigatedTo(object? parameter)
    {
        if (parameter is Guid id)
        {
            ProjectId = id;
            _ = _signalRService.JoinProjectAsync(id);
            _ = LoadProjectDataAsync();
        }
    }

    private void OnProjectChanged(Guid id)
    {
        if (id == ProjectId)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
            {
                await LoadProjectDataAsync();
            });
        }
    }

    private void OnTrackChanged(Guid trackId, Guid projectId)
    {
        if (projectId == ProjectId)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
            {
                await LoadTracksAsync();
                _dialogService.ShowToast("Tracks Updated", "Track list was updated in real time.", ToastType.Info);
            });
        }
    }

    private void OnEventChanged(Guid eventId, Guid projectId)
    {
        if (projectId == ProjectId)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
            {
                await LoadEventsAsync();
            });
        }
    }

    [RelayCommand]
    private async Task LoadProjectDataAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            Project = await _projectService.GetProjectByIdAsync(ProjectId);
            CurrentStatus = Project.Status;
            IsCreator = _userSession.UserId.HasValue && Project.Creator.UserId == _userSession.UserId.Value;

            Members.Clear();
            foreach (var m in Project.Members)
            {
                Members.Add(m);
            }

            await LoadTracksAsync();
            await LoadEventsAsync();
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

    private async Task LoadTracksAsync()
    {
        var res = await _projectService.GetTracksByProjectAsync(ProjectId);
        Tracks.Clear();
        foreach (var t in res.Tracks)
        {
            Tracks.Add(t);
        }
    }

    private async Task LoadEventsAsync()
    {
        var now = DateTime.UtcNow;
        var res = await _eventService.GetEventsByMonthAsync(ProjectId, now.Year, now.Month);
        Events.Clear();
        foreach (var ev in res.Events)
        {
            Events.Add(ev);
        }
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        SelectedTab = tab;
    }

    [RelayCommand]
    private void OpenCreateTrackModal()
    {
        NewTrackName = string.Empty;
        NewTrackDescription = string.Empty;
        IsCreateTrackModalOpen = true;
    }

    [RelayCommand]
    private void CloseCreateTrackModal()
    {
        IsCreateTrackModalOpen = false;
    }

    [RelayCommand]
    private async Task SubmitCreateTrackAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTrackName))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a track name.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _trackService.CreateTrackAsync(new CreateTrackRequest(
                ProjectId,
                NewTrackName.Trim(),
                string.IsNullOrWhiteSpace(NewTrackDescription) ? null : NewTrackDescription.Trim()));

            IsCreateTrackModalOpen = false;
            _dialogService.ShowToast("Track Created", $"Track '{res.Name}' created successfully.", ToastType.Success);
            await LoadTracksAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Create Track Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenCreateEventModal()
    {
        NewEventTitle = string.Empty;
        NewEventDescription = string.Empty;
        NewEventType = EventType.Meeting;
        NewEventDate = DateTime.Today.AddDays(1);
        IsCreateEventModalOpen = true;
    }

    [RelayCommand]
    private void CloseCreateEventModal()
    {
        IsCreateEventModalOpen = false;
    }

    [RelayCommand]
    private async Task SubmitCreateEventAsync()
    {
        if (string.IsNullOrWhiteSpace(NewEventTitle))
        {
            _dialogService.ShowToast("Validation Error", "Please provide an event title.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            await _eventService.CreateEventAsync(new CreateEventRequest(
                ProjectId,
                NewEventTitle.Trim(),
                string.IsNullOrWhiteSpace(NewEventDescription) ? null : NewEventDescription.Trim(),
                NewEventType,
                NewEventDate.ToUniversalTime(),
                NewEventDate.AddHours(1).ToUniversalTime(),
                null,
                null,
                false,
                null,
                null));

            IsCreateEventModalOpen = false;
            _dialogService.ShowToast("Event Created", "Event added to project calendar.", ToastType.Success);
            await LoadEventsAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Create Event Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnCurrentStatusChanged(ProjectStatus value)
    {
        if (Project == null || value == Project.Status) return;
        _ = UpdateProjectStatusAsync(value);
    }

    [RelayCommand]
    private async Task UpdateProjectStatusAsync(ProjectStatus newStatus)
    {
        if (Project == null) return;
        IsBusy = true;
        try
        {
            var res = await _projectService.UpdateProjectAsync(new UpdateProjectRequest(
                Project.Id,
                Project.Title,
                Project.Description,
                newStatus));

            Project = Project with { Status = res.Status };
            CurrentStatus = res.Status;
            _dialogService.ShowToast("Status Updated", $"Project status changed to {res.Status}.", ToastType.Success);
        }
        catch (Exception ex)
        {
            CurrentStatus = Project.Status;
            _dialogService.ShowToast("Update Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenEditProjectModal()
    {
        if (Project == null) return;
        EditProjectTitle = Project.Title;
        EditProjectDescription = Project.Description ?? string.Empty;
        EditProjectStatus = Project.Status;
        IsEditProjectModalOpen = true;
    }

    [RelayCommand]
    private void CloseEditProjectModal()
    {
        IsEditProjectModalOpen = false;
    }

    [RelayCommand]
    private async Task SubmitEditProjectAsync()
    {
        if (Project == null) return;
        if (string.IsNullOrWhiteSpace(EditProjectTitle))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a project title.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _projectService.UpdateProjectAsync(new UpdateProjectRequest(
                Project.Id,
                EditProjectTitle.Trim(),
                string.IsNullOrWhiteSpace(EditProjectDescription) ? null : EditProjectDescription.Trim(),
                EditProjectStatus));

            Project = Project with
            {
                Title = res.Title,
                Description = res.Description,
                Status = res.Status
            };
            CurrentStatus = res.Status;
            IsEditProjectModalOpen = false;
            _dialogService.ShowToast("Project Updated", "Project details updated successfully.", ToastType.Success);
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
    private void OpenTrack(TrackSummaryItem track)
    {
        if (track == null) return;
        _navigationService.NavigateTo<TrackDetailViewModel>(track.Id);
    }

    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        if (!IsCreator) return;
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Delete Project",
            $"Are you sure you want to delete '{Project?.Title}'? This will cascade-delete all tracks and tasks under it.",
            "Delete",
            "Cancel");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            await _projectService.DeleteProjectAsync(ProjectId);
            _dialogService.ShowToast("Project Deleted", "The project has been deleted.", ToastType.Info);
            _navigationService.NavigateTo<ProjectsViewModel>();
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
        _navigationService.NavigateTo<ProjectsViewModel>();
    }
}
