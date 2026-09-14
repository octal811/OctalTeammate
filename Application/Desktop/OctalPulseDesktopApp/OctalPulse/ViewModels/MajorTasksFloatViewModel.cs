using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class MajorTasksFloatViewModel : ObservableObject
{
    private const string AllProjectsOption = "All Projects";
    private const string AllTracksOption = "All Tracks";
    private const string AllStatesOption = "All States";

    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly IUserSession _userSession;
    private readonly IDialogService _dialogService;
    private readonly List<MajorTaskCardModel> _allCards = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _selectedProject = AllProjectsOption;
    [ObservableProperty] private string _selectedTrack = AllTracksOption;
    [ObservableProperty] private string _selectedState = AllStatesOption;
    [ObservableProperty] private string _searchQuery = string.Empty;

    public ObservableCollection<string> AvailableProjects { get; } = new() { AllProjectsOption };
    public ObservableCollection<string> AvailableTracks { get; } = new() { AllTracksOption };
    public ObservableCollection<string> AvailableStates { get; } = new()
    {
        AllStatesOption,
        nameof(MajorTaskState.InProgress),
        nameof(MajorTaskState.Todo),
        nameof(MajorTaskState.Review),
        nameof(MajorTaskState.Done)
    };

    public ObservableCollection<MajorTaskCardModel> ActiveTasks { get; } = new();

    public MajorTasksFloatViewModel(
        IProjectService projectService,
        ITaskService taskService,
        IUserSession userSession,
        IDialogService dialogService)
    {
        _projectService = projectService;
        _taskService = taskService;
        _userSession = userSession;
        _dialogService = dialogService;
    }

    partial void OnSelectedProjectChanged(string value)
    {
        UpdateAvailableTracks();
        ApplyFilter();
    }

    partial void OnSelectedTrackChanged(string value) => ApplyFilter();
    partial void OnSelectedStateChanged(string value) => ApplyFilter();
    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            _allCards.Clear();
            var userId = _userSession.UserId;
            var paged = await _projectService.GetAllProjectsAsync(1, 50);

            foreach (var p in paged.Items)
            {
                var tracksRes = await _projectService.GetTracksByProjectAsync(p.Id);
                foreach (var t in tracksRes.Tracks)
                {
                    var isMember = t.TrackLeadUserId == userId
                        || t.CurrentUserMembership == MembershipStatus.Approved;
                    if (!isMember) continue;

                    try
                    {
                        var taskRes = await _taskService.GetMajorTasksByTrackAsync(t.Id);
                        foreach (var m in taskRes.MajorTasks)
                        {
                            _allCards.Add(new MajorTaskCardModel
                            {
                                Id = m.Id,
                                TrackId = m.TrackId,
                                Title = m.Title,
                                Description = m.Description,
                                State = m.State,
                                Priority = m.Priority,
                                DueDate = m.DueDate,
                                Progress = m.Progress,
                                AssignedUserId = m.AssignedUserId,
                                CreatedByUserId = m.CreatedByUserId,
                                IsOwner = userId.HasValue && m.CreatedByUserId == userId,
                                CreatedDate = m.CreatedDate,
                                ProjectTitle = p.Title,
                                TrackTitle = t.Name
                            });
                        }
                    }
                    catch { /* skip inaccessible tracks */ }
                }
            }

            UpdateAvailableProjects();
            UpdateAvailableTracks();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Float Tasks", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateAvailableProjects()
    {
        var current = SelectedProject;
        AvailableProjects.Clear();
        AvailableProjects.Add(AllProjectsOption);

        var distinctProjects = _allCards
            .Select(c => c.ProjectTitle)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s);

        foreach (var prj in distinctProjects)
            AvailableProjects.Add(prj);

        SelectedProject = AvailableProjects.Contains(current) ? current : AllProjectsOption;
    }

    private void UpdateAvailableTracks()
    {
        var current = SelectedTrack;
        AvailableTracks.Clear();
        AvailableTracks.Add(AllTracksOption);

        var tracksQuery = _allCards.AsEnumerable();
        if (!string.Equals(SelectedProject, AllProjectsOption, StringComparison.OrdinalIgnoreCase))
        {
            tracksQuery = tracksQuery.Where(c => string.Equals(c.ProjectTitle, SelectedProject, StringComparison.OrdinalIgnoreCase));
        }

        var distinctTracks = tracksQuery
            .Select(c => c.TrackTitle)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s);

        foreach (var trk in distinctTracks)
            AvailableTracks.Add(trk);

        SelectedTrack = AvailableTracks.Contains(current) ? current : AllTracksOption;
    }

    private void ApplyFilter()
    {
        var filtered = _allCards.AsEnumerable();

        if (!string.Equals(SelectedProject, AllProjectsOption, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(c => string.Equals(c.ProjectTitle, SelectedProject, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedTrack, AllTracksOption, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(c => string.Equals(c.TrackTitle, SelectedTrack, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedState, AllStatesOption, StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<MajorTaskState>(SelectedState, true, out var stateVal))
        {
            filtered = filtered.Where(c => c.State == stateVal);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim();
            filtered = filtered.Where(c =>
                c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (c.Description != null && c.Description.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        ActiveTasks.Clear();
        foreach (var item in filtered)
        {
            ActiveTasks.Add(item);
        }
    }
}
