using System.Collections.ObjectModel;
using System.IO;
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

    public ObservableCollection<ProfilePostItem> Posts { get; } = new();

    public ProfileViewModel(
        IAuthService authService,
        IUserSession userSession,
        ILocalCacheService localCache,
        IDialogService dialogService,
        IPostService postService,
        IBadgeService badgeService)
    {
        _authService = authService;
        _userSession = userSession;
        _localCache = localCache;
        _dialogService = dialogService;
        _postService = postService;
        _badgeService = badgeService;

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
        InitializeFromSession();
        _ = LoadProfileAsync(isSilent: true);
    }

    [RelayCommand]
    private async Task RefreshProfileAsync()
    {
        await LoadProfileAsync(isSilent: false);
    }

    private async Task LoadProfileAsync(bool isSilent = true)
    {
        if (string.IsNullOrEmpty(ProfileName))
        {
            IsLoading = true;
        }

        try
        {
            var me = await _authService.GetProfileAsync();
            if (me == null)
            {
                if (!isSilent)
                {
                    _dialogService.ShowToast("Profile Unavailable", "Could not load your profile.", ToastType.Error);
                }
                return;
            }

            ApplyProfile(me);
            await Task.WhenAll(LoadBadgesAsync(), LoadUserPostsAsync());
            if (!isSilent)
            {
                _dialogService.ShowToast("Profile Loaded", "Your profile was refreshed.", ToastType.Info);
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
        if (!_userSession.IsAuthenticated || !_userSession.UserId.HasValue) return;

        try
        {
            var res = await _postService.GetProfilePostsAsync(_userSession.UserId.Value, 1, 20);
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
        if (!_userSession.IsAuthenticated) return;

        try
        {
            var res = await _badgeService.GetMyBadgesAsync();
            Badges.Clear();
            if (res?.Badges == null) return;

            foreach (var b in res.Badges)
            {
                var (iconKey, brush) = GetBadgeVisuals(b.Type);
                var hasShine = b.CurrentLevel is "Gold" or "Platinum" or "Diamond" or "Legend";
                var isLegend = b.CurrentLevel == "Legend";
                var description = b.CurrentLevel != null
                    ? $"Unlocked!"
                    : $"Progress toward {GetNextLabel(b.Type, b.NextTarget)}";

                Badges.Add(new ProfileBadgeItem(
                    Title: GetBadgeTitle(b.Type),
                    Description: description,
                    Level: b.CurrentLevel ?? "Locked",
                    ProgressPercent: b.ProgressPercent,
                    IsUnlocked: b.CurrentLevel != null,
                    IconKey: iconKey,
                    AccentBrush: brush,
                    HasShine: hasShine,
                    IsLegend: isLegend));
            }
        }
        catch
        {
            Badges.Clear();
        }
    }

    private static string GetBadgeTitle(string type) => type switch
    {
        "CriticalFocus" => "Critical Focus",
        "HeavyWork" => "Heavy Work",
        "BugHunter" => "Bug Hunter",
        _ => type
    };

    private static string GetNextLabel(string type, long nextTarget) => type switch
    {
        "CriticalFocus" => nextTarget >= 3600
            ? $"{nextTarget / 3600}h focus"
            : $"{nextTarget / 60} min focus",
        "HeavyWork" => $"{nextTarget}% contribution",
        "BugHunter" => $"{nextTarget} bugs solved",
        _ => ""
    };

    private static (string IconKey, Brush Brush) GetBadgeVisuals(string type) => type switch
    {
        "CriticalFocus" => ("Icon.Stopwatch", new SolidColorBrush(Color.FromRgb(0xCD, 0x7F, 0x32))),
        "HeavyWork" => ("Icon.Award", new SolidColorBrush(Color.FromRgb(0xE5, 0xE4, 0xE2))),
        "BugHunter" => ("Icon.Shield", new SolidColorBrush(Color.FromRgb(0x4F, 0xD6, 0xF7))),
        _ => ("Icon.Sparkles", new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)))
    };

    private static Brush GetBadgeLevelBrush(string? level) => level switch
    {
        "Bronze" => new SolidColorBrush(Color.FromRgb(0xCD, 0x7F, 0x32)),
        "Silver" => new SolidColorBrush(Color.FromRgb(0xA8, 0xA9, 0xAD)),
        "Gold" => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
        "Platinum" => new SolidColorBrush(Color.FromRgb(0xE5, 0xE4, 0xE2)),
        "Diamond" => new SolidColorBrush(Color.FromRgb(0x4F, 0xD6, 0xF7)),
        "Legend" => new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x25, 0x63, 0xEB), 0),
                new GradientStop(Color.FromRgb(0xEF, 0x44, 0x44), 0.5),
                new GradientStop(Color.FromRgb(0xFA, 0xCC, 0x15), 1)
            }
        },
        _ => new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))
    };

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
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

    private void ApplyProfile(UserProfileResponse profile)
    {
        ProfileName = profile.Name;
        ProfileRole = profile.MainRole;
        ProfileRank = profile.Rank;
        ProfileEmail = profile.Email;
        Bio = profile.Bio ?? string.Empty;
        ProfileImageUrl = profile.ProfilePictureUrl;
        BackgroundImageUrl = profile.BackgroundImageUrl;
        _userSession.SetSession(profile.Id, profile.Email, profile.Name, profile.MainRole, profile.Rank, profile.ProfilePictureUrl);
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
    string Title,
    string Description,
    string Level,
    int ProgressPercent,
    bool IsUnlocked,
    string IconKey,
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