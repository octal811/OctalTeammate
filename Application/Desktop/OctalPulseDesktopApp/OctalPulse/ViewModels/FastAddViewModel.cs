using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using Clipboard = System.Windows.Clipboard;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public enum FastAddStep
{
    Input,
    Executing,
    Finished
}

public partial class FastAddExecutionItemModel : ObservableObject
{
    public FastAddTaskType TaskType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DetailsOrTarget { get; set; }
    public string? Notes { get; set; }
    public string? ExternalLink { get; set; }
    public string? ParentTitle { get; set; }
    public MajorTaskState MajorState { get; set; } = MajorTaskState.Todo;
    public Priority MajorPriority { get; set; } = Priority.Medium;
    public DateTime? DueDate { get; set; }
    public MinorTaskState MinorState { get; set; } = MinorTaskState.Todo;
    public MinorTaskJobType? JobType { get; set; }

    // Reference to parent major item model if this is a child minor task
    public FastAddExecutionItemModel? ParentMajorItem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending))]
    [NotifyPropertyChangedFor(nameof(IsInProgress))]
    [NotifyPropertyChangedFor(nameof(IsSuccess))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(IsSkipped))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private FastAddExecutionStatus _status = FastAddExecutionStatus.Pending;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private Guid? _createdId;

    public bool IsPending => Status == FastAddExecutionStatus.Pending;
    public bool IsInProgress => Status == FastAddExecutionStatus.InProgress;
    public bool IsSuccess => Status == FastAddExecutionStatus.Success;
    public bool IsFailed => Status == FastAddExecutionStatus.Failed;
    public bool IsSkipped => Status == FastAddExecutionStatus.Skipped;

    public string TypeBadge => TaskType == FastAddTaskType.Major ? "MAJOR" : "MINOR";

    public string StatusDisplay => Status switch
    {
        FastAddExecutionStatus.Pending => "Queued",
        FastAddExecutionStatus.InProgress => "Adding to project...",
        FastAddExecutionStatus.Success => "Created",
        FastAddExecutionStatus.Failed => $"Failed: {ErrorMessage}",
        FastAddExecutionStatus.Skipped => "Skipped (Parent failed)",
        _ => "Unknown"
    };
}

public partial class FastAddViewModel : ObservableObject
{
    private readonly ITaskService _taskService;
    private readonly IProjectService _projectService;
    private readonly IUserSession _userSession;
    private readonly IFastAddParserService _parserService;
    private readonly IDialogService _dialogService;

    public event Action<bool>? RequestClose;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputStep))]
    [NotifyPropertyChangedFor(nameof(IsExecutingStep))]
    [NotifyPropertyChangedFor(nameof(IsFinishedStep))]
    private FastAddStep _currentStep = FastAddStep.Input;

    public bool IsInputStep => CurrentStep == FastAddStep.Input;
    public bool IsExecutingStep => CurrentStep == FastAddStep.Executing;
    public bool IsFinishedStep => CurrentStep == FastAddStep.Finished;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMajorTasksScope))]
    [NotifyPropertyChangedFor(nameof(IsMinorTasksScope))]
    private FastAddScope _selectedScope = FastAddScope.MajorTasks;

    public bool IsMajorTasksScope => SelectedScope == FastAddScope.MajorTasks;
    public bool IsMinorTasksScope => SelectedScope == FastAddScope.MinorTasks;

    [ObservableProperty]
    private bool _isScopeLocked;

    [ObservableProperty]
    private string _rawJsonInput = string.Empty;

    // Track Selection
    public ObservableCollection<TrackOption> AvailableTracks { get; } = new();

    [ObservableProperty]
    private TrackOption? _selectedTrack;

    // Major Task Context (when opened from MajorTaskDetail)
    [ObservableProperty]
    private Guid? _targetMajorTaskId;

    [ObservableProperty]
    private string? _targetMajorTaskTitle;

    // Validation State
    [ObservableProperty]
    private bool _hasValidationRun;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string _validationStatusMessage = string.Empty;

    public ObservableCollection<string> ValidationErrors { get; } = new();
    public ObservableCollection<string> ValidationWarnings { get; } = new();
    public ObservableCollection<FastAddMajorTaskDto> ParsedMajorTasks { get; } = new();
    public ObservableCollection<FastAddMinorTaskDto> ParsedMinorTasks { get; } = new();

    // Execution State
    public ObservableCollection<FastAddExecutionItemModel> ExecutionItems { get; } = new();

    [ObservableProperty]
    private int _processedCount;

    [ObservableProperty]
    private int _totalToProcess;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _progressText = "Preparing import...";

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private bool _hasAnySucceeded;

    public FastAddViewModel(
        ITaskService taskService,
        IProjectService projectService,
        IUserSession userSession,
        IFastAddParserService parserService,
        IDialogService dialogService)
    {
        _taskService = taskService;
        _projectService = projectService;
        _userSession = userSession;
        _parserService = parserService;
        _dialogService = dialogService;
    }

    public async Task InitializeForTrackAsync(Guid? preferredTrackId = null)
    {
        SelectedScope = FastAddScope.MajorTasks;
        IsScopeLocked = false;
        TargetMajorTaskId = null;
        TargetMajorTaskTitle = null;

        await LoadTracksAsync(preferredTrackId);
    }

    public void InitializeForMajorTask(Guid majorTaskId, string majorTaskTitle)
    {
        SelectedScope = FastAddScope.MinorTasks;
        IsScopeLocked = true;
        TargetMajorTaskId = majorTaskId;
        TargetMajorTaskTitle = majorTaskTitle;
    }

    private async Task LoadTracksAsync(Guid? preferredTrackId)
    {
        AvailableTracks.Clear();
        try
        {
            var userId = _userSession.UserId;
            var paged = await _projectService.GetAllProjectsAsync(1, 50);

            TrackOption? toSelect = null;

            foreach (var p in paged.Items)
            {
                var tracksRes = await _projectService.GetTracksByProjectAsync(p.Id);
                foreach (var t in tracksRes.Tracks)
                {
                    var isMember = t.TrackLeadUserId == userId
                        || t.CurrentUserMembership == MembershipStatus.Approved;

                    if (!isMember)
                        continue;

                    var opt = new TrackOption(t.Id, p.Id, p.Title, t.Name);
                    AvailableTracks.Add(opt);

                    if (preferredTrackId.HasValue && t.Id == preferredTrackId.Value)
                    {
                        toSelect = opt;
                    }
                }
            }

            SelectedTrack = toSelect ?? AvailableTracks.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Load Tracks Error", ex.Message, ToastType.Error);
        }
    }

    partial void OnRawJsonInputChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ValidateJson();
        }
        else
        {
            ResetValidation();
        }
    }

    partial void OnSelectedScopeChanged(FastAddScope value)
    {
        if (!string.IsNullOrWhiteSpace(RawJsonInput))
        {
            ValidateJson();
        }
    }

    [RelayCommand]
    public void ValidateJson()
    {
        HasValidationRun = true;
        ValidationErrors.Clear();
        ValidationWarnings.Clear();
        ParsedMajorTasks.Clear();
        ParsedMinorTasks.Clear();

        if (string.IsNullOrWhiteSpace(RawJsonInput))
        {
            IsValid = false;
            ValidationStatusMessage = "Please paste JSON task definitions.";
            return;
        }

        // Scope check
        FastAddScope? forced = IsScopeLocked ? FastAddScope.MinorTasks : SelectedScope;
        var result = _parserService.ParseAndValidate(RawJsonInput, forced);

        if (!IsScopeLocked && result.DetectedScope != SelectedScope)
        {
            SelectedScope = result.DetectedScope;
        }

        // Context check (Track or MajorTask target required)
        if (SelectedScope == FastAddScope.MajorTasks && SelectedTrack == null)
        {
            result.Errors.Add("Please select a target Project & Track for these Major Tasks.");
        }
        else if (SelectedScope == FastAddScope.MinorTasks && TargetMajorTaskId == null)
        {
            result.Errors.Add("Please specify or navigate to a target Major Task to add minor tasks.");
        }

        foreach (var err in result.Errors)
            ValidationErrors.Add(err);

        foreach (var warn in result.Warnings)
            ValidationWarnings.Add(warn);

        foreach (var m in result.MajorTasks)
            ParsedMajorTasks.Add(m);

        foreach (var sub in result.MinorTasks)
            ParsedMinorTasks.Add(sub);

        IsValid = result.Errors.Count == 0 && (ParsedMajorTasks.Count > 0 || ParsedMinorTasks.Count > 0);

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(HasMajorTasks));
        OnPropertyChanged(nameof(HasMinorTasks));

        if (IsValid)
        {
            var majorCount = ParsedMajorTasks.Count;
            var subCount = ParsedMajorTasks.Sum(m => m.MinorTasks.Count) + ParsedMinorTasks.Count;

            ValidationStatusMessage = SelectedScope == FastAddScope.MajorTasks
                ? $"✓ Ready to import {majorCount} Major Task{(majorCount == 1 ? string.Empty : "s")} and {subCount} Sub-task{(subCount == 1 ? string.Empty : "s")}."
                : $"✓ Ready to import {subCount} Minor Task{(subCount == 1 ? string.Empty : "s")}.";
        }
        else
        {
            ValidationStatusMessage = $"Found {ValidationErrors.Count} error{(ValidationErrors.Count == 1 ? string.Empty : "s")} to fix.";
        }
    }

    public bool HasErrors => ValidationErrors.Count > 0;
    public bool HasWarnings => ValidationWarnings.Count > 0;
    public bool HasMajorTasks => ParsedMajorTasks.Count > 0;
    public bool HasMinorTasks => ParsedMinorTasks.Count > 0;

    private void ResetValidation()
    {
        HasValidationRun = false;
        IsValid = false;
        ValidationStatusMessage = string.Empty;
        ValidationErrors.Clear();
        ValidationWarnings.Clear();
        ParsedMajorTasks.Clear();
        ParsedMinorTasks.Clear();

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(HasMajorTasks));
        OnPropertyChanged(nameof(HasMinorTasks));
    }

    [RelayCommand]
    private void CopyMajorPrompt()
    {
        try
        {
            Clipboard.SetText(FastAddPromptProvider.GetMajorTasksPrompt());
            _dialogService.ShowToast("Prompt Copied", "Major Tasks AI prompt copied to clipboard. Paste it in your AI chat!", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Copy Failed", ex.Message, ToastType.Warning);
        }
    }

    [RelayCommand]
    private void CopyMinorPrompt()
    {
        try
        {
            Clipboard.SetText(FastAddPromptProvider.GetMinorTasksPrompt());
            _dialogService.ShowToast("Prompt Copied", "Minor Tasks AI prompt copied to clipboard. Paste it in your AI chat!", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Copy Failed", ex.Message, ToastType.Warning);
        }
    }

    [RelayCommand]
    private void LoadSampleJson()
    {
        RawJsonInput = SelectedScope == FastAddScope.MajorTasks
            ? FastAddPromptProvider.GetSampleMajorTasksJson()
            : FastAddPromptProvider.GetSampleMinorTasksJson();

        ValidateJson();
        _dialogService.ShowToast("Sample Loaded", "Sample task JSON loaded into editor.", ToastType.Info);
    }

    [RelayCommand]
    private void FormatJson()
    {
        if (string.IsNullOrWhiteSpace(RawJsonInput)) return;
        RawJsonInput = _parserService.FormatJson(RawJsonInput);
        ValidateJson();
    }

    [RelayCommand]
    private void PasteFromClipboard()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                RawJsonInput = Clipboard.GetText();
                ValidateJson();
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Clipboard Error", ex.Message, ToastType.Warning);
        }
    }

    [RelayCommand]
    private void ClearInput()
    {
        RawJsonInput = string.Empty;
        ResetValidation();
    }

    [RelayCommand]
    private async Task StartFastAddAsync()
    {
        ValidateJson();
        if (!IsValid)
        {
            _dialogService.ShowToast("Validation Error", "Please fix errors before starting import.", ToastType.Warning);
            return;
        }

        // Build execution queue
        ExecutionItems.Clear();

        if (SelectedScope == FastAddScope.MajorTasks)
        {
            foreach (var major in ParsedMajorTasks)
            {
                var majorItem = new FastAddExecutionItemModel
                {
                    TaskType = FastAddTaskType.Major,
                    Title = major.Title,
                    Description = major.Description,
                    DetailsOrTarget = major.Details,
                    ExternalLink = major.ExternalLink,
                    MajorState = major.ResolvedState,
                    MajorPriority = major.ResolvedPriority,
                    DueDate = major.ResolvedDueDate,
                    Status = FastAddExecutionStatus.Pending
                };
                ExecutionItems.Add(majorItem);

                foreach (var minor in major.MinorTasks)
                {
                    var minorItem = new FastAddExecutionItemModel
                    {
                        TaskType = FastAddTaskType.Minor,
                        Title = minor.Title,
                        Description = minor.Description,
                        DetailsOrTarget = minor.Target,
                        Notes = minor.Notes,
                        ExternalLink = minor.ExternalLink,
                        MinorState = minor.ResolvedState,
                        JobType = minor.ResolvedJobType,
                        ParentTitle = major.Title,
                        ParentMajorItem = majorItem,
                        Status = FastAddExecutionStatus.Pending
                    };
                    ExecutionItems.Add(minorItem);
                }
            }
        }
        else
        {
            foreach (var minor in ParsedMinorTasks)
            {
                var minorItem = new FastAddExecutionItemModel
                {
                    TaskType = FastAddTaskType.Minor,
                    Title = minor.Title,
                    Description = minor.Description,
                    DetailsOrTarget = minor.Target,
                    Notes = minor.Notes,
                    ExternalLink = minor.ExternalLink,
                    MinorState = minor.ResolvedState,
                    JobType = minor.ResolvedJobType,
                    ParentTitle = TargetMajorTaskTitle,
                    Status = FastAddExecutionStatus.Pending
                };
                ExecutionItems.Add(minorItem);
            }
        }

        CurrentStep = FastAddStep.Executing;
        await ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        IsExecuting = true;
        TotalToProcess = ExecutionItems.Count;
        ProcessedCount = 0;
        SuccessCount = 0;
        FailedCount = 0;
        ProgressPercentage = 0;

        var trackId = SelectedTrack?.TrackId ?? Guid.Empty;
        var directMajorId = TargetMajorTaskId ?? Guid.Empty;

        var majorOrder = 0;
        var minorOrder = 0;

        for (var i = 0; i < ExecutionItems.Count; i++)
        {
            var item = ExecutionItems[i];

            // If already succeeded (during retry), skip
            if (item.Status == FastAddExecutionStatus.Success)
            {
                ProcessedCount++;
                UpdateProgress();
                continue;
            }

            // If parent major task failed, skip child minor task
            if (item.TaskType == FastAddTaskType.Minor && item.ParentMajorItem != null && item.ParentMajorItem.Status == FastAddExecutionStatus.Failed)
            {
                item.Status = FastAddExecutionStatus.Skipped;
                item.ErrorMessage = "Parent Major Task creation failed.";
                FailedCount++;
                ProcessedCount++;
                UpdateProgress();
                continue;
            }

            item.Status = FastAddExecutionStatus.InProgress;
            ProgressText = $"Adding task {i + 1} of {TotalToProcess}: '{item.Title}'...";

            // Delay slight tick for smooth UI rendering
            await Task.Delay(60);

            try
            {
                if (item.TaskType == FastAddTaskType.Major)
                {
                    majorOrder++;
                    var response = await _taskService.CreateMajorTaskAsync(new CreateMajorTaskRequest(
                        trackId,
                        item.Title,
                        item.Description,
                        item.DetailsOrTarget,
                        item.ExternalLink,
                        item.MajorState,
                        item.MajorPriority,
                        item.DueDate,
                        majorOrder,
                        null));

                    item.CreatedId = response.Id;
                    item.Status = FastAddExecutionStatus.Success;
                    item.ErrorMessage = null;
                    SuccessCount++;
                    HasAnySucceeded = true;
                }
                else
                {
                    // Minor Task
                    Guid targetParentId;
                    if (item.ParentMajorItem != null)
                    {
                        targetParentId = item.ParentMajorItem.CreatedId ?? Guid.Empty;
                    }
                    else
                    {
                        targetParentId = directMajorId;
                    }

                    if (targetParentId == Guid.Empty)
                    {
                        throw new InvalidOperationException("Target Major Task ID is missing.");
                    }

                    minorOrder++;
                    var response = await _taskService.CreateMinorTaskAsync(new CreateMinorTaskRequest(
                        targetParentId,
                        item.Title,
                        item.Description,
                        item.DetailsOrTarget,
                        item.MinorState,
                        item.JobType,
                        item.Notes,
                        item.ExternalLink,
                        minorOrder,
                        null));

                    item.CreatedId = response.Id;
                    item.Status = FastAddExecutionStatus.Success;
                    item.ErrorMessage = null;
                    SuccessCount++;
                    HasAnySucceeded = true;
                }
            }
            catch (Exception ex)
            {
                item.Status = FastAddExecutionStatus.Failed;
                item.ErrorMessage = ex.Message;
                FailedCount++;
            }

            ProcessedCount++;
            UpdateProgress();
        }

        IsExecuting = false;
        CurrentStep = FastAddStep.Finished;

        if (FailedCount == 0)
        {
            ProgressText = $"Completed! All {SuccessCount} tasks added successfully.";
            _dialogService.ShowToast("Fast Add Finished", $"Successfully created {SuccessCount} tasks!", ToastType.Success);
        }
        else
        {
            ProgressText = $"Finished with issues: {SuccessCount} succeeded, {FailedCount} failed.";
            _dialogService.ShowToast("Fast Add Finished", $"{SuccessCount} tasks created, {FailedCount} failed.", ToastType.Warning);
        }
    }

    private void UpdateProgress()
    {
        if (TotalToProcess > 0)
        {
            ProgressPercentage = Math.Round((double)ProcessedCount / TotalToProcess * 100, 1);
        }
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        var hasFailed = ExecutionItems.Any(item => item.Status == FastAddExecutionStatus.Failed || item.Status == FastAddExecutionStatus.Skipped);
        if (!hasFailed) return;

        foreach (var item in ExecutionItems)
        {
            if (item.Status == FastAddExecutionStatus.Failed || item.Status == FastAddExecutionStatus.Skipped)
            {
                item.Status = FastAddExecutionStatus.Pending;
                item.ErrorMessage = null;
            }
        }

        CurrentStep = FastAddStep.Executing;
        await ProcessQueueAsync();
    }

    [RelayCommand]
    private void CopyErrorReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Fast Add Error Report ===");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        sb.AppendLine($"Target: {SelectedTrack?.DisplayName ?? TargetMajorTaskTitle ?? "Unknown"}");
        sb.AppendLine();

        foreach (var item in ExecutionItems.Where(x => x.Status == FastAddExecutionStatus.Failed || x.Status == FastAddExecutionStatus.Skipped))
        {
            sb.AppendLine($"[{item.TypeBadge}] {item.Title}");
            sb.AppendLine($"Status: {item.Status}");
            sb.AppendLine($"Error: {item.ErrorMessage}");
            sb.AppendLine();
        }

        try
        {
            Clipboard.SetText(sb.ToString());
            _dialogService.ShowToast("Report Copied", "Error report copied to clipboard.", ToastType.Info);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Copy Error", ex.Message, ToastType.Warning);
        }
    }

    [RelayCommand]
    private void BackToInput()
    {
        CurrentStep = FastAddStep.Input;
    }

    [RelayCommand]
    private void CloseDialog()
    {
        RequestClose?.Invoke(HasAnySucceeded);
    }
}
