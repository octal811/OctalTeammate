using System.Windows;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;

namespace OctalPulse.Services;

/// <summary>
/// Central coordinator for real-time SignalR notifications and Windows balloon tips.
/// Manages automatic group memberships, alerts the user via Windows notifications,
/// and notifies ViewModels and Floating Windows to refresh in real time.
/// </summary>
public sealed class RealtimeNotificationService : IDisposable
{
    private readonly ISignalRRealtimeService _signalRService;
    private readonly TrayService _trayService;
    private readonly IProjectService _projectService;
    private readonly IUserSession _userSession;
    private readonly ILogger<RealtimeNotificationService> _logger;

    private bool _initialized;
    private bool _isDisposed;

    public event Action? MajorTasksUpdated;
    public event Action? MinorTasksUpdated;
    public event Action? CalendarUpdated;
    public event Action? ProjectsTracksUpdated;
    public event Action? MediaUpdated;

    public RealtimeNotificationService(
        ISignalRRealtimeService signalRService,
        TrayService trayService,
        IProjectService projectService,
        IUserSession userSession,
        ILogger<RealtimeNotificationService> logger)
    {
        _signalRService = signalRService;
        _trayService = trayService;
        _projectService = projectService;
        _userSession = userSession;
        _logger = logger;
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _signalRService.ConnectionStateChanged += OnConnectionStateChanged;
        _signalRService.ProjectChanged += OnProjectChanged;
        _signalRService.TrackChanged += OnTrackChanged;
        _signalRService.MajorTaskChanged += OnMajorTaskChanged;
        _signalRService.MinorTaskChanged += OnMinorTaskChanged;
        _signalRService.EventChanged += OnEventChanged;
        _signalRService.PostCreated += OnPostCreated;

        // If already connected, sync groups right away
        if (_signalRService.IsConnected)
        {
            _ = SyncAllUserGroupsAsync();
        }
    }

    private void OnConnectionStateChanged(bool connected)
    {
        if (connected)
        {
            _logger.LogInformation("SignalR connected in RealtimeNotificationService — syncing groups...");
            _ = SyncAllUserGroupsAsync();
        }
    }

    public async Task SyncAllUserGroupsAsync()
    {
        try
        {
            if (!_userSession.IsAuthenticated) return;

            var projects = await _projectService.GetAllProjectsAsync(1, 100);
            var projectIds = new List<Guid>();
            var trackIds = new List<Guid>();

            foreach (var project in projects.Items)
            {
                projectIds.Add(project.Id);
                try
                {
                    var tracks = await _projectService.GetTracksByProjectAsync(project.Id);
                    foreach (var t in tracks.Tracks)
                    {
                        trackIds.Add(t.Id);
                    }
                }
                catch { /* ignore inaccessible tracks */ }
            }

            await _signalRService.RegisterGroupMembershipAsync(projectIds, trackIds);
            _logger.LogInformation("Synced {ProjectCount} project groups and {TrackCount} track groups with SignalR.",
                projectIds.Count, trackIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to synchronize user SignalR groups.");
        }
    }

    private void OnProjectChanged(Guid projectId)
    {
        _ = _signalRService.JoinProjectAsync(projectId);

        ShowNotification("Project Updated", "A project was created or updated.");
        NotifyUi(() => ProjectsTracksUpdated?.Invoke());
    }

    private void OnTrackChanged(Guid trackId, Guid projectId)
    {
        // When a new track is created, join its track group immediately so we receive its tasks!
        _ = _signalRService.JoinTrackAsync(trackId);
        _ = _signalRService.JoinProjectAsync(projectId);

        ShowNotification("Track Updated", "A project track was created or updated.");
        NotifyUi(() =>
        {
            ProjectsTracksUpdated?.Invoke();
            MajorTasksUpdated?.Invoke();
            MinorTasksUpdated?.Invoke();
        });
    }

    private void OnMajorTaskChanged(Guid majorTaskId, Guid trackId)
    {
        ShowNotification("Major Task Updated", "A major task was added or updated in your track.");
        NotifyUi(() => MajorTasksUpdated?.Invoke());
    }

    private void OnMinorTaskChanged(Guid minorTaskId, Guid trackId)
    {
        ShowNotification("Minor Task Updated", "A sub-task was added or updated in your track.");
        NotifyUi(() => MinorTasksUpdated?.Invoke());
    }

    private void OnEventChanged(Guid eventId, Guid projectId)
    {
        _ = _signalRService.JoinProjectAsync(projectId);

        ShowNotification("Calendar Event", "A calendar schedule event was added or updated.");
        NotifyUi(() => CalendarUpdated?.Invoke());
    }

    private void OnPostCreated(Guid postId, Guid? projectId, Guid? trackId)
    {
        ShowNotification("New Media Post", "A new post was shared on your team feed.");
        NotifyUi(() => MediaUpdated?.Invoke());
    }

    private void ShowNotification(string title, string message)
    {
        try
        {
            _trayService.ShowBalloonTip(title, message, 3000);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to display tray balloon tip.");
        }
    }

    private static void NotifyUi(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app != null)
        {
            if (app.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                app.Dispatcher.BeginInvoke(action);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _signalRService.ConnectionStateChanged -= OnConnectionStateChanged;
        _signalRService.ProjectChanged -= OnProjectChanged;
        _signalRService.TrackChanged -= OnTrackChanged;
        _signalRService.MajorTaskChanged -= OnMajorTaskChanged;
        _signalRService.MinorTaskChanged -= OnMinorTaskChanged;
        _signalRService.EventChanged -= OnEventChanged;
        _signalRService.PostCreated -= OnPostCreated;
    }
}
