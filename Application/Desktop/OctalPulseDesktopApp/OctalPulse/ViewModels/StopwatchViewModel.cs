using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class StopwatchViewModel : ObservableObject, INavigationAware
{
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly IUserSession _userSession;
    private readonly IDialogService _dialogService;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isElapsed;
    [ObservableProperty] private TimeSpan _elapsed = TimeSpan.Zero;
    [ObservableProperty] private string _elapsedDisplay = "00:00:00";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ProjectSummaryItem> Projects { get; } = new();
    public ObservableCollection<TrackSummaryItem> Tracks { get; } = new();
    public ObservableCollection<MajorTaskItem> MajorTasks { get; } = new();
    public ObservableCollection<MinorTaskItem> MinorTasks { get; } = new();

    [ObservableProperty] private ProjectSummaryItem? _selectedProject;
    [ObservableProperty] private TrackSummaryItem? _selectedTrack;
    [ObservableProperty] private MajorTaskItem? _selectedMajorTask;
    [ObservableProperty] private MinorTaskItem? _selectedMinorTask;

    [ObservableProperty] private bool _isCreatingMinor;
    [ObservableProperty] private string _newMinorTitle = string.Empty;
    [ObservableProperty] private string _newMinorDescription = string.Empty;
    [ObservableProperty] private string _newMinorTarget = string.Empty;
    [ObservableProperty] private string _newMinorNotes = string.Empty;
    [ObservableProperty] private string _newMinorLink = string.Empty;

    public StopwatchViewModel(
        IProjectService projectService,
        ITaskService taskService,
        IUserSession userSession,
        IDialogService dialogService)
    {
        _projectService = projectService;
        _taskService = taskService;
        _userSession = userSession;
        _dialogService = dialogService;

        _timer.Tick += (_, _) => Elapsed = _stopwatch.Elapsed;
    }

    public async void OnNavigatedTo(object? parameter)
    {
        await LoadProjectsAsync();
    }

    partial void OnElapsedChanged(TimeSpan value)
    {
        IsElapsed = value.TotalSeconds > 0;
        ElapsedDisplay = value.TotalHours >= 24
            ? $"{(int)value.TotalHours / 24}d {value:hh\\:mm\\:ss}"
            : value.ToString("hh\\:mm\\:ss");
    }

    partial void OnSelectedProjectChanged(ProjectSummaryItem? value)
    {
        if (value != null)
            _ = LoadTracksAsync(value.Id);
        else
            Tracks.Clear();
    }

    partial void OnSelectedTrackChanged(TrackSummaryItem? value)
    {
        if (value != null)
            _ = LoadMajorTasksAsync(value.Id);
        else
            MajorTasks.Clear();
    }

    partial void OnSelectedMajorTaskChanged(MajorTaskItem? value)
    {
        if (value != null)
            _ = LoadMinorTasksAsync(value.Id);
        else
            MinorTasks.Clear();
    }

    [RelayCommand]
    private void ToggleRunning()
    {
        if (IsRunning)
        {
            _stopwatch.Stop();
            _timer.Stop();
        }
        else
        {
            _stopwatch.Start();
            _timer.Start();
        }
        IsRunning = _stopwatch.IsRunning;
    }

    [RelayCommand]
    private void ResetTimer()
    {
        _stopwatch.Reset();
        _timer.Stop();
        IsRunning = false;
        Elapsed = TimeSpan.Zero;
        ClearSelections();
    }

    [RelayCommand]
    private void ToggleCreateMinor()
    {
        IsCreatingMinor = !IsCreatingMinor;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Elapsed.TotalSeconds < 1)
        {
            _dialogService.ShowToast("Nothing to save", "Start the timer first and let it run.", ToastType.Warning);
            return;
        }

        if (SelectedMajorTask == null)
        {
            _dialogService.ShowToast("Select a task", "Choose a project, track, and major task.", ToastType.Warning);
            return;
        }

        Guid? targetMinorId = null;

        if (IsCreatingMinor)
        {
            if (string.IsNullOrWhiteSpace(NewMinorTitle))
            {
                _dialogService.ShowToast("Task title required", "Enter a title for the new minor task.", ToastType.Warning);
                return;
            }

            try
            {
                var created = await _taskService.CreateMinorTaskAsync(new CreateMinorTaskRequest(
                    SelectedMajorTask.Id,
                    NewMinorTitle.Trim(),
                    string.IsNullOrWhiteSpace(NewMinorDescription) ? null : NewMinorDescription.Trim(),
                    string.IsNullOrWhiteSpace(NewMinorTarget) ? null : NewMinorTarget.Trim(),
                    MinorTaskState.Todo,
                    string.IsNullOrWhiteSpace(NewMinorNotes) ? null : NewMinorNotes.Trim(),
                    string.IsNullOrWhiteSpace(NewMinorLink) ? null : NewMinorLink.Trim(),
                    1,
                    null));

                targetMinorId = created.Id;
            }
            catch (Exception ex)
            {
                _dialogService.ShowToast("Create task failed", ex.Message, ToastType.Error);
                return;
            }
        }
        else if (SelectedMinorTask != null)
        {
            targetMinorId = SelectedMinorTask.Id;
        }

        if (targetMinorId == null)
        {
            _dialogService.ShowToast("Select or create a minor task", "Pick an existing task or create a new one before saving.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var elapsed = Elapsed;
            var seconds = (long)elapsed.TotalSeconds;
            await _taskService.AddMinorTaskWorkTimeAsync(new AddMinorTaskWorkTimeRequest(targetMinorId.Value, seconds));

            _stopwatch.Reset();
            _timer.Stop();
            IsRunning = false;
            Elapsed = TimeSpan.Zero;
            ClearSelections();

            _dialogService.ShowToast("Time saved", $"Added {FormatElapsed(elapsed)} to the task.", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Save failed", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearSelections()
    {
        IsCreatingMinor = false;
        NewMinorTitle = string.Empty;
        NewMinorDescription = string.Empty;
        NewMinorTarget = string.Empty;
        NewMinorNotes = string.Empty;
        NewMinorLink = string.Empty;
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            var res = await _projectService.GetAllProjectsAsync(1, 100);
            Projects.Clear();
            foreach (var p in res.Items)
                Projects.Add(p);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Load error", $"Could not load projects: {ex.Message}", ToastType.Error);
        }
    }

    private async Task LoadTracksAsync(Guid projectId)
    {
        SelectedTrack = null;
        MajorTasks.Clear();
        MinorTasks.Clear();

        try
        {
            var res = await _projectService.GetTracksByProjectAsync(projectId);
            Tracks.Clear();
            foreach (var t in res.Tracks)
                Tracks.Add(t);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Load error", $"Could not load tracks: {ex.Message}", ToastType.Error);
        }
    }

    private async Task LoadMajorTasksAsync(Guid trackId)
    {
        SelectedMajorTask = null;
        MinorTasks.Clear();

        try
        {
            var res = await _taskService.GetMajorTasksByTrackAsync(trackId);
            MajorTasks.Clear();
            foreach (var m in res.MajorTasks)
                MajorTasks.Add(m);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Load error", $"Could not load tasks: {ex.Message}", ToastType.Error);
        }
    }

    private async Task LoadMinorTasksAsync(Guid majorTaskId)
    {
        SelectedMinorTask = null;
        IsCreatingMinor = false;

        try
        {
            var res = await _taskService.GetMinorTasksByMajorTaskAsync(majorTaskId);
            MinorTasks.Clear();
            foreach (var m in res.MinorTasks.Where(m => m.CreatedByUserId == _userSession.UserId))
                MinorTasks.Add(m);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Load error", $"Could not load sub-tasks: {ex.Message}", ToastType.Error);
        }
    }

    private static string FormatElapsed(TimeSpan ts) =>
        ts.TotalHours >= 24 ? $"{(int)ts.TotalHours / 24}d {ts:hh\\:mm\\:ss}" : ts.ToString("hh\\:mm\\:ss");
}