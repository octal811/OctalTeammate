using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class OctoViewModel : ObservableObject, INavigationAware
{
    private readonly IOctoAgent _octoAgent;
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly ISecureStorageService _secureStorage;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    private const string GeminiApiKeySecretKey = "OctalPulse_GeminiApiKey";

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _currentStatusText;

    [ObservableProperty]
    private bool _isGeminiConfigured;

    [ObservableProperty]
    private bool _isScopeSelectorOpen;

    // Active Scope
    [ObservableProperty]
    private OctoScope _activeScope = new();

    [ObservableProperty]
    private string _activeScopeBadgeText = "All Accessible Workspaces";

    // Hierarchical Scope Selector Lists
    public ObservableCollection<ProjectSummaryItem> AvailableProjects { get; } = new();

    [ObservableProperty]
    private ProjectSummaryItem? _selectedProject;

    public ObservableCollection<TrackSummaryItem> AvailableTracks { get; } = new();

    [ObservableProperty]
    private TrackSummaryItem? _selectedTrack;

    public ObservableCollection<MajorTaskItem> AvailableMajorTasks { get; } = new();

    [ObservableProperty]
    private MajorTaskItem? _selectedMajorTask;

    public ObservableCollection<SelectableTaskItem> AvailableTasksForSelection { get; } = new();

    // Chat Messages
    public ObservableCollection<OctoMessageDisplayItem> Messages { get; } = new();

    public OctoViewModel(
        IOctoAgent octoAgent,
        IProjectService projectService,
        ITaskService taskService,
        ISecureStorageService secureStorage,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _octoAgent = octoAgent;
        _projectService = projectService;
        _taskService = taskService;
        _secureStorage = secureStorage;
        _navigationService = navigationService;
        _dialogService = dialogService;
    }

    public void OnNavigatedTo(object? parameter)
    {
        CheckGeminiConfiguration();
        _ = InitializeScopeHierarchyAsync();

        if (Messages.Count == 0)
        {
            AddWelcomeMessage();
        }
    }

    private void CheckGeminiConfiguration()
    {
        var key = _secureStorage.GetSecret(GeminiApiKeySecretKey);
        IsGeminiConfigured = !string.IsNullOrWhiteSpace(key);
    }

    private void AddWelcomeMessage()
    {
        Messages.Add(new OctoMessageDisplayItem
        {
            Role = "assistant",
            Content = "Hi! I'm **Octo**, your AI project and engineering teammate.\n\nI can help you:\n• **Find where to start** by evaluating priorities, dependencies, and deadlines.\n• **Analyze blockers** and sequential task progression.\n• **Compare tasks** side-by-side.\n• **Suggest task breakdowns** for larger deliverables.\n\nUse the **Context Scope** selector above if you want me to focus on a specific Project, Track, or Task!",
            Timestamp = DateTime.UtcNow
        });
    }

    [RelayCommand]
    private async Task InitializeScopeHierarchyAsync()
    {
        try
        {
            AvailableProjects.Clear();
            var resp = await _projectService.GetAllProjectsAsync(1, 20);
            foreach (var p in resp.Items)
            {
                AvailableProjects.Add(p);
            }
        }
        catch
        {
            // Silently handled
        }
    }

    async partial void OnSelectedProjectChanged(ProjectSummaryItem? value)
    {
        AvailableTracks.Clear();
        SelectedTrack = null;
        AvailableMajorTasks.Clear();
        SelectedMajorTask = null;
        AvailableTasksForSelection.Clear();

        if (value == null) return;

        try
        {
            var tracksResp = await _projectService.GetTracksByProjectAsync(value.Id);
            foreach (var t in tracksResp.Tracks)
            {
                AvailableTracks.Add(t);
            }
        }
        catch
        {
            // Ignored
        }
    }

    async partial void OnSelectedTrackChanged(TrackSummaryItem? value)
    {
        AvailableMajorTasks.Clear();
        SelectedMajorTask = null;
        AvailableTasksForSelection.Clear();

        if (value == null) return;

        try
        {
            var tasksResp = await _taskService.GetMajorTasksByTrackAsync(value.Id);
            foreach (var mt in tasksResp.MajorTasks)
            {
                AvailableMajorTasks.Add(mt);
                AvailableTasksForSelection.Add(new SelectableTaskItem
                {
                    Id = mt.Id,
                    Title = mt.Title,
                    Subtitle = $"Order: {mt.Order} • {mt.State} • {mt.Priority}",
                    IsSelected = false
                });
            }
        }
        catch
        {
            // Ignored
        }
    }

    async partial void OnSelectedMajorTaskChanged(MajorTaskItem? value)
    {
        if (value == null) return;

        try
        {
            var minorResp = await _taskService.GetMinorTasksByMajorTaskAsync(value.Id);
            AvailableTasksForSelection.Clear();

            // First add the major task itself
            AvailableTasksForSelection.Add(new SelectableTaskItem
            {
                Id = value.Id,
                Title = $"[Major] {value.Title}",
                Subtitle = $"{value.State} • {value.Priority}",
                IsSelected = false
            });

            // Then add child subtasks
            foreach (var m in minorResp.MinorTasks.Where(x => !x.IsDeleted))
            {
                AvailableTasksForSelection.Add(new SelectableTaskItem
                {
                    Id = m.Id,
                    Title = m.Title,
                    Subtitle = $"Subtask • Order: {m.Order} • {m.State}",
                    IsSelected = false
                });
            }
        }
        catch
        {
            // Ignored
        }
    }

    [RelayCommand]
    private void ToggleScopeSelector()
    {
        IsScopeSelectorOpen = !IsScopeSelectorOpen;
    }

    [RelayCommand]
    private void ApplyScope()
    {
        var newScope = new OctoScope();

        if (SelectedProject != null)
        {
            newScope.ProjectId = SelectedProject.Id;
            newScope.ProjectTitle = SelectedProject.Title;
        }

        if (SelectedTrack != null)
        {
            newScope.TrackId = SelectedTrack.Id;
            newScope.TrackTitle = SelectedTrack.Name;
        }

        if (SelectedMajorTask != null)
        {
            newScope.MajorTaskId = SelectedMajorTask.Id;
            newScope.MajorTaskTitle = SelectedMajorTask.Title;
        }

        // Specific task selection
        var selectedTasks = AvailableTasksForSelection.Where(t => t.IsSelected).ToList();
        if (selectedTasks.Count > 0)
        {
            newScope.SelectedTaskIds = selectedTasks.Select(t => t.Id).ToList();
            newScope.SelectedTaskTitles = selectedTasks.Select(t => t.Title).ToList();
        }

        ActiveScope = newScope;
        ActiveScopeBadgeText = newScope.DisplayText;
        IsScopeSelectorOpen = false;

        Messages.Add(new OctoMessageDisplayItem
        {
            Role = "system",
            Content = $"🎯 **Scope updated**: {newScope.DisplayText}",
            Timestamp = DateTime.UtcNow
        });
    }

    [RelayCommand]
    private void ClearScope()
    {
        SelectedProject = null;
        SelectedTrack = null;
        SelectedMajorTask = null;
        foreach (var t in AvailableTasksForSelection)
        {
            t.IsSelected = false;
        }

        ActiveScope = new OctoScope();
        ActiveScopeBadgeText = "All Accessible Workspaces";
        IsScopeSelectorOpen = false;

        Messages.Add(new OctoMessageDisplayItem
        {
            Role = "system",
            Content = "🌐 **Scope reset**: Octo can now analyze across all accessible workspaces.",
            Timestamp = DateTime.UtcNow
        });
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsBusy)
            return;

        CheckGeminiConfiguration();
        if (!IsGeminiConfigured)
        {
            _dialogService.ShowToast("Gemini Key Required", "Please configure your Google Gemini API key in Settings to use Octo.", ToastType.Warning);
            return;
        }

        var userMessage = InputText.Trim();
        InputText = string.Empty;

        // Add user bubble
        Messages.Add(new OctoMessageDisplayItem
        {
            Role = "user",
            Content = userMessage,
            Timestamp = DateTime.UtcNow
        });

        // Add placeholder assistant bubble for live streaming status
        var assistantMsg = new OctoMessageDisplayItem
        {
            Role = "assistant",
            Content = string.Empty,
            IsThinking = true,
            StatusText = "Octo is thinking...",
            Timestamp = DateTime.UtcNow
        };
        Messages.Add(assistantMsg);

        IsBusy = true;
        CurrentStatusText = "Octo is thinking...";

        try
        {
            // Build conversation history for agent
            var history = Messages
                .Where(m => m != assistantMsg && !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => new OctoMessage
                {
                    Role = m.Role,
                    Content = m.Content,
                    Timestamp = m.Timestamp
                })
                .ToList();

            var result = await _octoAgent.ProcessMessageAsync(
                userMessage,
                ActiveScope,
                history,
                status =>
                {
                    var app = System.Windows.Application.Current;
                    app?.Dispatcher.BeginInvoke(() =>
                    {
                        CurrentStatusText = status;
                        assistantMsg.StatusText = status;
                    });
                });

            assistantMsg.IsThinking = false;
            assistantMsg.StatusText = null;

            if (result.Success)
            {
                assistantMsg.Content = result.ResponseText;
                if (result.ExecutedToolCount > 0)
                {
                    assistantMsg.ToolSummary = $"Used {result.ExecutedToolCount} data tool{(result.ExecutedToolCount > 1 ? "s" : "")} within {ActiveScope.DisplayText}";
                }
            }
            else
            {
                assistantMsg.Content = result.ResponseText;
                assistantMsg.IsError = true;
            }
        }
        catch (Exception ex)
        {
            assistantMsg.IsThinking = false;
            assistantMsg.StatusText = null;
            assistantMsg.IsError = true;
            assistantMsg.Content = $"Something went wrong: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CurrentStatusText = null;
        }
    }

    [RelayCommand]
    private async Task SendQuickPromptAsync(string prompt)
    {
        InputText = prompt;
        await SendMessageAsync();
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        AddWelcomeMessage();
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        _navigationService.NavigateTo<SettingsViewModel>();
    }
}

public partial class OctoMessageDisplayItem : ObservableObject
{
    [ObservableProperty]
    private string _role = "user"; // "user", "assistant", "system"

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.UtcNow;

    [ObservableProperty]
    private bool _isThinking;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private string? _toolSummary;

    [ObservableProperty]
    private bool _isError;

    public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => Role.Equals("assistant", StringComparison.OrdinalIgnoreCase);
    public bool IsSystem => Role.Equals("system", StringComparison.OrdinalIgnoreCase);

    partial void OnRoleChanged(string value)
    {
        OnPropertyChanged(nameof(IsUser));
        OnPropertyChanged(nameof(IsAssistant));
        OnPropertyChanged(nameof(IsSystem));
    }
}

public partial class SelectableTaskItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
