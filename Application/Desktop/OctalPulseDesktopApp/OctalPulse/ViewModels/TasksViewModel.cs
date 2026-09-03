using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class TasksViewModel : ObservableObject, INavigationAware
{
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IUserSession _userSession;
    private List<MajorTaskItem> _allLoadedTasks = new();

    [ObservableProperty]
    private string _filterStatus = "All";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<MajorTaskItem> DisplayedTasks { get; } = new();

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
    private async Task LoadAllTasksAsync()
    {
        IsBusy = true;
        _allLoadedTasks.Clear();

        try
        {
            var paged = await _projectService.GetAllProjectsAsync(1, 20);
            foreach (var p in paged.Items)
            {
                var tracksRes = await _projectService.GetTracksByProjectAsync(p.Id);
                foreach (var t in tracksRes.Tracks)
                {
                    var taskRes = await _taskService.GetMajorTasksByTrackAsync(t.Id);
                    _allLoadedTasks.AddRange(taskRes.MajorTasks);
                }
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

    partial void OnFilterStatusChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allLoadedTasks.AsEnumerable();

        if (FilterStatus != "All" && Enum.TryParse<MajorTaskState>(FilterStatus, out var state))
        {
            filtered = filtered.Where(t => t.State == state);
        }

        DisplayedTasks.Clear();
        foreach (var task in filtered)
        {
            DisplayedTasks.Add(task);
        }
    }

    [RelayCommand]
    private void OpenTaskDetail(MajorTaskItem task)
    {
        if (task == null) return;
        _navigationService.NavigateTo<MajorTaskDetailViewModel>(task.Id);
    }
}
