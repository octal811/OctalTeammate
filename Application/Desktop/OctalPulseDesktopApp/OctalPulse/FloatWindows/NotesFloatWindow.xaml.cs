using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;
using OctalPulse.ViewModels;

namespace OctalPulse.FloatWindows;

public enum NotesDockPosition
{
    Left,
    Right,
    Top,
    Bottom
}

public partial class NotesFloatWindow : Window
{
    private readonly NotesFloatViewModel _viewModel;
    private readonly ILocalCacheService? _localCache;

    public NotesDockPosition CurrentDockPosition { get; private set; } = NotesDockPosition.Left;

    public NotesFloatWindow(NotesFloatViewModel viewModel, ILocalCacheService? localCache = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _localCache = localCache;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadSavedDockPositionAsync();
        ApplyDockPosition();
        await _viewModel.InitializeAsync();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            ApplyDockPosition();
            Activate();
        }
    }

    private async Task LoadSavedDockPositionAsync()
    {
        if (_localCache == null) return;
        try
        {
            var pref = await _localCache.GetPreferencesAsync();
            if (!string.IsNullOrWhiteSpace(pref.NotesDockPosition) &&
                Enum.TryParse<NotesDockPosition>(pref.NotesDockPosition, true, out var savedPos))
            {
                CurrentDockPosition = savedPos;
            }
            else
            {
                CurrentDockPosition = NotesDockPosition.Left;
            }
        }
        catch
        {
            CurrentDockPosition = NotesDockPosition.Left;
        }
    }

    /// <summary>
    /// Locks the window to the chosen screen edge (Left, Right, Top, or Bottom).
    /// Default position is Left.
    /// </summary>
    public void SetDockPosition(NotesDockPosition position, bool persist = true)
    {
        CurrentDockPosition = position;
        ApplyDockPosition();

        if (persist && _localCache != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var pref = await _localCache.GetPreferencesAsync();
                    pref.NotesDockPosition = position.ToString();
                    await _localCache.SavePreferencesAsync(pref);
                }
                catch { }
            });
        }
    }

    public void CycleDockPosition()
    {
        var next = CurrentDockPosition switch
        {
            NotesDockPosition.Left   => NotesDockPosition.Right,
            NotesDockPosition.Right  => NotesDockPosition.Top,
            NotesDockPosition.Top    => NotesDockPosition.Bottom,
            NotesDockPosition.Bottom => NotesDockPosition.Left,
            _                        => NotesDockPosition.Left
        };
        SetDockPosition(next);
    }

    private void ApplyDockPosition()
    {
        var workArea = SystemParameters.WorkArea;

        switch (CurrentDockPosition)
        {
            case NotesDockPosition.Left:
                Width = 460;
                Height = workArea.Height;
                Top = workArea.Top;
                Left = workArea.Left;
                RootBorder.BorderThickness = new Thickness(0, 0, 1.5, 0);
                RootShadow.Direction = 0;
                DockIcon.Text = "⬅️";
                DockButton.ToolTip = "Locked to Left Side (Default) • Click for menu, Alt + D to cycle";
                break;

            case NotesDockPosition.Right:
                Width = 460;
                Height = workArea.Height;
                Top = workArea.Top;
                Left = Math.Max(0, workArea.Right - Width);
                RootBorder.BorderThickness = new Thickness(1.5, 0, 0, 0);
                RootShadow.Direction = 180;
                DockIcon.Text = "➡️";
                DockButton.ToolTip = "Locked to Right Side • Click for menu, Alt + D to cycle";
                break;

            case NotesDockPosition.Top:
                Width = workArea.Width;
                Height = Math.Min(500, Math.Max(380, workArea.Height * 0.52));
                Top = workArea.Top;
                Left = workArea.Left;
                RootBorder.BorderThickness = new Thickness(0, 0, 0, 1.5);
                RootShadow.Direction = 270;
                DockIcon.Text = "⬆️";
                DockButton.ToolTip = "Locked to Top Side • Click for menu, Alt + D to cycle";
                break;

            case NotesDockPosition.Bottom:
                Width = workArea.Width;
                Height = Math.Min(500, Math.Max(380, workArea.Height * 0.52));
                Top = Math.Max(0, workArea.Bottom - Height);
                Left = workArea.Left;
                RootBorder.BorderThickness = new Thickness(0, 1.5, 0, 0);
                RootShadow.Direction = 90;
                DockIcon.Text = "⬇️";
                DockButton.ToolTip = "Locked to Bottom Side • Click for menu, Alt + D to cycle";
                break;
        }

        // Update ContextMenu checkmarks
        MenuDockLeft.IsChecked = CurrentDockPosition == NotesDockPosition.Left;
        MenuDockRight.IsChecked = CurrentDockPosition == NotesDockPosition.Right;
        MenuDockTop.IsChecked = CurrentDockPosition == NotesDockPosition.Top;
        MenuDockBottom.IsChecked = CurrentDockPosition == NotesDockPosition.Bottom;
    }

    private void DockButton_Click(object sender, RoutedEventArgs e)
    {
        if (DockButton.ContextMenu != null)
        {
            DockButton.ContextMenu.PlacementTarget = DockButton;
            DockButton.ContextMenu.IsOpen = true;
        }
    }

    private void DockLeft_Click(object sender, RoutedEventArgs e) => SetDockPosition(NotesDockPosition.Left);
    private void DockRight_Click(object sender, RoutedEventArgs e) => SetDockPosition(NotesDockPosition.Right);
    private void DockTop_Click(object sender, RoutedEventArgs e) => SetDockPosition(NotesDockPosition.Top);
    private void DockBottom_Click(object sender, RoutedEventArgs e) => SetDockPosition(NotesDockPosition.Bottom);

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Alt+D cycles dock position (Left -> Right -> Top -> Bottom)
        var isAltD = (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && (e.SystemKey == Key.D || e.Key == Key.D));
        if (isAltD)
        {
            e.Handled = true;
            CycleDockPosition();
            return;
        }

        // Alt+I or Ctrl+I opens Fast Add
        var isAltI = (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && (e.SystemKey == Key.I || e.Key == Key.I));
        var isCtrlI = (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.I);

        if (isAltI || isCtrlI)
        {
            e.Handled = true;
            _viewModel.OpenFastAddCommand.Execute(null);
            return;
        }

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
