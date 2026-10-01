using System;
using System.Windows;
using System.Windows.Input;
using OctalPulse.ViewModels;

namespace OctalPulse.FloatWindows;

public partial class NotesFloatWindow : Window
{
    private readonly NotesFloatViewModel _viewModel;

    public NotesFloatWindow(NotesFloatViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionOnRightSide();
        await _viewModel.InitializeAsync();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            PositionOnRightSide();
            Activate();
        }
    }

    /// <summary>
    /// Locks the window permanently to the entire right edge of the primary working area.
    /// </summary>
    public void PositionOnRightSide()
    {
        var workArea = SystemParameters.WorkArea;
        Width = 460;
        Height = workArea.Height;
        Top = workArea.Top;
        Left = Math.Max(0, workArea.Right - Width);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // ESC key hides the drawer if composer is not open
        if (e.Key == Key.Escape)
        {
            if (_viewModel.IsComposerOpen)
            {
                _viewModel.CancelComposerCommand.Execute(null);
            }
            else
            {
                Hide();
            }
        }
    }
}
