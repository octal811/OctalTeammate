using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class ProjectsViewModel : ObservableObject, INavigationAware, INavigationFromAware
{
    private readonly IProjectService _projectService;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private List<ProjectSummaryItem> _allProjects = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusFilter = "All";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    // Create Project Modal
    [ObservableProperty]
    private bool _isCreateModalOpen;

    [ObservableProperty]
    private string _newProjectTitle = string.Empty;

    [ObservableProperty]
    private string _newProjectDescription = string.Empty;

    [ObservableProperty]
    private ProjectStatus _newProjectStatus = ProjectStatus.Active;

    // Join Project Modal
    [ObservableProperty]
    private bool _isJoinModalOpen;

    [ObservableProperty]
    private string _joinProjectId = string.Empty;

    [ObservableProperty]
    private ProjectRole _joinSelectedRole = ProjectRole.FrontEnd;

    public ObservableCollection<ProjectSummaryItem> DisplayedProjects { get; } = new();
    public IReadOnlyList<string> StatusFilterOptions { get; } = new[] { "All", "Active", "Completed", "OnHold", "Archived" };
    public IReadOnlyList<ProjectStatus> AvailableStatuses { get; } = Enum.GetValues<ProjectStatus>();
    public IReadOnlyList<ProjectRole> AvailableRoles { get; } = Enum.GetValues<ProjectRole>();

    public ProjectsViewModel(
        IProjectService projectService,
        ISignalRRealtimeService signalRService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _projectService = projectService;
        _signalRService = signalRService;
        _navigationService = navigationService;
        _dialogService = dialogService;

        _signalRService.ProjectChanged += OnProjectChanged;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _ = LoadProjectsAsync();
    }

    public void OnNavigatedFrom()
    {
        _signalRService.ProjectChanged -= OnProjectChanged;
    }

    private void OnProjectChanged(Guid projectId)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
        {
            await LoadProjectsAsync();
        });
    }

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var paged = await _projectService.GetAllProjectsAsync(1, 100);
            _allProjects = paged.Items.ToList();
            ApplyFilter();
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

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnStatusFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allProjects.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim().ToLowerInvariant();
            filtered = filtered.Where(p => p.Title.ToLowerInvariant().Contains(q) || (p.Description?.ToLowerInvariant().Contains(q) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All" && Enum.TryParse<ProjectStatus>(StatusFilter, true, out var st))
        {
            filtered = filtered.Where(p => p.Status == st);
        }

        DisplayedProjects.Clear();
        foreach (var p in filtered)
        {
            DisplayedProjects.Add(p);
        }
    }

    [RelayCommand]
    private void OpenCreateModal()
    {
        NewProjectTitle = string.Empty;
        NewProjectDescription = string.Empty;
        NewProjectStatus = ProjectStatus.Active;
        IsCreateModalOpen = true;
    }

    [RelayCommand]
    private void CloseCreateModal()
    {
        IsCreateModalOpen = false;
    }

    [RelayCommand]
    private async Task SubmitCreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectTitle))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a project title.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _projectService.CreateProjectAsync(new CreateProjectRequest(
                NewProjectTitle.Trim(),
                string.IsNullOrWhiteSpace(NewProjectDescription) ? null : NewProjectDescription.Trim(),
                NewProjectStatus));

            IsCreateModalOpen = false;
            _dialogService.ShowToast("Project Created", $"'{res.Title}' was created successfully.", ToastType.Success);
            await LoadProjectsAsync();

            // Auto-join project SignalR group
            _ = _signalRService.JoinProjectAsync(res.Id);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Create Project Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenJoinModal()
    {
        JoinProjectId = string.Empty;
        IsJoinModalOpen = true;
    }

    [RelayCommand]
    private void CloseJoinModal()
    {
        IsJoinModalOpen = false;
    }

    [RelayCommand]
    private void CopyJoinId()
    {
        if (string.IsNullOrWhiteSpace(JoinProjectId) || !Guid.TryParse(JoinProjectId.Trim(), out _))
        {
            _dialogService.ShowToast("Validation Error", "Enter a valid Project GUID first.", ToastType.Warning);
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(JoinProjectId.Trim());
            _dialogService.ShowToast("Project ID Copied", "Share this ID with teammates so they can request to join.", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Copy Failed", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    private async Task SubmitJoinProjectAsync()
    {
        if (!Guid.TryParse(JoinProjectId.Trim(), out var projId))
        {
            _dialogService.ShowToast("Validation Error", "Please enter a valid Project GUID.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _projectService.RequestJoinProjectAsync(new RequestProjectJoinRequest(
                projId,
                new List<ProjectRole> { JoinSelectedRole }));

            IsJoinModalOpen = false;
            _dialogService.ShowToast("Join Request Submitted", res.Message, ToastType.Info);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Join Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenProjectDetail(ProjectSummaryItem project)
    {
        if (project == null) return;
        _navigationService.NavigateTo<ProjectDetailViewModel>(project.Id);
    }
}
