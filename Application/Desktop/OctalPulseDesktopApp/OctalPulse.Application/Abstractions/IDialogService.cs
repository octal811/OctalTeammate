namespace OctalPulse.Application.Abstractions;

public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel");
    Task ShowErrorAsync(string title, string error);
    void ShowToast(string title, string message, ToastType type = ToastType.Info);
}

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}
