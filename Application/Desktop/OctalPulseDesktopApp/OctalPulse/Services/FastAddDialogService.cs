using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OctalPulse.ViewModels;
using OctalPulse.Views;

namespace OctalPulse.Services;

public class FastAddDialogService : IFastAddDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public FastAddDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> OpenForTrackAsync(Guid? preferredTrackId = null)
    {
        var vm = _serviceProvider.GetRequiredService<FastAddViewModel>();
        await vm.InitializeForTrackAsync(preferredTrackId);

        var window = new FastAddWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var result = window.ShowDialog();
        return result == true || window.HasImportedTasks;
    }

    public Task<bool> OpenForMajorTaskAsync(Guid majorTaskId, string majorTaskTitle)
    {
        var vm = _serviceProvider.GetRequiredService<FastAddViewModel>();
        vm.InitializeForMajorTask(majorTaskId, majorTaskTitle);

        var window = new FastAddWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var result = window.ShowDialog();
        return Task.FromResult(result == true || window.HasImportedTasks);
    }
}
