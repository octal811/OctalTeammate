using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class CalendarFloatViewModel : ObservableObject
{
    private readonly IEventService _eventService;
    private readonly IProjectService _projectService;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private bool _isBusy;

    /// <summary>Upcoming events within the next 14 days, across all projects.</summary>
    public ObservableCollection<UpcomingEventItem> UpcomingEvents { get; } = new();

    public CalendarFloatViewModel(
        IEventService eventService,
        IProjectService projectService,
        IDialogService dialogService,
        RealtimeNotificationService notificationService)
    {
        _eventService = eventService;
        _projectService = projectService;
        _dialogService = dialogService;

        notificationService.CalendarUpdated += () =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
            {
                await RefreshAsync();
            });
        };
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        UpcomingEvents.Clear();

        try
        {
            var today = DateTime.Today;
            var horizon = today.AddDays(14);
            var paged = await _projectService.GetAllProjectsAsync(1, 50);

            var collected = new List<UpcomingEventItem>();

            foreach (var p in paged.Items)
            {
                try
                {
                    // Load current month
                    var res = await _eventService.GetEventsByMonthAsync(p.Id, today.Year, today.Month);
                    foreach (var ev in res.Events.Where(e => e.StartDate.Date >= today && e.StartDate.Date <= horizon))
                    {
                        collected.Add(new UpcomingEventItem(ev, p.Title));
                    }

                    // Also load next month if we cross a month boundary
                    if (horizon.Month != today.Month)
                    {
                        var nextRes = await _eventService.GetEventsByMonthAsync(p.Id, horizon.Year, horizon.Month);
                        foreach (var ev in nextRes.Events.Where(e => e.StartDate.Date >= today && e.StartDate.Date <= horizon))
                        {
                            collected.Add(new UpcomingEventItem(ev, p.Title));
                        }
                    }
                }
                catch { /* skip inaccessible project */ }
            }

            foreach (var item in collected.OrderBy(e => e.StartDate))
            {
                UpcomingEvents.Add(item);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Float Calendar", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public class UpcomingEventItem
{
    public EventItem Event { get; }
    public string ProjectTitle { get; }
    public DateTime StartDate => Event.StartDate;
    public string Title => Event.Title;
    public string EventTypeName => Event.Type.ToString();
    public bool IsToday => Event.StartDate.Date == DateTime.Today;
    public bool IsTomorrow => Event.StartDate.Date == DateTime.Today.AddDays(1);
    public string DayLabel => IsToday ? "Today"
        : IsTomorrow ? "Tomorrow"
        : Event.StartDate.ToString("ddd, MMM d");

    public UpcomingEventItem(EventItem ev, string projectTitle)
    {
        Event = ev;
        ProjectTitle = projectTitle;
    }
}
