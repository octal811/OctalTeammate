using System.Windows;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
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
    public event Action<NotificationItemDto>? NotificationReceived;

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
        _signalRService.NotificationReceived += OnNotificationReceived;

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

    private readonly HashSet<Guid> _processedNotificationIds = new();

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

            var userId = _userSession.UserId != Guid.Empty ? _userSession.UserId : (Guid?)null;
            await _signalRService.RegisterGroupMembershipAsync(projectIds, trackIds, userId);
            _logger.LogInformation("Synced {ProjectCount} project groups, {TrackCount} track groups, and user group {UserId} with SignalR.",
                projectIds.Count, trackIds.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to synchronize user SignalR groups.");
        }
    }

    private void OnNotificationReceived(NotificationItemDto notification)
    {
        if (notification.Id != Guid.Empty)
        {
            lock (_processedNotificationIds)
            {
                if (!_processedNotificationIds.Add(notification.Id))
                {
                    _logger.LogDebug("Duplicate notification {NotificationId} ignored.", notification.Id);
                    return;
                }

                if (_processedNotificationIds.Count > 1000)
                {
                    _processedNotificationIds.Clear();
                    _processedNotificationIds.Add(notification.Id);
                }
            }
        }

        _logger.LogInformation("Notification received for user: {Title} - {Message}", notification.Title, notification.Message);

        var title = !string.IsNullOrWhiteSpace(notification.Title) ? notification.Title : "OctalPulse Notification";
        var message = notification.Message;
        if (!string.IsNullOrWhiteSpace(notification.ActorName) &&
            !message.StartsWith(notification.ActorName, StringComparison.OrdinalIgnoreCase))
        {
            message = $"{notification.ActorName}: {message}";
        }

        ShowNotification(title, message);

        NotifyUi(() => NotificationReceived?.Invoke(notification));
    }

    private void OnProjectChanged(Guid projectId)
    {
        _ = _signalRService.JoinProjectAsync(projectId);
        NotifyUi(() => ProjectsTracksUpdated?.Invoke());
    }

    private void OnTrackChanged(Guid trackId, Guid projectId)
    {
        _ = _signalRService.JoinTrackAsync(trackId);
        _ = _signalRService.JoinProjectAsync(projectId);

        NotifyUi(() =>
        {
            ProjectsTracksUpdated?.Invoke();
            MajorTasksUpdated?.Invoke();
            MinorTasksUpdated?.Invoke();
        });
    }

    private void OnMajorTaskChanged(Guid majorTaskId, Guid trackId)
    {
        NotifyUi(() => MajorTasksUpdated?.Invoke());
    }

    private void OnMinorTaskChanged(Guid minorTaskId, Guid trackId)
    {
        NotifyUi(() => MinorTasksUpdated?.Invoke());
    }

    private void OnEventChanged(Guid eventId, Guid projectId)
    {
        _ = _signalRService.JoinProjectAsync(projectId);
        NotifyUi(() => CalendarUpdated?.Invoke());
    }

    private void OnPostCreated(Guid postId, Guid? projectId, Guid? trackId)
    {
        NotifyUi(() => MediaUpdated?.Invoke());
    }

    private void ShowNotification(string title, string message)
    {
        try
        {
            _trayService.ShowBalloonTip(title, message, 3500);
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
        _signalRService.NotificationReceived -= OnNotificationReceived;
    }
}
