using System.Collections.ObjectModel;
using System.IO;
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
        IPostService postService)
    {
        _authService = authService;
        _userSession = userSession;
        _localCache = localCache;
        _dialogService = dialogService;
        _postService = postService;

        InitializeFromSession();
        InitializeShowcaseData();
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
            await LoadUserPostsAsync();
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
            if (res.Items != null && res.Items.Count > 0)
            {
                Posts.Clear();
                foreach (var p in res.Items)
                {
                    Posts.Add(new ProfilePostItem(
                        Title: p.Content.Length > 40 ? p.Content[..40] + "..." : p.Content,
                        Category: "General Note",
                        TimeAgo: FormatTimeAgo(p.CreatedDate),
                        Content: p.Content,
                        Tags: string.Empty,
                        LikesCount: p.TotalReactions,
                        CommentsCount: p.CommentsCount));
                }
            }
        }
        catch
        {
            // fallback gracefully to showcase data
        }
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

    private void InitializeShowcaseData()
    {
        Badges.Clear();
        Badges.Add(new ProfileBadgeItem(
            "Early Adopter",
            "Founding contributor in OctalPulse workspace",
            "Workspace",
            "Diamond",
            100,
            true,
            "Icon.Sparkles",
            "#38BDF8"));

        Badges.Add(new ProfileBadgeItem(
            "Sprint Champion",
            "Delivered all planned sprint deliverables on time",
            "Agile",
            "Platinum",
            100,
            true,
            "Icon.Trophy",
            "#A855F7"));

        Badges.Add(new ProfileBadgeItem(
            "Bug Hunter",
            "Squashed 25+ critical issues across tracks",
            "Quality",
            "Gold",
            85,
            true,
            "Icon.Award",
            "#F59E0B"));

        Badges.Add(new ProfileBadgeItem(
            "Clean Architect",
            "Zero compiler warnings and solid code review score",
            "Code",
            "Silver",
            60,
            false,
            "Icon.Shield",
            "#6366F1"));

        Posts.Clear();
        Posts.Add(new ProfilePostItem(
            "Sprint 14 Retro & UI Redesign",
            "Engineering Note",
            "2 hours ago",
            "Just shipped the modernized profile and navigation updates. Next milestone will integrate realtime SignalR task notifications and interactive sprint retrospective notes.",
            "#ui #desktop #dotnet10",
            14,
            5));

        Posts.Add(new ProfilePostItem(
            "Local Session DPAPI Storage Rollout",
            "Milestone",
            "Yesterday",
            "Finished migrating the authentication cache to Windows DPAPI encryption for enhanced security and zero-friction desktop launch.",
            "#security #architecture #desktop",
            22,
            8));
    }
}

public record ProfileBadgeItem(
    string Title,
    string Description,
    string Category,
    string Tier,
    int ProgressPercent,
    bool IsUnlocked,
    string IconKey,
    string AccentColor);

public record ProfilePostItem(
    string Title,
    string Category,
    string TimeAgo,
    string Content,
    string Tags,
    int LikesCount,
    int CommentsCount);