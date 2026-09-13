using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Realtime;

public class SignalRInfiniteRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (retryContext.PreviousRetryCount == 0) return TimeSpan.Zero;
        if (retryContext.PreviousRetryCount < 3) return TimeSpan.FromSeconds(2);
        return TimeSpan.FromSeconds(5);
    }
}

public class SignalRRealtimeService : ISignalRRealtimeService
{
    private readonly ILogger<SignalRRealtimeService> _logger;
    private readonly ITokenService _tokenService;
    private readonly string _hubBaseUrl;
    private HubConnection? _hubConnection;
    private readonly HashSet<Guid> _joinedProjects = new();
    private readonly HashSet<Guid> _joinedTracks = new();
    private string? _currentAccessToken;
    private bool _isDisposed;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public event Action<Guid>? ProjectChanged;
    public event Action<Guid, Guid>? TrackChanged;
    public event Action<Guid, Guid>? MajorTaskChanged;
    public event Action<Guid, Guid>? MinorTaskChanged;
    public event Action<Guid, Guid>? EventChanged;
    public event Action<Guid, Guid?, Guid?>? PostCreated;
    public event Action<Guid, Guid?, Guid?>? PostUpdated;
    public event Action<Guid, Guid?, Guid?>? PostDeleted;
    public event Action<Guid, Guid?>? PostReactionChanged;
    public event Action<Guid, Guid, Guid?, Guid?>? CommentAdded;
    public event Action<Guid, Guid, Guid?>? CommentDeleted;
    public event Action<bool>? ConnectionStateChanged;

    public SignalRRealtimeService(string hubBaseUrl, ITokenService tokenService, ILogger<SignalRRealtimeService> logger)
    {
        _hubBaseUrl = hubBaseUrl.TrimEnd('/');
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task ConnectAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;

        _isDisposed = false;
        _currentAccessToken = accessToken;

        if (_hubConnection != null)
        {
            await DisconnectAsync(cancellationToken);
        }

        var hubUrl = $"{_hubBaseUrl}/hubs/collaboration";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () =>
                {
                    var token = _tokenService.GetAccessToken();
                    return Task.FromResult<string?>(!string.IsNullOrEmpty(token) ? token : _currentAccessToken);
                };
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                        {
                            if (message.RequestUri?.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) == true)
                                return true;
                            return errors == System.Net.Security.SslPolicyErrors.None;
                        };
                    }
                    return handler;
                };
            })
            .WithAutomaticReconnect(new SignalRInfiniteRetryPolicy())
            .Build();

        RegisterHubEvents();

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR reconnecting...");
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async connectionId =>
        {
            _logger.LogInformation("SignalR reconnected with connectionId: {ConnectionId}", connectionId);
            ConnectionStateChanged?.Invoke(true);
            await RejoinGroupsAsync();
        };

        _hubConnection.Closed += async error =>
        {
            _logger.LogWarning(error, "SignalR connection closed. Triggering background reconnect...");
            ConnectionStateChanged?.Invoke(false);
            if (!_isDisposed)
            {
                await Task.Delay(3000);
                _ = BackgroundReconnectLoopAsync();
            }
        };

        try
        {
            await _hubConnection.StartAsync(cancellationToken);
            _logger.LogInformation("SignalR connected successfully to {HubUrl}", hubUrl);
            ConnectionStateChanged?.Invoke(true);
            await RejoinGroupsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection to {HubUrl}. Scheduling reconnect...", hubUrl);
            ConnectionStateChanged?.Invoke(false);
            _ = BackgroundReconnectLoopAsync();
        }
    }

    private async Task BackgroundReconnectLoopAsync()
    {
        while (!_isDisposed && (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected))
        {
            var token = _tokenService.GetAccessToken() ?? _currentAccessToken;
            if (string.IsNullOrEmpty(token)) break;

            try
            {
                await Task.Delay(4000);
                if (_isDisposed) break;

                if (_hubConnection != null && _hubConnection.State == HubConnectionState.Disconnected)
                {
                    await _hubConnection.StartAsync();
                    _logger.LogInformation("SignalR background reconnection succeeded!");
                    ConnectionStateChanged?.Invoke(true);
                    await RejoinGroupsAsync();
                    break;
                }
            }
            catch
            {
                // Continue retry loop
            }
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _isDisposed = true;
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync(cancellationToken);
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while stopping SignalR connection.");
            }
            finally
            {
                _hubConnection = null;
                _joinedProjects.Clear();
                _joinedTracks.Clear();
                ConnectionStateChanged?.Invoke(false);
            }
        }
    }

    public async Task JoinProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _joinedProjects.Add(projectId);
        if (IsConnected && _hubConnection != null)
        {
            try
            {
                await _hubConnection.InvokeAsync("JoinProject", projectId, cancellationToken);
                _logger.LogInformation("Joined SignalR project group for {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to join SignalR project group for {ProjectId}", projectId);
            }
        }
    }

    public async Task LeaveProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _joinedProjects.Remove(projectId);
        if (IsConnected && _hubConnection != null)
        {
            try
            {
                await _hubConnection.InvokeAsync("LeaveProject", projectId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to leave SignalR project group for {ProjectId}", projectId);
            }
        }
    }

    public async Task JoinTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        _joinedTracks.Add(trackId);
        if (IsConnected && _hubConnection != null)
        {
            try
            {
                await _hubConnection.InvokeAsync("JoinTrack", trackId, cancellationToken);
                _logger.LogInformation("Joined SignalR track group for {TrackId}", trackId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to join SignalR track group for {TrackId}", trackId);
            }
        }
    }

    public async Task LeaveTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        _joinedTracks.Remove(trackId);
        if (IsConnected && _hubConnection != null)
        {
            try
            {
                await _hubConnection.InvokeAsync("LeaveTrack", trackId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to leave SignalR track group for {TrackId}", trackId);
            }
        }
    }

    public async Task RegisterGroupMembershipAsync(
        IReadOnlyList<Guid> projectIds,
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var projectId in projectIds)
        {
            _joinedProjects.Add(projectId);
        }

        foreach (var trackId in trackIds)
        {
            _joinedTracks.Add(trackId);
        }

        if (!IsConnected || _hubConnection == null) return;

        foreach (var projectId in projectIds)
        {
            try
            {
                await _hubConnection.InvokeAsync("JoinProject", projectId, cancellationToken);
                _logger.LogInformation("Joined SignalR project group for {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to join SignalR project group for {ProjectId}", projectId);
            }
        }

        foreach (var trackId in trackIds)
        {
            try
            {
                await _hubConnection.InvokeAsync("JoinTrack", trackId, cancellationToken);
                _logger.LogInformation("Joined SignalR track group for {TrackId}", trackId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to join SignalR track group for {TrackId}", trackId);
            }
        }
    }

    private void RegisterHubEvents()
    {
        if (_hubConnection == null) return;

        _hubConnection.On<Guid>("projectChanged", projectId =>
        {
            _logger.LogDebug("SignalR projectChanged received: {ProjectId}", projectId);
            ProjectChanged?.Invoke(projectId);
        });

        _hubConnection.On<Guid, Guid>("trackChanged", (trackId, projectId) =>
        {
            _logger.LogDebug("SignalR trackChanged received: Track={TrackId}, Project={ProjectId}", trackId, projectId);
            TrackChanged?.Invoke(trackId, projectId);
        });

        _hubConnection.On<Guid, Guid>("majorTaskChanged", (majorTaskId, trackId) =>
        {
            _logger.LogDebug("SignalR majorTaskChanged received: MajorTask={MajorTaskId}, Track={TrackId}", majorTaskId, trackId);
            MajorTaskChanged?.Invoke(majorTaskId, trackId);
        });

        _hubConnection.On<Guid, Guid>("minorTaskChanged", (minorTaskId, trackId) =>
        {
            _logger.LogDebug("SignalR minorTaskChanged received: MinorTask={MinorTaskId}, Track={TrackId}", minorTaskId, trackId);
            MinorTaskChanged?.Invoke(minorTaskId, trackId);
        });

        _hubConnection.On<Guid, Guid>("eventChanged", (eventId, projectId) =>
        {
            _logger.LogDebug("SignalR eventChanged received: Event={EventId}, Project={ProjectId}", eventId, projectId);
            EventChanged?.Invoke(eventId, projectId);
        });

        _hubConnection.On<Guid, Guid?, Guid?>("postCreated", (postId, projectId, trackId) =>
        {
            _logger.LogDebug("SignalR postCreated received: Post={PostId}", postId);
            PostCreated?.Invoke(postId, projectId, trackId);
        });

        _hubConnection.On<Guid, Guid?, Guid?>("postUpdated", (postId, projectId, trackId) =>
        {
            _logger.LogDebug("SignalR postUpdated received: Post={PostId}", postId);
            PostUpdated?.Invoke(postId, projectId, trackId);
        });

        _hubConnection.On<Guid, Guid?, Guid?>("postDeleted", (postId, projectId, trackId) =>
        {
            _logger.LogDebug("SignalR postDeleted received: Post={PostId}", postId);
            PostDeleted?.Invoke(postId, projectId, trackId);
        });

        _hubConnection.On<Guid, Guid?>("postReactionChanged", (postId, projectId) =>
        {
            _logger.LogDebug("SignalR postReactionChanged received: Post={PostId}", postId);
            PostReactionChanged?.Invoke(postId, projectId);
        });

        _hubConnection.On<Guid, Guid, Guid?, Guid?>("commentAdded", (postId, commentId, parentCommentId, projectId) =>
        {
            _logger.LogDebug("SignalR commentAdded received: Post={PostId}, Comment={CommentId}", postId, commentId);
            CommentAdded?.Invoke(postId, commentId, parentCommentId, projectId);
        });

        _hubConnection.On<Guid, Guid, Guid?>("commentDeleted", (postId, commentId, projectId) =>
        {
            _logger.LogDebug("SignalR commentDeleted received: Post={PostId}, Comment={CommentId}", postId, commentId);
            CommentDeleted?.Invoke(postId, commentId, projectId);
        });
    }

    private async Task RejoinGroupsAsync()
    {
        if (!IsConnected || _hubConnection == null) return;

        foreach (var projectId in _joinedProjects)
        {
            try
            {
                await _hubConnection.InvokeAsync("JoinProject", projectId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error rejoining project group {ProjectId}", projectId);
            }
        }

        foreach (var trackId in _joinedTracks)
        {
            try
            {
                await _hubConnection.InvokeAsync("JoinTrack", trackId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error rejoining track group {TrackId}", trackId);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}
