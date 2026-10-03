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

        var owner = GetActiveOrMainWindow();
        var window = new FastAddWindow(vm)
        {
            Owner = owner
        };
        if (owner != null && owner.Topmost)
        {
            window.Topmost = true;
        }

        var result = window.ShowDialog();
        return result == true || window.HasImportedTasks;
    }

    public Task<bool> OpenForMajorTaskAsync(Guid majorTaskId, string majorTaskTitle)
    {
        var vm = _serviceProvider.GetRequiredService<FastAddViewModel>();
        vm.InitializeForMajorTask(majorTaskId, majorTaskTitle);

        var owner = GetActiveOrMainWindow();
        var window = new FastAddWindow(vm)
        {
            Owner = owner
        };
        if (owner != null && owner.Topmost)
        {
            window.Topmost = true;
        }

        var result = window.ShowDialog();
        return Task.FromResult(result == true || window.HasImportedTasks);
    }

    public async Task<bool> OpenForNotesAsync()
    {
        var vm = _serviceProvider.GetRequiredService<FastAddViewModel>();
        await vm.InitializeForNotesAsync();

        var owner = GetActiveOrMainWindow();
        var window = new FastAddWindow(vm)
        {
            Owner = owner
        };
        if (owner != null && owner.Topmost)
        {
            window.Topmost = true;
        }

        var result = window.ShowDialog();
        return result == true || window.HasImportedTasks;
    }

    private static Window? GetActiveOrMainWindow()
    {
        var active = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible);
        return active ?? System.Windows.Application.Current?.MainWindow;
    }
}
