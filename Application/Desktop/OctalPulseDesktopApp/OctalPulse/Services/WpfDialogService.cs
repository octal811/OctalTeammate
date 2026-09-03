using System.Windows;
using OctalPulse.Application.Abstractions;

namespace OctalPulse.Services;

public class WpfDialogService : IDialogService
{
    public event Action<string, string, ToastType>? ToastRequested;

    public Task ShowMessageAsync(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel")
    {
        var result = System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    public Task ShowErrorAsync(string title, string error)
    {
        System.Windows.MessageBox.Show(error, title, MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }

    public void ShowToast(string title, string message, ToastType type = ToastType.Info)
    {
        ToastRequested?.Invoke(title, message, type);
    }
}
