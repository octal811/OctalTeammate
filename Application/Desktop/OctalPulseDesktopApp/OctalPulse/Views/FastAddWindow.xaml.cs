using System.Windows;
using System.Windows.Input;
using OctalPulse.ViewModels;

namespace OctalPulse.Views;

public partial class FastAddWindow : Window
{
    public bool HasImportedTasks { get; private set; }

    public FastAddWindow(FastAddViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(bool anySuccess)
    {
        HasImportedTasks = anySuccess;
        DialogResult = anySuccess;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is FastAddViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
        }
        base.OnClosed(e);
    }
}
