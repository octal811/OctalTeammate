using System.Windows.Controls;
using System.Windows.Input;
using OctalPulse.ViewModels;

namespace OctalPulse.Views;

public partial class PapersView : UserControl
{
    public PapersView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PapersViewModel oldVm)
        {
            oldVm.FormattingRequested -= OnFormattingRequested;
        }

        if (e.NewValue is PapersViewModel newVm)
        {
            newVm.FormattingRequested += OnFormattingRequested;
        }
    }

    private void OnFormattingRequested(string prefix, string suffix, string placeholder)
    {
        if (EditorTextBox == null) return;

        EditorTextBox.Focus();
        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;

        if (selectionLength > 0)
        {
            var selectedText = EditorTextBox.SelectedText;
            var wrapped = $"{prefix}{selectedText}{suffix}";
            EditorTextBox.SelectedText = wrapped;
            EditorTextBox.SelectionStart = selectionStart + prefix.Length;
            EditorTextBox.SelectionLength = selectedText.Length;
        }
        else
        {
            var insertText = $"{prefix}{placeholder}{suffix}";
            EditorTextBox.SelectedText = insertText;
            EditorTextBox.SelectionStart = selectionStart + prefix.Length;
            EditorTextBox.SelectionLength = placeholder.Length;
        }
    }

    private void EditorTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Ctrl+S to save
        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (DataContext is PapersViewModel vm)
            {
                vm.SaveCurrentPaperCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
