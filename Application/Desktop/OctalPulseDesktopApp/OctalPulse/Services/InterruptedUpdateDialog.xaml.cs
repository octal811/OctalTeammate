using System.Windows;

namespace OctalPulse.Services;

public partial class InterruptedUpdateDialog : Window
{
    public bool UserChoseReinstall { get; private set; }

    public InterruptedUpdateDialog()
    {
        InitializeComponent();
    }

    private void Reinstall_Click(object sender, RoutedEventArgs e)
    {
        UserChoseReinstall = true;
        DialogResult = true;
        Close();
    }
}
