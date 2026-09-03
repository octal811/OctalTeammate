using System.Windows;
using System.Windows.Controls;
using OctalPulse.ViewModels;

namespace OctalPulse.Views;

public partial class ForgotPasswordView : UserControl
{
    public ForgotPasswordView()
    {
        InitializeComponent();
    }

    private void NewPwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ForgotPasswordViewModel vm && sender is PasswordBox pb)
        {
            vm.NewPassword = pb.Password;
        }
    }

    private void ConfirmNewPwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ForgotPasswordViewModel vm && sender is PasswordBox pb)
        {
            vm.ConfirmPassword = pb.Password;
        }
    }
}
