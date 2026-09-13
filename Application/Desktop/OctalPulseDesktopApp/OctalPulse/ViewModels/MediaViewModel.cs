using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public record ProjectPickerOption(Guid? Id, string DisplayName);
public record TrackPickerOption(Guid? Id, string DisplayName);

public partial class MediaViewModel : ObservableObject, INavigationAware, INavigationFromAware
{
    private const int MaxPostLength = 1800;

    private readonly IPostService _postService;
    private readonly IProjectService _projectService;
    private readonly ITrackService _trackService;
    private readonly IUserSession _userSession;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool _isLoadingFeed;

    [ObservableProperty]
    private bool _isCreatingPost;

    [ObservableProperty]
    private string _newPostContent = string.Empty;

    [ObservableProperty]
    private string _newPostPhotoUrl = string.Empty;

    [ObservableProperty]
    private int _remainingCharacters = MaxPostLength;

    [ObservableProperty]
    private ProjectPickerOption? _selectedProject;

    [ObservableProperty]
    private TrackPickerOption? _selectedTrack;

    [ObservableProperty]
    private ProjectPickerOption? _filterProject;

    [ObservableProperty]
    private bool _filterOnlyGeneral;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private bool _hasNextPage;

    public ObservableCollection<ProjectPickerOption> AvailableProjects { get; } = new();
    public ObservableCollection<TrackPickerOption> AvailableTracks { get; } = new();
    public ObservableCollection<ProjectPickerOption> FilterProjectOptions { get; } = new();
    public ObservableCollection<PostCardViewModel> Posts { get; } = new();

    /// <summary>True when a real project is selected (not the general/personal option).</summary>
    public bool IsProjectSelected => SelectedProject?.Id.HasValue == true;

    public IUserSession UserSession => _userSession;

    public MediaViewModel(
        IPostService postService,
        IProjectService projectService,
        ITrackService trackService,
        IUserSession userSession,
        ISignalRRealtimeService signalRService,
        IDialogService dialogService)
    {
        _postService = postService;
        _projectService = projectService;
        _trackService = trackService;
        _userSession = userSession;
        _signalRService = signalRService;
        _dialogService = dialogService;

        _signalRService.PostCreated += OnRealtimePostCreated;
        _signalRService.PostUpdated += OnRealtimePostUpdated;
        _signalRService.PostDeleted += OnRealtimePostDeleted;
        _signalRService.PostReactionChanged += OnRealtimePostReactionChanged;
        _signalRService.CommentAdded += OnRealtimeCommentAdded;
        _signalRService.CommentDeleted += OnRealtimeCommentDeleted;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _ = InitializeAsync();
    }

    public void OnNavigatedFrom()
    {
        _signalRService.PostCreated -= OnRealtimePostCreated;
        _signalRService.PostUpdated -= OnRealtimePostUpdated;
        _signalRService.PostDeleted -= OnRealtimePostDeleted;
        _signalRService.PostReactionChanged -= OnRealtimePostReactionChanged;
        _signalRService.CommentAdded -= OnRealtimeCommentAdded;
        _signalRService.CommentDeleted -= OnRealtimeCommentDeleted;
    }

    private async Task InitializeAsync()
    {
        await LoadProjectsAsync();
        await LoadFeedAsync(page: 1);
    }

    partial void OnNewPostContentChanged(string value)
    {
        RemainingCharacters = MaxPostLength - (value?.Length ?? 0);
    }

    partial void OnSelectedProjectChanged(ProjectPickerOption? value)
    {
        OnPropertyChanged(nameof(IsProjectSelected));
        _ = LoadTracksForSelectedProjectAsync(value?.Id);
    }

    partial void OnFilterProjectChanged(ProjectPickerOption? value)
    {
        if (value != null && value.Id.HasValue)
        {
            FilterOnlyGeneral = false;
        }
        _ = LoadFeedAsync(page: 1);
    }

    partial void OnFilterOnlyGeneralChanged(bool value)
    {
        if (value)
        {
            FilterProject = FilterProjectOptions.FirstOrDefault(p => !p.Id.HasValue);
        }
        _ = LoadFeedAsync(page: 1);
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            var res = await _projectService.GetAllProjectsAsync(1, 100);
            AvailableProjects.Clear();
            FilterProjectOptions.Clear();

            var generalOption = new ProjectPickerOption(null, "🌐 All Projects / General (Personal)");
            AvailableProjects.Add(generalOption);
            SelectedProject = generalOption;

            var allFilter = new ProjectPickerOption(null, "All Feed");
            FilterProjectOptions.Add(allFilter);
            FilterProject = allFilter;

            if (res.Items != null)
            {
                foreach (var p in res.Items)
                {
                    var opt = new ProjectPickerOption(p.Id, p.Title);
                    AvailableProjects.Add(opt);
                    FilterProjectOptions.Add(opt);
                }
            }
        }
        catch
        {
            // fallback if offline
        }
    }

    private async Task LoadTracksForSelectedProjectAsync(Guid? projectId)
    {
        AvailableTracks.Clear();
        var allTracks = new TrackPickerOption(null, "🌐 All Tracks (whole project)");
        AvailableTracks.Add(allTracks);
        SelectedTrack = allTracks;

        if (!projectId.HasValue) return;

        try
        {
            var tracks = await _projectService.GetTracksByProjectAsync(projectId.Value);
            if (tracks?.Tracks != null)
            {
                foreach (var t in tracks.Tracks)
                {
                    AvailableTracks.Add(new TrackPickerOption(t.Id, t.Name));
                }
            }
        }
        catch
        {
            // Ignore error loading tracks
        }
    }

    [RelayCommand]
    public async Task LoadFeedAsync(int page = 1)
    {
        if (IsLoadingFeed) return;
        IsLoadingFeed = true;

        try
        {
            var req = new GetFeedPostsClientRequest(
                PageNumber: page,
                PageSize: 10,
                ProjectId: FilterProject?.Id,
                OnlyGeneral: FilterOnlyGeneral);

            var feed = await _postService.GetFeedPostsAsync(req);
            CurrentPage = feed.PageNumber;
            TotalPages = feed.TotalPages;
            HasNextPage = CurrentPage < TotalPages;

            if (page == 1)
            {
                Posts.Clear();
            }

            if (feed.Items != null)
            {
                foreach (var item in feed.Items)
                {
                    Posts.Add(new PostCardViewModel(item, _postService, _userSession, _dialogService, OnDeletePostRequested));
                }
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Feed Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsLoadingFeed = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasNextPage || IsLoadingFeed) return;
        await LoadFeedAsync(CurrentPage + 1);
    }

    [RelayCommand]
    private async Task RefreshFeedAsync()
    {
        await LoadFeedAsync(1);
    }

    [RelayCommand]
    private async Task CreatePostAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPostContent))
        {
            _dialogService.ShowToast("Post Empty", "Please write something to share.", ToastType.Warning);
            return;
        }

        if (NewPostContent.Length > MaxPostLength)
        {
            _dialogService.ShowToast("Limit Exceeded", $"Post cannot exceed {MaxPostLength} characters.", ToastType.Warning);
            return;
        }

        IsCreatingPost = true;
        try
        {
            var req = new CreatePostClientRequest(
                Content: NewPostContent.Trim(),
                PhotoUrl: string.IsNullOrWhiteSpace(NewPostPhotoUrl) ? null : NewPostPhotoUrl.Trim(),
                ProjectId: SelectedProject?.Id,
                TrackId: SelectedTrack?.Id);

            await _postService.CreatePostAsync(req);
            // Post will appear via SignalR postCreated event — no local insert to avoid duplicates.

            NewPostContent = string.Empty;
            NewPostPhotoUrl = string.Empty;
            _dialogService.ShowToast("Published", "Your post has been shared successfully.", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Post Failed", ex.Message, ToastType.Error);
        }
        finally
        {
            IsCreatingPost = false;
        }
    }

    private void OnDeletePostRequested(PostCardViewModel card)
    {
        Posts.Remove(card);
    }

    // Realtime Handlers
    private void OnRealtimePostCreated(Guid postId, Guid? projectId, Guid? trackId)
    {
        RunOnUi(async () =>
        {
            if (Posts.Any(p => p.Id == postId)) return;
            try
            {
                var post = await _postService.GetPostByIdAsync(postId);
                if (MatchesCurrentFilter(post))
                {
                    Posts.Insert(0, new PostCardViewModel(post, _postService, _userSession, _dialogService, OnDeletePostRequested));
                }
            }
            catch { }
        });
    }

    private void OnRealtimePostUpdated(Guid postId, Guid? projectId, Guid? trackId)
    {
        RunOnUi(async () =>
        {
            var existing = Posts.FirstOrDefault(p => p.Id == postId);
            if (existing != null)
            {
                try
                {
                    var updated = await _postService.GetPostByIdAsync(postId);
                    existing.UpdateFrom(updated);
                }
                catch { }
            }
        });
    }

    private void OnRealtimePostDeleted(Guid postId, Guid? projectId, Guid? trackId)
    {
        RunOnUi(() =>
        {
            var existing = Posts.FirstOrDefault(p => p.Id == postId);
            if (existing != null)
            {
                Posts.Remove(existing);
            }
        });
    }

    private void OnRealtimePostReactionChanged(Guid postId, Guid? projectId)
    {
        RunOnUi(async () =>
        {
            var existing = Posts.FirstOrDefault(p => p.Id == postId);
            if (existing != null)
            {
                try
                {
                    var updated = await _postService.GetPostByIdAsync(postId);
                    existing.UpdateFrom(updated);
                }
                catch { }
            }
        });
    }

    private void OnRealtimeCommentAdded(Guid postId, Guid commentId, Guid? parentCommentId, Guid? projectId)
    {
        RunOnUi(async () =>
        {
            var existing = Posts.FirstOrDefault(p => p.Id == postId);
            if (existing != null)
            {
                existing.CommentsCount++;
                if (existing.IsCommentsExpanded)
                {
                    await existing.LoadCommentsAsync();
                }
            }
        });
    }

    private void OnRealtimeCommentDeleted(Guid postId, Guid commentId, Guid? projectId)
    {
        RunOnUi(async () =>
        {
            var existing = Posts.FirstOrDefault(p => p.Id == postId);
            if (existing != null)
            {
                if (existing.CommentsCount > 0) existing.CommentsCount--;
                if (existing.IsCommentsExpanded)
                {
                    await existing.LoadCommentsAsync();
                }
            }
        });
    }

    private bool MatchesCurrentFilter(PostItem post)
    {
        if (FilterOnlyGeneral)
            return !post.ProjectId.HasValue;

        if (FilterProject?.Id.HasValue == true)
            return post.ProjectId == FilterProject.Id.Value;

        return true;
    }

    private static void RunOnUi(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;
        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.BeginInvoke(action);
    }
}

public partial class PostCardViewModel : ObservableObject
{
    private const int MaxCommentLength = 800;

    private readonly IPostService _postService;
    private readonly IUserSession _userSession;
    private readonly IDialogService _dialogService;
    private readonly Action<PostCardViewModel> _onDeleteCallback;

    private static readonly HttpClient _mediaHttp = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly Regex DriveFileIdRegex = new(@"/file/d/([^/?#]+)", RegexOptions.IgnoreCase);
    private static readonly Regex DriveOpenIdRegex = new(@"/open\?[^#]*?id=([^&?#]+)", RegexOptions.IgnoreCase);
    private static readonly Regex DriveUcIdRegex = new(@"/uc\?[^#]*?[?&]id=([^&?#]+)", RegexOptions.IgnoreCase);
    private int _mediaGeneration;

    public Guid Id { get; private set; }
    public Guid AuthorId { get; private set; }

    [ObservableProperty]
    private string _authorName = string.Empty;

    [ObservableProperty]
    private UserRole _authorRole;

    [ObservableProperty]
    private string? _authorAvatarUrl;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string? _photoUrl;

    public bool IsImageMedia { get; private set; }
    public bool IsExternalLink { get; private set; }

    [ObservableProperty]
    private Guid? _projectId;

    [ObservableProperty]
    private string? _projectTitle;

    [ObservableProperty]
    private Guid? _trackId;

    [ObservableProperty]
    private string? _trackName;

    [ObservableProperty]
    private DateTime _createdDate;

    [ObservableProperty]
    private string _timeAgo = string.Empty;

    [ObservableProperty]
    private int _likeCount;

    [ObservableProperty]
    private int _loveCount;

    [ObservableProperty]
    private int _celebrateCount;

    [ObservableProperty]
    private int _insightfulCount;

    [ObservableProperty]
    private int _totalReactions;

    [ObservableProperty]
    private ReactionType? _userReaction;

    [ObservableProperty]
    private int _commentsCount;

    [ObservableProperty]
    private bool _isAuthor;

    [ObservableProperty]
    private bool _isCommentsExpanded;

    [ObservableProperty]
    private bool _isLoadingComments;

    [ObservableProperty]
    private string _newCommentText = string.Empty;

    [ObservableProperty]
    private int _remainingCommentCharacters = MaxCommentLength;

    [ObservableProperty]
    private bool _isSubmittingComment;

    public bool IsGeneral => !ProjectId.HasValue;
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoUrl);

    public bool IsLiked => UserReaction == ReactionType.Like;
    public bool IsLoved => UserReaction == ReactionType.Love;
    public bool IsCelebrated => UserReaction == ReactionType.Celebrate;
    public bool IsInsightful => UserReaction == ReactionType.Insightful;

    public ObservableCollection<CommentCardViewModel> Comments { get; } = new();

    public PostCardViewModel(
        PostItem item,
        IPostService postService,
        IUserSession userSession,
        IDialogService dialogService,
        Action<PostCardViewModel> onDeleteCallback)
    {
        _postService = postService;
        _userSession = userSession;
        _dialogService = dialogService;
        _onDeleteCallback = onDeleteCallback;

        Id = item.Id;
        AuthorId = item.AuthorId;
        UpdateFrom(item);
    }

    public void UpdateFrom(PostItem item)
    {
        AuthorName = item.AuthorName;
        AuthorRole = item.AuthorRole;
        AuthorAvatarUrl = item.AuthorAvatarUrl;
        Content = item.Content;
        PhotoUrl = item.PhotoUrl;
        ProjectId = item.ProjectId;
        ProjectTitle = item.ProjectTitle;
        TrackId = item.TrackId;
        TrackName = item.TrackName;
        CreatedDate = item.CreatedDate;
        TimeAgo = FormatTimeAgo(item.CreatedDate);
        LikeCount = item.LikeCount;
        LoveCount = item.LoveCount;
        CelebrateCount = item.CelebrateCount;
        InsightfulCount = item.InsightfulCount;
        TotalReactions = item.TotalReactions;
        UserReaction = item.UserReaction;
        CommentsCount = item.CommentsCount;
        IsAuthor = _userSession.IsAuthenticated && _userSession.UserId == item.AuthorId;

        OnPropertyChanged(nameof(IsLiked));
        OnPropertyChanged(nameof(IsLoved));
        OnPropertyChanged(nameof(IsCelebrated));
        OnPropertyChanged(nameof(IsInsightful));
        OnPropertyChanged(nameof(IsGeneral));
        OnPropertyChanged(nameof(HasPhoto));
        _ = DetermineMediaKindAsync();
    }

    private async Task DetermineMediaKindAsync()
    {
        var gen = ++_mediaGeneration;
        var url = string.IsNullOrWhiteSpace(PhotoUrl) ? null : PhotoUrl.Trim();

        var image = false;
        var external = false;

        try
        {
            if (url is null)
            {
                // No media attached.
            }
            else
            {
                var driveId = TryGetDriveId(url);
                if (driveId is not null)
                {
                    var direct = $"https://drive.google.com/uc?export=view&id={driveId}";
                    image = await LooksLikeImageAsync(direct, CancellationToken.None);
                }
                else
                {
                    image = LooksLikeDirectImageUrl(url) || await LooksLikeImageAsync(url, CancellationToken.None);
                }

                external = !image;
            }
        }
        catch
        {
            external = true;
        }

        if (gen != _mediaGeneration) return;

        IsImageMedia = image;
        IsExternalLink = external;
        OnPropertyChanged(nameof(IsImageMedia));
        OnPropertyChanged(nameof(IsExternalLink));
    }

    private static string? TryGetDriveId(string url)
    {
        static string? Match(Regex r, string u)
        {
            var m = r.Match(u);
            return m.Success ? m.Groups[1].Value : null;
        }

        return Match(DriveFileIdRegex, url)
            ?? Match(DriveOpenIdRegex, url)
            ?? Match(DriveUcIdRegex, url);
    }

    private static bool LooksLikeDirectImageUrl(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.AbsolutePath : url;
        var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp";
    }

    private static async Task<bool> LooksLikeImageAsync(string url, CancellationToken token)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var resp = await _mediaHttp.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, token);
            if (resp.IsSuccessStatusCode && IsImageContentType(resp.Content.Headers.ContentType?.MediaType))
            {
                return true;
            }

            using var get = new HttpRequestMessage(HttpMethod.Get, url);
            using var getResp = await _mediaHttp.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, token);
            return getResp.IsSuccessStatusCode && IsImageContentType(getResp.Content.Headers.ContentType?.MediaType);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsImageContentType(string? mediaType)
        => !string.IsNullOrEmpty(mediaType) && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void OpenMediaLink()
    {
        if (string.IsNullOrWhiteSpace(PhotoUrl)) return;

        try
        {
            Process.Start(new ProcessStartInfo(PhotoUrl) { UseShellExecute = true });
        }
        catch
        {
            _dialogService.ShowToast("Open Link", "Could not open the link in your browser.", ToastType.Error);
        }
    }

    partial void OnNewCommentTextChanged(string value)
    {
        RemainingCommentCharacters = MaxCommentLength - (value?.Length ?? 0);
    }

    [RelayCommand]
    private async Task ToggleReactionAsync(object? parameter)
    {
        if (parameter is not string typeStr || !Enum.TryParse<ReactionType>(typeStr, out var type))
            return;
        try
        {
            var updated = await _postService.ToggleReactionAsync(Id, type);
            UpdateFrom(updated);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Reaction Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    private async Task ToggleCommentsAsync()
    {
        IsCommentsExpanded = !IsCommentsExpanded;
        if (IsCommentsExpanded && Comments.Count == 0)
        {
            await LoadCommentsAsync();
        }
    }

    [RelayCommand]
    public async Task LoadCommentsAsync()
    {
        if (IsLoadingComments) return;
        IsLoadingComments = true;
        try
        {
            var comments = await _postService.GetCommentsAsync(Id, pageNumber: 1, pageSize: 50);
            Comments.Clear();
            if (comments.Items != null)
            {
                foreach (var c in comments.Items)
                {
                    Comments.Add(new CommentCardViewModel(c, Id, _postService, _userSession, _dialogService, OnCommentDeleted));
                }
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Comments", ex.Message, ToastType.Error);
        }
        finally
        {
            IsLoadingComments = false;
        }
    }

    [RelayCommand]
    private async Task SubmitCommentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCommentText)) return;
        if (NewCommentText.Length > MaxCommentLength)
        {
            _dialogService.ShowToast("Limit Exceeded", $"Comment cannot exceed {MaxCommentLength} characters.", ToastType.Warning);
            return;
        }

        IsSubmittingComment = true;
        try
        {
            await _postService.AddCommentAsync(Id, new AddCommentClientRequest(NewCommentText.Trim()));
            // Comment will appear via SignalR commentAdded event — no local insert to avoid duplicates.
            NewCommentText = string.Empty;
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Comment Failed", ex.Message, ToastType.Error);
        }
        finally
        {
            IsSubmittingComment = false;
        }
    }

    [RelayCommand]
    private async Task DeletePostAsync()
    {
        var confirm = await _dialogService.ShowConfirmationAsync("Delete Post", "Are you sure you want to delete this post?");
        if (!confirm) return;

        try
        {
            await _postService.DeletePostAsync(Id);
            _onDeleteCallback(this);
            _dialogService.ShowToast("Deleted", "Post removed.", ToastType.Info);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Delete Failed", ex.Message, ToastType.Error);
        }
    }

    private void OnCommentDeleted(CommentCardViewModel c)
    {
        Comments.Remove(c);
        if (CommentsCount > 0) CommentsCount--;
    }

    private static string FormatTimeAgo(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt.ToUniversalTime();
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dt.ToLocalTime().ToString("MMM dd, yyyy");
    }
}

public partial class CommentCardViewModel : ObservableObject
{
    private const int MaxReplyLength = 800;

    private readonly Guid _postId;
    private readonly IPostService _postService;
    private readonly IUserSession _userSession;
    private readonly IDialogService _dialogService;
    private readonly Action<CommentCardViewModel> _onDeletedCallback;

    public Guid Id { get; }
    public Guid UserId { get; }

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private UserRole _userRole;

    [ObservableProperty]
    private string? _userAvatarUrl;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private DateTime _createdDate;

    [ObservableProperty]
    private string _timeAgo = string.Empty;

    [ObservableProperty]
    private bool _isAuthor;

    [ObservableProperty]
    private bool _isReplying;

    [ObservableProperty]
    private string _replyText = string.Empty;

    [ObservableProperty]
    private int _remainingReplyCharacters = MaxReplyLength;

    [ObservableProperty]
    private bool _isSubmittingReply;

    public ObservableCollection<CommentCardViewModel> Replies { get; } = new();

    public CommentCardViewModel(
        CommentItem item,
        Guid postId,
        IPostService postService,
        IUserSession userSession,
        IDialogService dialogService,
        Action<CommentCardViewModel> onDeletedCallback)
    {
        _postId = postId;
        _postService = postService;
        _userSession = userSession;
        _dialogService = dialogService;
        _onDeletedCallback = onDeletedCallback;

        Id = item.Id;
        UserId = item.UserId;
        UserName = item.UserName;
        UserRole = item.UserRole;
        UserAvatarUrl = item.UserAvatarUrl;
        Content = item.Content;
        CreatedDate = item.CreatedDate;
        TimeAgo = FormatTimeAgo(item.CreatedDate);
        IsAuthor = _userSession.IsAuthenticated && _userSession.UserId == item.UserId;

        if (item.Replies != null)
        {
            foreach (var r in item.Replies)
            {
                Replies.Add(new CommentCardViewModel(r, postId, postService, userSession, dialogService, OnSubReplyDeleted));
            }
        }
    }

    partial void OnReplyTextChanged(string value)
    {
        RemainingReplyCharacters = MaxReplyLength - (value?.Length ?? 0);
    }

    [RelayCommand]
    private void ToggleReply()
    {
        IsReplying = !IsReplying;
        if (!IsReplying) ReplyText = string.Empty;
    }

    [RelayCommand]
    private async Task SubmitReplyAsync()
    {
        if (string.IsNullOrWhiteSpace(ReplyText)) return;
        if (ReplyText.Length > MaxReplyLength)
        {
            _dialogService.ShowToast("Limit Exceeded", $"Reply cannot exceed {MaxReplyLength} characters.", ToastType.Warning);
            return;
        }

        IsSubmittingReply = true;
        try
        {
            await _postService.AddCommentAsync(_postId, new AddCommentClientRequest(ReplyText.Trim(), ParentCommentId: Id));
            // Reply will appear via SignalR commentAdded event — no local insert to avoid duplicates.
            ReplyText = string.Empty;
            IsReplying = false;
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Reply Failed", ex.Message, ToastType.Error);
        }
        finally
        {
            IsSubmittingReply = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCommentAsync()
    {
        var confirm = await _dialogService.ShowConfirmationAsync("Delete Comment", "Are you sure you want to delete this comment?");
        if (!confirm) return;

        try
        {
            await _postService.DeleteCommentAsync(_postId, Id);
            _onDeletedCallback(this);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Delete Failed", ex.Message, ToastType.Error);
        }
    }

    private void OnSubReplyDeleted(CommentCardViewModel reply)
    {
        Replies.Remove(reply);
    }

    private static string FormatTimeAgo(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt.ToUniversalTime();
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dt.ToLocalTime().ToString("MMM dd, yyyy");
    }
}
