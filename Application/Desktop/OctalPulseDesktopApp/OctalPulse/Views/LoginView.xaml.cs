using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using OctalPulse.ViewModels;

namespace OctalPulse.Views;

public partial class LoginView : UserControl
{
    private bool _updatingPassword;

    public LoginView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HookViewModel();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.PropertyChanged -= OnVmPropertyChanged;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel old)
        {
            old.PropertyChanged -= OnVmPropertyChanged;
        }

        HookViewModel();
    }

    private void HookViewModel()
    {
        if (DataContext is not LoginViewModel vm)
        {
            return;
        }

        vm.PropertyChanged -= OnVmPropertyChanged;
        vm.PropertyChanged += OnVmPropertyChanged;

        // Reflect the VM's initial password (e.g. filled from a saved account)
        _updatingPassword = true;
        PwdBox.Password = vm.Password;
        _updatingPassword = false;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.Password) && sender is LoginViewModel vm)
        {
            _updatingPassword = true;
            PwdBox.Password = vm.Password;
            _updatingPassword = false;
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingPassword)
        {
            return;
        }

        if (DataContext is LoginViewModel vm && sender is PasswordBox pwdBox)
        {
            vm.Password = pwdBox.Password;
        }
    }
}