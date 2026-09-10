using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using OctalPulse.Application.Abstractions;
using OctalPulse.ViewModels;

namespace OctalPulse.Services;

public partial class WpfNavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<(Type ViewModelType, object? Parameter)> _history = new();

    [ObservableProperty]
    private object? _currentViewModel;

    public bool CanGoBack => _history.Count > 1;

    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();

    public WpfNavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        NavigateTo<TViewModel>(null!);
    }

    public void SetBreadcrumbs(params BreadcrumbItem[] items)
    {
        Breadcrumbs.Clear();
        for (var i = 0; i < items.Length; i++)
        {
            items[i].IsFirst = i == 0;
            items[i].IsCurrent = i == items.Length - 1;
            Breadcrumbs.Add(items[i]);
        }
    }

    public void NavigateTo<TViewModel>(object parameter) where TViewModel : class
    {
        var targetType = typeof(TViewModel);

        SetBreadcrumbs(DefaultBreadcrumb(targetType));

        if (IsInnerShellView(targetType))
        {
            var shellVm = _serviceProvider.GetRequiredService<ShellViewModel>();
            if (CurrentViewModel != shellVm)
            {
                CurrentViewModel = shellVm;
            }

            var vm = _serviceProvider.GetRequiredService<TViewModel>();
            if (vm is INavigationAware navAware)
            {
                navAware.OnNavigatedTo(parameter);
            }

            _history.Push((targetType, parameter));
            shellVm.CurrentView = vm;
            OnPropertyChanged(nameof(CanGoBack));
        }
        else
        {
            var vm = _serviceProvider.GetRequiredService<TViewModel>();
            if (vm is INavigationAware navAware)
            {
                navAware.OnNavigatedTo(parameter);
            }

            _history.Push((targetType, parameter));
            CurrentViewModel = vm;
            OnPropertyChanged(nameof(CanGoBack));
        }
    }

    public void GoBack()
    {
        if (_history.Count <= 1) return;

        _history.Pop(); // current
        var previous = _history.Peek();

        if (IsInnerShellView(previous.ViewModelType))
        {
            var shellVm = _serviceProvider.GetRequiredService<ShellViewModel>();
            if (CurrentViewModel != shellVm)
            {
                CurrentViewModel = shellVm;
            }

            var vm = _serviceProvider.GetRequiredService(previous.ViewModelType);
            if (vm is INavigationAware navAware)
            {
                navAware.OnNavigatedTo(previous.Parameter);
            }

            shellVm.CurrentView = vm;
        }
        else
        {
            var vm = _serviceProvider.GetRequiredService(previous.ViewModelType);
            if (vm is INavigationAware navAware)
            {
                navAware.OnNavigatedTo(previous.Parameter);
            }

            CurrentViewModel = vm;
        }

        OnPropertyChanged(nameof(CanGoBack));
    }

    private static bool IsInnerShellView(Type type)
    {
        return type == typeof(DashboardViewModel) ||
               type == typeof(ProjectsViewModel) ||
               type == typeof(ProjectDetailViewModel) ||
               type == typeof(TrackDetailViewModel) ||
               type == typeof(MajorTaskDetailViewModel) ||
               type == typeof(CalendarViewModel) ||
               type == typeof(TasksViewModel) ||
               type == typeof(SettingsViewModel) ||
               type == typeof(StopwatchViewModel) ||
               type == typeof(ProfileViewModel) ||
               type == typeof(MediaViewModel);
    }

    private static BreadcrumbItem DefaultBreadcrumb(Type viewModelType)
    {
        var label = viewModelType.Name switch
        {
            nameof(DashboardViewModel) => "Dashboard",
            nameof(ProjectsViewModel) => "Projects",
            nameof(ProjectDetailViewModel) => "Projects",
            nameof(TrackDetailViewModel) => "Projects",
            nameof(CalendarViewModel) => "Calendar",
            nameof(TasksViewModel) => "Tasks Center",
            nameof(MajorTaskDetailViewModel) => "Tasks Center",
            nameof(SettingsViewModel) => "Settings & GitHub",
            nameof(StopwatchViewModel) => "Stopwatch",
            nameof(ProfileViewModel) => "Profile",
            nameof(MediaViewModel) => "Media",
            _ => viewModelType.Name.Replace("ViewModel", string.Empty, StringComparison.Ordinal)
        };

        return new BreadcrumbItem(label);
    }
}

public interface INavigationAware
{
    void OnNavigatedTo(object? parameter);
}
