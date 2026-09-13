using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class DashboardViewModel : ObservableObject, INavigationAware, INavigationFromAware
{
    private readonly IProjectService _projectService;
    private readonly IEventService _eventService;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IUserSession _userSession;

    [ObservableProperty]
    private int _myProjectsCount;

    [ObservableProperty]
    private int _averageProgress;

    [ObservableProperty]
    private int _upcomingEventsCount;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ProjectSummaryItem> RecentProjects { get; } = new();
    public ObservableCollection<EventItem> UpcomingEvents { get; } = new();

    public DashboardViewModel(
        IProjectService projectService,
        IEventService eventService,
        ISignalRRealtimeService signalRService,
        INavigationService navigationService,
        IDialogService dialogService,
        IUserSession userSession)
    {
        _projectService = projectService;
        _eventService = eventService;
        _signalRService = signalRService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _userSession = userSession;

        _signalRService.ProjectChanged += OnProjectChanged;
        _signalRService.EventChanged += OnEventChanged;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _ = LoadDashboardDataAsync();
    }

    public void OnNavigatedFrom()
    {
        _signalRService.ProjectChanged -= OnProjectChanged;
        _signalRService.EventChanged -= OnEventChanged;
    }

    private void OnProjectChanged(Guid projectId)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
        {
            await LoadDashboardDataAsync();
            _dialogService.ShowToast("Realtime Update", "Project information was updated.", ToastType.Info);
        });
    }

    private void OnEventChanged(Guid eventId, Guid projectId)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
        {
            await LoadDashboardDataAsync();
            _dialogService.ShowToast("Realtime Update", "Calendar events were updated.", ToastType.Info);
        });
    }

    [RelayCommand]
    private async Task LoadDashboardDataAsync()
    {
        IsBusy = true;
        try
        {
            // Load projects
            var pagedProjects = await _projectService.GetAllProjectsAsync(1, 20);
            RecentProjects.Clear();

            var totalProgress = 0;
            foreach (var p in pagedProjects.Items)
            {
                RecentProjects.Add(p);
                totalProgress += p.Progress;
            }

            MyProjectsCount = pagedProjects.TotalCount;
            AverageProgress = pagedProjects.Items.Count > 0 ? totalProgress / pagedProjects.Items.Count : 0;

            // Load upcoming events for the first active project if available
            UpcomingEvents.Clear();
            if (pagedProjects.Items.Count > 0)
            {
                var now = DateTime.UtcNow;
                var monthEvents = await _eventService.GetEventsByMonthAsync(pagedProjects.Items[0].Id, now.Year, now.Month);
                var upcoming = monthEvents.Events.Where(e => e.StartDate >= now.Date).Take(5);
                foreach (var ev in upcoming)
                {
                    UpcomingEvents.Add(ev);
                }
                UpcomingEventsCount = monthEvents.TotalCount;
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenProject(ProjectSummaryItem project)
    {
        if (project == null) return;
        _navigationService.NavigateTo<ProjectDetailViewModel>(project.Id);
    }

    [RelayCommand]
    private void NavigateProjects()
    {
        _navigationService.NavigateTo<ProjectsViewModel>();
    }
}
