using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public class MinorTaskCardModel : ObservableObject
{
    public Guid Id { get; set; }
    public Guid MajorTaskId { get; set; }
    public string MajorTaskTitle { get; set; } = string.Empty;
    public Guid TrackId { get; set; }
    public string TrackTitle { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public MinorTaskState State { get; set; }
    public MinorTaskJobType? JobType { get; set; }
    public long WorkTimeSeconds { get; set; }

    public string FormattedWorkTime => TimeSpan.FromSeconds(WorkTimeSeconds).ToString(@"hh\:mm\:ss");
    public string ProjectAndTrack => $"{ProjectTitle} • {TrackTitle}";
}

public partial class MinorTasksFloatViewModel : ObservableObject
{
    private const string AllProjectsOption = "All Projects";
    private const string AllTracksOption = "All Tracks";
    private const string AllMajorTasksOption = "All Major Tasks";
    private const string AllStatesOption = "All States";

    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly IUserSession _userSession;
    private readonly IDialogService _dialogService;
    private readonly List<MinorTaskCardModel> _allCards = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _selectedProject = AllProjectsOption;
    [ObservableProperty] private string _selectedTrack = AllTracksOption;
    [ObservableProperty] private string _selectedMajorTask = AllMajorTasksOption;
    [ObservableProperty] private string _selectedState = AllStatesOption;
    [ObservableProperty] private string _searchQuery = string.Empty;

    public ObservableCollection<string> AvailableProjects { get; } = new() { AllProjectsOption };
    public ObservableCollection<string> AvailableTracks { get; } = new() { AllTracksOption };
    public ObservableCollection<string> AvailableMajorTasks { get; } = new() { AllMajorTasksOption };
    public ObservableCollection<string> AvailableStates { get; } = new()
    {
        AllStatesOption,
        nameof(MinorTaskState.InProgress),
        nameof(MinorTaskState.Todo),
        nameof(MinorTaskState.Done),
        nameof(MinorTaskState.Canceled),
        nameof(MinorTaskState.Failed)
    };

    public ObservableCollection<MinorTaskCardModel> MyMinorTasks { get; } = new();

    public MinorTasksFloatViewModel(
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
        UpdateAvailableMajorTasks();
        ApplyFilter();
    }

    partial void OnSelectedTrackChanged(string value)
    {
        UpdateAvailableMajorTasks();
        ApplyFilter();
    }

    partial void OnSelectedMajorTaskChanged(string value) => ApplyFilter();
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
                        var majorRes = await _taskService.GetMajorTasksByTrackAsync(t.Id);
                        foreach (var major in majorRes.MajorTasks)
                        {
                            try
                            {
                                var minorRes = await _taskService.GetMinorTasksByMajorTaskAsync(major.Id);
                                foreach (var minor in minorRes.MinorTasks)
                                {
                                    _allCards.Add(new MinorTaskCardModel
                                    {
                                        Id = minor.Id,
                                        MajorTaskId = major.Id,
                                        MajorTaskTitle = major.Title,
                                        TrackId = t.Id,
                                        TrackTitle = t.Name,
                                        ProjectId = p.Id,
                                        ProjectTitle = p.Title,
                                        Title = minor.Title,
                                        State = minor.State,
                                        JobType = minor.JobType,
                                        WorkTimeSeconds = minor.WorkTimeSeconds ?? 0
                                    });
                                }
                            }
                            catch { /* skip minor task errors */ }
                        }
                    }
                    catch { /* skip */ }
                }
            }

            UpdateAvailableProjects();
            UpdateAvailableTracks();
            UpdateAvailableMajorTasks();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Float Minor Tasks", ex.Message, ToastType.Error);
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

        var query = _allCards.AsEnumerable();
        if (!string.Equals(SelectedProject, AllProjectsOption, StringComparison.OrdinalIgnoreCase))
            query = query.Where(c => string.Equals(c.ProjectTitle, SelectedProject, StringComparison.OrdinalIgnoreCase));

        var distinctTracks = query
            .Select(c => c.TrackTitle)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s);

        foreach (var trk in distinctTracks)
            AvailableTracks.Add(trk);

        SelectedTrack = AvailableTracks.Contains(current) ? current : AllTracksOption;
    }

    private void UpdateAvailableMajorTasks()
    {
        var current = SelectedMajorTask;
        AvailableMajorTasks.Clear();
        AvailableMajorTasks.Add(AllMajorTasksOption);

        var query = _allCards.AsEnumerable();
        if (!string.Equals(SelectedProject, AllProjectsOption, StringComparison.OrdinalIgnoreCase))
            query = query.Where(c => string.Equals(c.ProjectTitle, SelectedProject, StringComparison.OrdinalIgnoreCase));

        if (!string.Equals(SelectedTrack, AllTracksOption, StringComparison.OrdinalIgnoreCase))
            query = query.Where(c => string.Equals(c.TrackTitle, SelectedTrack, StringComparison.OrdinalIgnoreCase));

        var distinctMajor = query
            .Select(c => c.MajorTaskTitle)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s);

        foreach (var m in distinctMajor)
            AvailableMajorTasks.Add(m);

        SelectedMajorTask = AvailableMajorTasks.Contains(current) ? current : AllMajorTasksOption;
    }

    private void ApplyFilter()
    {
        var filtered = _allCards.AsEnumerable();

        if (!string.Equals(SelectedProject, AllProjectsOption, StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(c => string.Equals(c.ProjectTitle, SelectedProject, StringComparison.OrdinalIgnoreCase));

        if (!string.Equals(SelectedTrack, AllTracksOption, StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(c => string.Equals(c.TrackTitle, SelectedTrack, StringComparison.OrdinalIgnoreCase));

        if (!string.Equals(SelectedMajorTask, AllMajorTasksOption, StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(c => string.Equals(c.MajorTaskTitle, SelectedMajorTask, StringComparison.OrdinalIgnoreCase));

        if (!string.Equals(SelectedState, AllStatesOption, StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<MinorTaskState>(SelectedState, true, out var stateVal))
            filtered = filtered.Where(c => c.State == stateVal);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim();
            filtered = filtered.Where(c =>
                c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.MajorTaskTitle.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        MyMinorTasks.Clear();
        foreach (var item in filtered)
            MyMinorTasks.Add(item);
    }
}
