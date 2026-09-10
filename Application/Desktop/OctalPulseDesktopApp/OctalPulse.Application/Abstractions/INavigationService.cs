using System.Collections.ObjectModel;
using System.ComponentModel;

namespace OctalPulse.Application.Abstractions;

public interface INavigationService : INotifyPropertyChanged
{
    object? CurrentViewModel { get; }
    bool CanGoBack { get; }
    ObservableCollection<BreadcrumbItem> Breadcrumbs { get; }

    void SetBreadcrumbs(params BreadcrumbItem[] items);

    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo<TViewModel>(object parameter) where TViewModel : class;
    void GoBack();
}
