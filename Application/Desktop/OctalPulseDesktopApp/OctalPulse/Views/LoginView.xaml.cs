using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
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
        if (PwdBox != null) PwdBox.Password = vm.Password;
        if (PwdTextBox != null) PwdTextBox.Text = vm.Password;
        _updatingPassword = false;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LoginViewModel vm) return;

        if (e.PropertyName == nameof(LoginViewModel.Password))
        {
            if (_updatingPassword) return;

            _updatingPassword = true;
            if (PwdBox != null && PwdBox.Password != vm.Password)
                PwdBox.Password = vm.Password;
            if (PwdTextBox != null && PwdTextBox.Text != vm.Password)
                PwdTextBox.Text = vm.Password;
            _updatingPassword = false;
        }
        else if (e.PropertyName == nameof(LoginViewModel.IsPasswordVisible))
        {
            if (vm.IsPasswordVisible)
            {
                if (PwdTextBox != null)
                {
                    PwdTextBox.Focus();
                    PwdTextBox.CaretIndex = PwdTextBox.Text.Length;
                }
            }
            else
            {
                if (PwdBox != null)
                {
                    PwdBox.Focus();
                }
            }
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
            _updatingPassword = true;
            vm.Password = pwdBox.Password;
            if (PwdTextBox != null && PwdTextBox.Text != pwdBox.Password)
            {
                PwdTextBox.Text = pwdBox.Password;
            }
            _updatingPassword = false;
        }
    }

    private void PwdTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingPassword)
        {
            return;
        }

        if (DataContext is LoginViewModel vm && sender is TextBox textBox)
        {
            _updatingPassword = true;
            vm.Password = textBox.Text;
            if (PwdBox != null && PwdBox.Password != textBox.Text)
            {
                PwdBox.Password = textBox.Text;
            }
            _updatingPassword = false;
        }
    }
}