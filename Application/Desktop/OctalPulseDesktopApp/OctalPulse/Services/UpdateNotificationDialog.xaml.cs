using System.Windows;
using OctalPulse.Application.Contracts;

namespace OctalPulse.Services;

public partial class UpdateNotificationDialog : Window
{
    public bool UserChoseUpdate { get; private set; }

    public UpdateNotificationDialog(UpdateCheckResult checkResult)
    {
        InitializeComponent();

        CurrentVersionText.Text = $"v{checkResult.CurrentVersion.DisplayString}";
        NewVersionText.Text = checkResult.NewVersion != null ? $"v{checkResult.NewVersion.DisplayString}" : "vUnknown";

        if (!string.IsNullOrWhiteSpace(checkResult.ReleaseNotes))
        {
            ReleaseNotesText.Text = checkResult.ReleaseNotes;
            ReleaseNotesPanel.Visibility = Visibility.Visible;
        }
    }

    private void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        UserChoseUpdate = true;
        DialogResult = true;
        Close();
    }
}
