using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public class CalendarDayItem : ObservableObject
{
    public DateTime Date { get; set; }
    public int DayNumber => Date.Day;
    public bool IsCurrentMonth { get; set; }
    public bool IsToday => Date.Date == DateTime.Today;
    public bool IsSelected { get; set; }
    public ObservableCollection<EventItem> DayEvents { get; } = new();
}

public partial class CalendarViewModel : ObservableObject, INavigationAware, INavigationFromAware
{
    private readonly IEventService _eventService;
    private readonly IProjectService _projectService;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly IDialogService _dialogService;
    private readonly IUserSession _userSession;
    private readonly INavigationService _navigationService;
    private readonly RealtimeNotificationService _notificationService;

    [ObservableProperty]
    private DateTime _currentMonthDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private string _monthTitle = string.Empty;

    [ObservableProperty]
    private ProjectSummaryItem? _selectedProject;

    [ObservableProperty]
    private CalendarDayItem? _selectedDay;

    [ObservableProperty]
    private bool _isBusy;

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
    private DateTime _newEventDate = DateTime.Today;

    public ObservableCollection<ProjectSummaryItem> AvailableProjects { get; } = new();
    public ObservableCollection<CalendarDayItem> Days { get; } = new();
    public ObservableCollection<EventItem> SelectedDayEvents { get; } = new();
    public IReadOnlyList<EventType> AvailableEventTypes { get; } = Enum.GetValues<EventType>();

    public CalendarViewModel(
        IEventService eventService,
        IProjectService projectService,
        ISignalRRealtimeService signalRService,
        IDialogService dialogService,
        IUserSession userSession,
        INavigationService navigationService,
        RealtimeNotificationService notificationService)
    {
        _eventService = eventService;
        _projectService = projectService;
        _signalRService = signalRService;
        _dialogService = dialogService;
        _userSession = userSession;
        _navigationService = navigationService;
        _notificationService = notificationService;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _signalRService.EventChanged -= OnEventChanged;
        _signalRService.EventChanged += OnEventChanged;
        _notificationService.CalendarUpdated -= OnCalendarUpdated;
        _notificationService.CalendarUpdated += OnCalendarUpdated;

        _ = LoadInitialDataAsync();
    }

    public void OnNavigatedFrom()
    {
        _signalRService.EventChanged -= OnEventChanged;
        _notificationService.CalendarUpdated -= OnCalendarUpdated;
    }

    private void OnCalendarUpdated()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
        {
            await LoadMonthEventsAsync();
        });
    }

    private void OnEventChanged(Guid eventId, Guid projectId)
    {
        if (SelectedProject != null && SelectedProject.Id == projectId)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
            {
                await LoadMonthEventsAsync();
            });
        }
    }

    private async Task LoadInitialDataAsync()
    {
        IsBusy = true;
        try
        {
            var paged = await _projectService.GetAllProjectsAsync(1, 50);
            AvailableProjects.Clear();
            foreach (var p in paged.Items)
            {
                AvailableProjects.Add(p);
            }

            if (AvailableProjects.Count > 0 && SelectedProject == null)
            {
                SelectedProject = AvailableProjects[0];
            }

            BuildCalendarGrid();
            await LoadMonthEventsAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Calendar Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedProjectChanged(ProjectSummaryItem? value)
    {
        UpdateBreadcrumbs(value);
        if (value != null)
        {
            _ = _signalRService.JoinProjectAsync(value.Id);
            _ = LoadMonthEventsAsync();
        }
    }

    private void UpdateBreadcrumbs(ProjectSummaryItem? project)
    {
        if (project == null)
        {
            _navigationService.SetBreadcrumbs(new BreadcrumbItem("Calendar"));
            return;
        }

        _navigationService.SetBreadcrumbs(
            new BreadcrumbItem("Calendar", () => _navigationService.NavigateTo<CalendarViewModel>()),
            new BreadcrumbItem(project.Title, () => _navigationService.NavigateTo<ProjectDetailViewModel>(project.Id)));
    }

    private void BuildCalendarGrid()
    {
        MonthTitle = CurrentMonthDate.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        Days.Clear();

        var firstDayOfMonth = new DateTime(CurrentMonthDate.Year, CurrentMonthDate.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(CurrentMonthDate.Year, CurrentMonthDate.Month);

        // Day of week offset (Monday as start of week)
        var firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
        var offset = firstDayOfWeek == 0 ? 6 : firstDayOfWeek - 1;

        // Previous month days
        var prevMonth = firstDayOfMonth.AddMonths(-1);
        var daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
        for (var i = offset - 1; i >= 0; i--)
        {
            Days.Add(new CalendarDayItem
            {
                Date = new DateTime(prevMonth.Year, prevMonth.Month, daysInPrevMonth - i),
                IsCurrentMonth = false
            });
        }

        // Current month days
        for (var i = 1; i <= daysInMonth; i++)
        {
            var dayDate = new DateTime(CurrentMonthDate.Year, CurrentMonthDate.Month, i);
            var item = new CalendarDayItem
            {
                Date = dayDate,
                IsCurrentMonth = true
            };
            if (dayDate.Date == DateTime.Today)
            {
                SelectedDay = item;
            }
            Days.Add(item);
        }

        // Next month trailing days to complete grid (42 cells: 6 weeks)
        var totalCells = Days.Count;
        var remaining = 42 - totalCells;
        var nextMonth = firstDayOfMonth.AddMonths(1);
        for (var i = 1; i <= remaining; i++)
        {
            Days.Add(new CalendarDayItem
            {
                Date = new DateTime(nextMonth.Year, nextMonth.Month, i),
                IsCurrentMonth = false
            });
        }

        if (SelectedDay == null && Days.Count > 0)
        {
            SelectedDay = Days.FirstOrDefault(d => d.IsCurrentMonth);
        }
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        CurrentMonthDate = CurrentMonthDate.AddMonths(-1);
        BuildCalendarGrid();
        await LoadMonthEventsAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        CurrentMonthDate = CurrentMonthDate.AddMonths(1);
        BuildCalendarGrid();
        await LoadMonthEventsAsync();
    }

    [RelayCommand]
    private async Task LoadMonthEventsAsync()
    {
        if (SelectedProject == null) return;

        IsBusy = true;
        try
        {
            var res = await _eventService.GetEventsByMonthAsync(SelectedProject.Id, CurrentMonthDate.Year, CurrentMonthDate.Month);

            // Clear previous events from grid
            foreach (var day in Days)
            {
                day.DayEvents.Clear();
            }

            foreach (var ev in res.Events)
            {
                var eventDay = GetEffectiveDate(ev.StartDate);
                var matchingDay = Days.FirstOrDefault(d => d.Date.Date == eventDay);
                matchingDay?.DayEvents.Add(ev);
            }

            RefreshSelectedDayEvents();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Load Events Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static DateTime GetEffectiveDate(DateTime dt)
    {
        // If midnight (00:00:00), it directly represents the calendar date.
        if (dt.TimeOfDay == TimeSpan.Zero)
        {
            return dt.Date;
        }

        // If it has a time component (e.g. UTC 22:00 from previous offset bug or specific hour),
        // convert to local time to determine the local calendar day.
        return dt.ToLocalTime().Date;
    }

    [RelayCommand]
    private void SelectDay(CalendarDayItem day)
    {
        if (day == null) return;
        SelectedDay = day;
        RefreshSelectedDayEvents();
    }

    private void RefreshSelectedDayEvents()
    {
        SelectedDayEvents.Clear();
        if (SelectedDay != null)
        {
            foreach (var ev in SelectedDay.DayEvents)
            {
                SelectedDayEvents.Add(ev);
            }
        }
    }

    [RelayCommand]
    private void OpenCreateEventModal()
    {
        if (SelectedProject == null)
        {
            _dialogService.ShowToast("Notice", "Please select a project first.", ToastType.Warning);
            return;
        }
        NewEventTitle = string.Empty;
        NewEventDescription = string.Empty;
        NewEventType = EventType.Meeting;
        NewEventDate = SelectedDay?.Date ?? DateTime.Today;
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
        if (string.IsNullOrWhiteSpace(NewEventTitle) || SelectedProject == null)
        {
            _dialogService.ShowToast("Validation Error", "Please provide an event title.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var eventDate = DateTime.SpecifyKind(NewEventDate.Date, DateTimeKind.Utc);
            await _eventService.CreateEventAsync(new CreateEventRequest(
                SelectedProject.Id,
                NewEventTitle.Trim(),
                string.IsNullOrWhiteSpace(NewEventDescription) ? null : NewEventDescription.Trim(),
                NewEventType,
                eventDate,
                eventDate,
                null,
                null,
                true,
                null,
                null));

            IsCreateEventModalOpen = false;
            _dialogService.ShowToast("Event Created", "New team event added.", ToastType.Success);
            await LoadMonthEventsAsync();
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

    [RelayCommand]
    private async Task DeleteEventAsync(EventItem ev)
    {
        if (ev == null) return;

        var confirm = await _dialogService.ShowConfirmationAsync("Delete Event", $"Delete event '{ev.Title}'?", "Delete", "Cancel");
        if (!confirm) return;

        IsBusy = true;
        try
        {
            await _eventService.DeleteEventAsync(ev.Id);
            _dialogService.ShowToast("Event Deleted", "Event removed.", ToastType.Info);
            await LoadMonthEventsAsync();
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
