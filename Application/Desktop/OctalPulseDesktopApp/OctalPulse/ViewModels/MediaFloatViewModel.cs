using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class MediaFloatViewModel : ObservableObject
{
    private readonly IPostService _postService;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<PostItem> RecentPosts { get; } = new();

    public MediaFloatViewModel(IPostService postService, IDialogService dialogService)
    {
        _postService = postService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        RecentPosts.Clear();

        try
        {
            var result = await _postService.GetFeedPostsAsync(new GetFeedPostsClientRequest(
                PageNumber: 1,
                PageSize: 10));

            foreach (var post in result.Items)
            {
                RecentPosts.Add(post);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Float Media", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
