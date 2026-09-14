using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Converters;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class ProfileViewModel : ObservableObject, INavigationAware
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private readonly IAuthService _authService;
    private readonly IUserSession _userSession;
    private readonly ILocalCacheService _localCache;
    private readonly IDialogService _dialogService;
    private readonly IPostService _postService;
    private readonly IBadgeService _badgeService;
    private readonly IActivityService _activityService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private UserRole _profileRole = UserRole.SoftwareEngineer;

    [ObservableProperty]
    private UserRank _profileRank = UserRank.Member;

    [ObservableProperty]
    private string _profileEmail = string.Empty;

    [ObservableProperty]
    private string _bio = string.Empty;

    [ObservableProperty]
    private string? _profileImageUrl;

    [ObservableProperty]
    private string? _backgroundImageUrl;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isUploadingProfile;

    [ObservableProperty]
    private bool _isUploadingBackground;

    public ObservableCollection<UserRole> AvailableRoles { get; } = new(Enum.GetValues<UserRole>());

    public ObservableCollection<ProfileBadgeItem> Badges { get; } = new();

    public ObservableCollection<ContributionCell> ContributionCells { get; } = new();

    public ObservableCollection<ProfilePostItem> Posts { get; } = new();

    [ObservableProperty]
    private int _unlockedBadgeCount;

    [ObservableProperty]
    private string _unlockedBadgeSummary = "0 unlocked";

    [ObservableProperty]
    private bool _badgesLoadFailed;

    [ObservableProperty]
    private bool _showAllBadges;

    [ObservableProperty]
    private bool _canExpandBadges;

    [ObservableProperty]
    private string _activityMonthLabel = string.Empty;

    [ObservableProperty]
    private string _activityTotalText = string.Empty;

    [ObservableProperty]
    private bool _isActivityLoading;

    [ObservableProperty]
    private bool _activityLoadFailed;

    [ObservableProperty]
    private bool _isOwnProfile = true;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearchingUsers;

    [ObservableProperty]
    private bool _hasSearchResults;

    [ObservableProperty]
    private string? _viewedUserName;

    private Guid? _activeUserId;

    private CancellationTokenSource? _searchCts;

    public ObservableCollection<UserSearchResult> SearchResults { get; } = new();

    public IEnumerable<ProfileBadgeItem> DisplayBadges =>
        ShowAllBadges ? Badges : Badges.Take(4);

    public string ShowAllBadgesText =>
        ShowAllBadges ? "Show less" : $"Show all ({Badges.Count})";

    public ProfileViewModel(
        IAuthService authService,
        IUserSession userSession,
        ILocalCacheService localCache,
        IDialogService dialogService,
        IPostService postService,
        IBadgeService badgeService,
        IActivityService activityService,
        INavigationService navigationService)
    {
        _authService = authService;
        _userSession = userSession;
        _localCache = localCache;
        _dialogService = dialogService;
        _postService = postService;
        _badgeService = badgeService;
        _activityService = activityService;
        _navigationService = navigationService;

        InitializeFromSession();
    }

    private void InitializeFromSession()
    {
        if (_userSession.IsAuthenticated)
        {
            if (!string.IsNullOrEmpty(_userSession.Name)) ProfileName = _userSession.Name;
            if (!string.IsNullOrEmpty(_userSession.Email)) ProfileEmail = _userSession.Email;
            if (_userSession.MainRole.HasValue) ProfileRole = _userSession.MainRole.Value;
            if (_userSession.Rank.HasValue) ProfileRank = _userSession.Rank.Value;
            if (!string.IsNullOrEmpty(_userSession.ProfileImageUrl) && string.IsNullOrEmpty(ProfileImageUrl))
            {
                ProfileImageUrl = _userSession.ProfileImageUrl;
            }
        }
    }

    public void OnNavigatedTo(object? parameter)
    {
        if (parameter is Guid targetId && targetId != Guid.Empty)
        {
            _activeUserId = targetId;
            IsOwnProfile = targetId == (_userSession.UserId ?? Guid.Empty);
            ProfileName = string.Empty;
            ProfileImageUrl = null;
        }
        else
        {
            _activeUserId = _userSession.UserId;
            IsOwnProfile = true;
            InitializeFromSession();
        }

        ViewedUserName = null;
        ClearSearch();
        _ = LoadProfileAsync(isSilent: true);
    }

    [RelayCommand]
    private async Task RefreshProfileAsync()
    {
        await LoadProfileAsync(isSilent: false);
    }

    [RelayCommand]
    private void ToggleBadges()
    {
        ShowAllBadges = !ShowAllBadges;
        OnPropertyChanged(nameof(DisplayBadges));
        OnPropertyChanged(nameof(ShowAllBadgesText));
    }

    [RelayCommand]
    private void OpenProfile(UserSearchResult user)
    {
        if (user is null) return;
        SearchQuery = string.Empty;
        _navigationService.NavigateTo<ProfileViewModel>(user.Id);
    }

    [RelayCommand]
    private void BackToMyProfile()
    {
        SearchQuery = string.Empty;
        _navigationService.NavigateTo<ProfileViewModel>();
    }

    partial void OnSearchQueryChanged(string value)
    {
        _searchCts?.Cancel();
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            HasSearchResults = false;
            IsSearchingUsers = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            await SearchUsersAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SearchUsersAsync(CancellationToken token)
    {
        IsSearchingUsers = true;
        try
        {
            var response = await _authService.SearchUsersAsync(SearchQuery.Trim(), token);
            SearchResults.Clear();
            if (response?.Results != null)
            {
                foreach (var result in response.Results)
                {
                    SearchResults.Add(result);
                }
            }
            HasSearchResults = SearchResults.Count > 0;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            SearchResults.Clear();
            HasSearchResults = false;
        }
        finally
        {
            IsSearchingUsers = false;
        }
    }

    private void ClearSearch()
    {
        _searchCts?.Cancel();
        _searchCts = null;
        SearchQuery = string.Empty;
        SearchResults.Clear();
        HasSearchResults = false;
    }

    private async Task LoadProfileAsync(bool isSilent = true)
    {
        if (string.IsNullOrEmpty(ProfileName))
        {
            IsLoading = true;
        }

        try
        {
            var targetId = _activeUserId ?? _userSession.UserId;
            if (!targetId.HasValue)
            {
                return;
            }

            var me = IsOwnProfile
                ? await _authService.GetProfileAsync()
                : await _authService.GetProfileByIdAsync(targetId.Value);
            if (me == null)
            {
                if (!isSilent)
                {
                    _dialogService.ShowToast("Profile Unavailable", "Could not load this profile.", ToastType.Error);
                }
                return;
            }

            ApplyProfile(me, updateSession: IsOwnProfile);
            ViewedUserName = IsOwnProfile ? null : me.Name;
            await Task.WhenAll(LoadBadgesAsync(), LoadUserPostsAsync(), LoadContributionsAsync());
            if (!isSilent)
            {
                _dialogService.ShowToast("Profile Loaded", "Profile refreshed.", ToastType.Info);
            }
        }
        catch (Exception ex)
        {
            if (!isSilent)
            {
                _dialogService.ShowToast("Profile Error", ex.Message, ToastType.Error);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadUserPostsAsync()
    {
        var targetId = _activeUserId ?? _userSession.UserId;
        if (!targetId.HasValue) return;

        try
        {
            var res = await _postService.GetProfilePostsAsync(targetId.Value, 1, 20);
            Posts.Clear();
            if (res.Items != null)
            {
                foreach (var p in res.Items)
                {
                    Posts.Add(new ProfilePostItem(
                        Title: p.Content.Length > 40 ? p.Content[..40] + "..." : p.Content,
                        Category: "General Note",
                        TimeAgo: FormatTimeAgo(p.CreatedDate),
                        Content: p.Content,
                        Tags: string.Empty,
                        LikesCount: p.TotalReactions,
                        CommentsCount: p.CommentsCount,
                        PhotoUrl: p.PhotoUrl));
                }
            }
        }
        catch
        {
            Posts.Clear();
        }
    }

    private async Task LoadBadgesAsync()
    {
        var targetId = _activeUserId ?? _userSession.UserId;
        if (!targetId.HasValue) return;

        try
        {
            var res = IsOwnProfile
                ? await _badgeService.GetMyBadgesAsync()
                : await _badgeService.GetBadgesAsync(targetId.Value);
            Badges.Clear();
            BadgesLoadFailed = false;

            if (res?.Badges == null)
            {
                UnlockedBadgeCount = 0;
                UnlockedBadgeSummary = "0 unlocked";
                return;
            }

            foreach (var b in res.Badges)
            {
                var isUnlocked = !string.IsNullOrEmpty(b.CurrentLevel);
                var level = b.CurrentLevel ?? "Locked";
                var accent = FreezeBrush(GetBadgeLevelBrush(b.CurrentLevel));
                var hasShine = b.CurrentLevel is "Gold" or "Platinum" or "Diamond" or "Legend";
                var isLegend = b.CurrentLevel == "Legend";
                var progressLabel = FormatProgressLabel(b.Type, b.CurrentValue, b.NextTarget, isUnlocked && b.ProgressPercent >= 100);
                var progressPercent = Math.Clamp(b.ProgressPercent, 0, 100);
                var status = isUnlocked
                    ? (b.AwardedDate.HasValue
                        ? $"Earned {b.AwardedDate.Value.ToLocalTime():MMM dd, yyyy}"
                        : "Unlocked from your work")
                    : $"Next: {FormatMetric(b.Type, b.NextTarget)}";

                Badges.Add(new ProfileBadgeItem(
                    Type: b.Type,
                    Title: GetBadgeTitle(b.Type),
                    Description: GetBadgeDescription(b.Type),
                    StatusText: status,
                    ProgressLabel: progressLabel,
                    ProgressPercentText: $"{progressPercent}%",
                    Level: level,
                    ProgressPercent: progressPercent,
                    IsUnlocked: isUnlocked,
                    IconGeometry: ResolveIconGeometry(GetBadgeIconKey(b.Type)),
                    AccentBrush: accent,
                    HasShine: hasShine,
                    IsLegend: isLegend));
            }

            UnlockedBadgeCount = Badges.Count(x => x.IsUnlocked);
            UnlockedBadgeSummary = $"{UnlockedBadgeCount}/{Badges.Count} unlocked";
            CanExpandBadges = Badges.Count > 4;
            ShowAllBadges = false;
            OnPropertyChanged(nameof(DisplayBadges));

            var ranked = Badges
                .OrderByDescending(b => b.IsUnlocked)
                .ThenByDescending(b => RankLevel(b.Level))
                .ThenByDescending(b => b.ProgressPercent)
                .ToList();

            Badges.Clear();
            foreach (var badge in ranked)
            {
                Badges.Add(badge);
            }

            OnPropertyChanged(nameof(DisplayBadges));
            OnPropertyChanged(nameof(ShowAllBadgesText));
        }
        catch
        {
            Badges.Clear();
            UnlockedBadgeCount = 0;
            UnlockedBadgeSummary = "0 unlocked";
            BadgesLoadFailed = true;
        }
    }

    private IReadOnlyList<DailyActivityResponse> _activityDays = Array.Empty<DailyActivityResponse>();

    private async Task LoadContributionsAsync()
    {
        var targetId = _activeUserId ?? _userSession.UserId;
        if (!targetId.HasValue) return;

        IsActivityLoading = true;
        ActivityLoadFailed = false;
        ContributionCells.Clear();

        try
        {
            var now = DateTime.Now;
            var res = IsOwnProfile
                ? await _activityService.GetMonthlyActivityAsync(now.Year, now.Month)
                : await _activityService.GetMonthlyActivityAsync(targetId.Value, now.Year, now.Month);
            if (res?.Days == null || res.Days.Count == 0)
            {
                ActivityLoadFailed = true;
                return;
            }

            _activityDays = res.Days;
            ActivityMonthLabel = res.Days[0].Date.ToString("MMMM yyyy");
            RebuildContributionGrid();
        }
        catch
        {
            ActivityLoadFailed = true;
            ContributionCells.Clear();
        }
        finally
        {
            IsActivityLoading = false;
        }
    }

    private void RebuildContributionGrid()
    {
        ContributionCells.Clear();

        if (_activityDays.Count == 0) return;

        var firstDate = _activityDays[0].Date;
        var monthStartOffset = (int)firstDate.DayOfWeek;

        long totalTasks = 0;
        var nonzero = new List<long>(_activityDays.Count);
        var cells = new List<ContributionCell>(_activityDays.Count);
        var levels = new List<int>(_activityDays.Count);

        for (var i = 0; i < _activityDays.Count; i++)
        {
            var day = _activityDays[i];
            totalTasks += day.CompletedTasks;

            var value = day.CompletedTasks;
            if (value > 0)
            {
                nonzero.Add(value);
            }

            var date = firstDate.AddDays(i);
            var row = (int)date.DayOfWeek;
            var column = (monthStartOffset + i) / 7;

            var toolTip = value > 0
                ? $"{value} completed task{(value == 1 ? "" : "s")} · {date:ddd, MMM d}"
                : $"No activity · {date:ddd, MMM d}";

            cells.Add(new ContributionCell(row, column, value > 0, null, toolTip, date.Day.ToString()));
            levels.Add(value > 0 ? 0 : -1);
        }

        if (nonzero.Count > 0)
        {
            var min = nonzero.Min();
            var max = nonzero.Max();

            for (var i = 0; i < cells.Count; i++)
            {
                if (levels[i] < 0) continue;

                var value = _activityDays[i].CompletedTasks;
                var ratio = max == min
                    ? 1d
                    : (double)(value - min) / (max - min);
                var bucket = Math.Clamp((int)Math.Ceiling(ratio * 4), 1, 4);
                cells[i] = cells[i] with { Fill = ContributionBrush(bucket) };
            }
        }

        ActivityTotalText = $"{totalTasks} task{(totalTasks == 1 ? "" : "s")} completed";

        foreach (var cell in cells)
        {
            ContributionCells.Add(cell);
        }
    }

    private static Brush ContributionBrush(int bucket)
    {
        const byte greenR = 0x10;
        const byte greenG = 0xB9;
        const byte greenB = 0x81;

        var alpha = (byte)(bucket switch
        {
            1 => 0x66,
            2 => 0x99,
            3 => 0xCC,
            _ => 0xFF
        });

        var brush = new SolidColorBrush(Color.FromArgb(alpha, greenR, greenG, greenB));
        brush.Freeze();
        return brush;
    }

    private static string GetBadgeTitle(string type) => type switch
    {
        "CriticalFocus" => "Critical Focus",
        "HeavyWork" => "Heavy Work",
        "BugHunter" => "Bug Hunter",
        "WorkTitan" => "Work Titan",
        "StreakMaster" => "Streak Master",
        "TaskFinisher" => "Task Finisher",
        "AllRounder" => "All-Rounder",
        "CommunityVoice" => "Community Voice",
        "TeamCaptain" => "Team Captain",
        "TeamOrganizer" => "Team Organizer",
        _ => type
    };

    private static string GetBadgeDescription(string type) => type switch
    {
        "CriticalFocus" => "Best single-day focused work time tracked on tasks.",
        "HeavyWork" => "Highest share of minor tasks you finished on a completed major.",
        "BugHunter" => "Bugs you solved by completing Solve Bug tasks.",
        "WorkTitan" => "Lifetime work time you tracked on tasks.",
        "StreakMaster" => "Longest run of consecutive days with logged work.",
        "TaskFinisher" => "Tasks you created that you completed.",
        "AllRounder" => "Different task types you completed a task in.",
        "CommunityVoice" => "Posts and comments you shared with your teams.",
        "TeamCaptain" => "Workstream tracks you currently lead.",
        "TeamOrganizer" => "Most major tasks you created within one track.",
        _ => "Achievement earned from your team activity."
    };

    private static string GetBadgeIconKey(string type) => type switch
    {
        "CriticalFocus" => "Icon.Stopwatch",
        "HeavyWork" => "Icon.Award",
        "BugHunter" => "Icon.Shield",
        "WorkTitan" => "Icon.Stopwatch",
        "StreakMaster" => "Icon.Calendar",
        "TaskFinisher" => "Icon.CheckCircle",
        "AllRounder" => "Icon.Sparkles",
        "CommunityVoice" => "Icon.MessageSquare",
        "TeamCaptain" => "Icon.Shield",
        "TeamOrganizer" => "Icon.TeamOrganizer",
        _ => "Icon.Trophy"
    };

    private static string FormatProgressLabel(string type, long current, long next, bool maxed) =>
        maxed
            ? $"{FormatMetric(type, current)} · Max level"
            : $"{FormatMetric(type, current)} / {FormatMetric(type, next)}";

    private static string FormatMetric(string type, long value) => type switch
    {
        "CriticalFocus" => FormatFocusDuration(value),
        "HeavyWork" => $"{value}%",
        "BugHunter" => value == 1 ? "1 bug" : $"{value} bugs",
        "WorkTitan" => FormatFocusDuration(value),
        "StreakMaster" => value == 1 ? "1 day" : $"{value} days",
        "TaskFinisher" => value == 1 ? "1 task" : $"{value} tasks",
        "AllRounder" => value == 1 ? "1 type" : $"{value} types",
        "CommunityVoice" => value == 1 ? "1 share" : $"{value} shares",
        "TeamCaptain" => value == 1 ? "1 track" : $"{value} tracks",
        "TeamOrganizer" => value == 1 ? "1 task" : $"{value} tasks",
        _ => value.ToString()
    };

    private static string FormatFocusDuration(long seconds)
    {
        if (seconds <= 0) return "0 min";
        if (seconds < 3600)
        {
            var minutes = Math.Max(1, (int)Math.Round(seconds / 60.0));
            return $"{minutes} min";
        }

        var hours = seconds / 3600.0;
        return hours == Math.Floor(hours)
            ? $"{(int)hours}h"
            : $"{hours:0.#}h";
    }

    private static Geometry ResolveIconGeometry(string resourceKey)
    {
        if (System.Windows.Application.Current?.TryFindResource(resourceKey) is Geometry geometry)
        {
            return geometry;
        }

        return System.Windows.Application.Current?.TryFindResource("Icon.Trophy") as Geometry
               ?? Geometry.Empty;
    }

    private static Brush FreezeBrush(Brush brush)
    {
        if (brush.CanFreeze && !brush.IsFrozen)
        {
            brush.Freeze();
        }

        return brush;
    }

    private static int RankLevel(string? level) => level switch
    {
        "Bronze" => 1,
        "Silver" => 2,
        "Gold" => 3,
        "Platinum" => 4,
        "Diamond" => 5,
        "Legend" => 6,
        _ => 0
    };

    private static Brush GetBadgeLevelBrush(string? level) => level switch
    {
        "Bronze" => new SolidColorBrush(Color.FromRgb(0xCD, 0x7F, 0x32)),
        "Silver" => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
        "Gold" => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
        "Platinum" => new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
        "Diamond" => new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
        "Legend" => new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x25, 0x63, 0xEB), 0),
                new GradientStop(Color.FromRgb(0xA8, 0x55, 0xF7), 0.45),
                new GradientStop(Color.FromRgb(0xF5, 0x9E, 0x0B), 1)
            }
        },
        _ => new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
    };

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (!IsOwnProfile) return;

        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            _dialogService.ShowToast("Validation Error", "Name cannot be empty.", ToastType.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            var updated = await _authService.UpdateProfileAsync(new UpdateUserProfileRequest(
                ProfileName.Trim(),
                ProfileRole,
                string.IsNullOrWhiteSpace(Bio) ? null : Bio.Trim(),
                null));

            ApplyProfile(updated);
            await PersistSessionAsync(updated);
            _dialogService.ShowToast("Profile Updated", "Your profile details have been saved.", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Profile Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task UploadProfileImageAsync()
    {
        await PickAndUploadAsync("profile", value => IsUploadingProfile = value);
    }

    [RelayCommand]
    private async Task UploadBackgroundImageAsync()
    {
        await PickAndUploadAsync("background", value => IsUploadingBackground = value);
    }

    private async Task PickAndUploadAsync(string imageType, Action<bool> setUploading)
    {
        if (!IsOwnProfile) return;

        var dialog = new OpenFileDialog
        {
            Title = imageType == "background" ? "Choose a background image" : "Choose a profile picture",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.webp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.gif"
        };
        if (dialog.ShowDialog() != true) return;

        var fileInfo = new System.IO.FileInfo(dialog.FileName);
        if (fileInfo.Length > MaxImageSizeBytes)
        {
            _dialogService.ShowToast("Image Too Large", "Images must not exceed 5 MB.", ToastType.Warning);
            return;
        }

        var extension = fileInfo.Extension.ToLowerInvariant();
        var contentType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(contentType))
        {
            _dialogService.ShowToast("Unsupported Image", "Only PNG, JPEG, WebP or GIF images are allowed.", ToastType.Warning);
            return;
        }

        setUploading(true);
        try
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(dialog.FileName);
            var updated = await _authService.UploadProfileImageAsync(imageType, bytes, fileInfo.Name, contentType);

            // Pre-cache local bitmap so the UI doesn't need to re-download over HTTP
            try
            {
                var localBitmap = new BitmapImage();
                localBitmap.BeginInit();
                localBitmap.StreamSource = new MemoryStream(bytes);
                localBitmap.CacheOption = BitmapCacheOption.OnLoad;
                localBitmap.EndInit();
                localBitmap.Freeze();

                var newUrl = imageType == "background" ? updated.BackgroundImageUrl : updated.ProfilePictureUrl;
                RelativeUrlToImageSourceConverter.SetCachedImage(newUrl, localBitmap);
            }
            catch
            {
                // Non-critical: falls back to HTTP download
            }

            ApplyProfile(updated);
            await PersistSessionAsync(updated);

            _dialogService.ShowToast(
                imageType == "background" ? "Background Updated" : "Profile Picture Updated",
                $"Your {(imageType == "background" ? "background" : "profile picture")} has been updated.",
                ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Upload Failed", ex.Message, ToastType.Error);
        }
        finally
        {
            setUploading(false);
        }
    }

    private void ApplyProfile(UserProfileResponse profile, bool updateSession = true)
    {
        ProfileName = profile.Name;
        ProfileRole = profile.MainRole;
        ProfileRank = profile.Rank;
        ProfileEmail = profile.Email;
        Bio = profile.Bio ?? string.Empty;
        ProfileImageUrl = profile.ProfilePictureUrl;
        BackgroundImageUrl = profile.BackgroundImageUrl;
        if (updateSession)
        {
            _userSession.SetSession(profile.Id, profile.Email, profile.Name, profile.MainRole, profile.Rank, profile.ProfilePictureUrl);
        }
    }

    private async Task PersistSessionAsync(UserProfileResponse profile)
    {
        var session = await _localCache.GetActiveSessionAsync();
        if (session == null) return;

        session.Name = profile.Name;
        session.MainRole = profile.MainRole;
        session.Rank = profile.Rank;
        await _localCache.SaveSessionAsync(session);
    }

    [RelayCommand]
    private void NotifyFutureFeature(string featureName)
    {
        _dialogService.ShowToast(
            $"{featureName} Coming Soon",
            $"{featureName} is currently in development and will be activated in an upcoming release.",
            ToastType.Info);
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

public record ProfileBadgeItem(
    string Type,
    string Title,
    string Description,
    string StatusText,
    string ProgressLabel,
    string ProgressPercentText,
    string Level,
    int ProgressPercent,
    bool IsUnlocked,
    Geometry IconGeometry,
    Brush AccentBrush,
    bool HasShine,
    bool IsLegend);

public record ProfilePostItem(
    string Title,
    string Category,
    string TimeAgo,
    string Content,
    string Tags,
    int LikesCount,
    int CommentsCount,
    string? PhotoUrl);

public record ContributionCell(
    int Row,
    int Column,
    bool IsActive,
    Brush? Fill,
    string ToolTip,
    string DayNumber);