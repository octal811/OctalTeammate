using System.Windows;

namespace OctalPulse.Services;

public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string defaultText = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultText;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string? ResultText => string.IsNullOrWhiteSpace(InputBox.Text) ? null : InputBox.Text.Trim();

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}