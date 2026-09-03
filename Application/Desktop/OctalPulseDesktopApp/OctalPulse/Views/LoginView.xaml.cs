using System.Windows;
using System.Windows.Controls;
using OctalPulse.ViewModels;

namespace OctalPulse.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox pwdBox)
        {
            vm.Password = pwdBox.Password;
        }
    }
}
