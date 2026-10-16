using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OctalPulse.Application.Abstractions;
using OctalPulse.Models;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class PapersViewModel : ObservableObject, INavigationAware
{
    private readonly IPapersStorageService _storageService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _currentDirectory = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private PaperItem? _selectedPaper;

    [ObservableProperty]
    private string _currentContent = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadMode))]
    private bool _isEditMode = true;

    public bool IsReadMode => !IsEditMode;

    [ObservableProperty]
    private FlowDocument? _readDocument;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ObservableCollection<PaperItem> Papers { get; } = new();
    public ObservableCollection<PaperItem> FilteredPapers { get; } = new();

    public event Action<string, string, string>? FormattingRequested;

    public PapersViewModel(
        IPapersStorageService storageService,
        IDialogService dialogService)
    {
        _storageService = storageService;
        _dialogService = dialogService;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            CurrentDirectory = _storageService.GetPapersDirectory();
            await ReloadPapersAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Papers Error", $"Failed to initialize: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnCurrentContentChanged(string value)
    {
        HasUnsavedChanges = true;
        StatusMessage = "Unsaved changes";

        if (IsReadMode)
        {
            ReadDocument = MarkdownToFlowDocumentHelper.ConvertToFlowDocument(value);
        }
    }

    partial void OnIsEditModeChanged(bool value)
    {
        if (!value)
        {
            // Switched to Read Mode: compile Markdown into FlowDocument
            ReadDocument = MarkdownToFlowDocumentHelper.ConvertToFlowDocument(CurrentContent);
        }
    }

    private void ApplyFilter()
    {
        FilteredPapers.Clear();
        var query = SearchText?.Trim() ?? string.Empty;

        var items = string.IsNullOrEmpty(query)
            ? Papers
            : Papers.Where(p => p.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                p.Snippet.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var item in items)
        {
            FilteredPapers.Add(item);
        }
    }

    [RelayCommand]
    public async Task ReloadPapersAsync()
    {
        try
        {
            var list = await _storageService.LoadPapersAsync();
            Papers.Clear();
            foreach (var item in list)
            {
                Papers.Add(item);
            }
            ApplyFilter();

            if (SelectedPaper != null)
            {
                var existing = Papers.FirstOrDefault(p => p.FilePath.Equals(SelectedPaper.FilePath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    await SelectPaperAsync(existing);
                    return;
                }
            }

            if (FilteredPapers.Count > 0)
            {
                await SelectPaperAsync(FilteredPapers[0]);
            }
            else
            {
                SelectedPaper = null;
                CurrentContent = string.Empty;
                ReadDocument = null;
                HasUnsavedChanges = false;
                StatusMessage = "No papers found";
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Papers Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task SelectPaperAsync(PaperItem? paper)
    {
        if (paper == null) return;

        if (HasUnsavedChanges && SelectedPaper != null)
        {
            // Auto-save previous paper before switching
            await SaveCurrentPaperAsync();
        }

        foreach (var p in Papers)
        {
            p.IsSelected = p.FilePath == paper.FilePath;
        }

        SelectedPaper = paper;
        IsLoading = true;
        try
        {
            CurrentContent = await _storageService.ReadPaperContentAsync(paper.FilePath);
            HasUnsavedChanges = false;
            StatusMessage = "All changes saved";

            if (IsReadMode)
            {
                ReadDocument = MarkdownToFlowDocumentHelper.ConvertToFlowDocument(CurrentContent);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Read Error", $"Could not load paper: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task NewPaperAsync()
    {
        var title = await _dialogService.ShowPromptAsync("New Paper", "Enter a title for your new Markdown paper:", "Research Note");
        if (string.IsNullOrWhiteSpace(title)) return;

        try
        {
            var created = await _storageService.CreatePaperAsync(title);
            Papers.Insert(0, created);
            ApplyFilter();
            await SelectPaperAsync(created);
            IsEditMode = true;
            _dialogService.ShowToast("Paper Created", $"Created '{created.Title}.md'", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Creation Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task SaveCurrentPaperAsync()
    {
        if (SelectedPaper == null) return;

        try
        {
            StatusMessage = "Saving...";
            await _storageService.SavePaperContentAsync(SelectedPaper.FilePath, CurrentContent);
            SelectedPaper.LastModified = DateTime.Now;

            var lines = CurrentContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var firstMeaningful = lines.FirstOrDefault(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l));
            SelectedPaper.Snippet = firstMeaningful ?? (lines.FirstOrDefault() ?? string.Empty);
            if (SelectedPaper.Snippet.Length > 90) SelectedPaper.Snippet = SelectedPaper.Snippet[..87] + "...";

            HasUnsavedChanges = false;
            StatusMessage = "All changes saved";
            _dialogService.ShowToast("Saved", $"Saved '{SelectedPaper.Title}.md'", ToastType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = "Save failed";
            _dialogService.ShowToast("Save Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task RenamePaperAsync(PaperItem? paper)
    {
        var target = paper ?? SelectedPaper;
        if (target == null) return;

        var newTitle = await _dialogService.ShowPromptAsync("Rename Paper", "Enter new name:", target.Title);
        if (string.IsNullOrWhiteSpace(newTitle) || newTitle.Equals(target.Title, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            var renamed = await _storageService.RenamePaperAsync(target.FilePath, newTitle);
            target.FilePath = renamed.FilePath;
            target.FileName = renamed.FileName;
            target.Title = renamed.Title;
            target.LastModified = renamed.LastModified;

            _dialogService.ShowToast("Renamed", $"Paper renamed to '{renamed.Title}.md'", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Rename Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task DeletePaperAsync(PaperItem? paper)
    {
        var target = paper ?? SelectedPaper;
        if (target == null) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Delete Paper",
            $"Are you sure you want to permanently delete '{target.Title}.md'?",
            "Delete",
            "Cancel");

        if (!confirm) return;

        try
        {
            await _storageService.DeletePaperAsync(target.FilePath);
            Papers.Remove(target);
            ApplyFilter();

            if (SelectedPaper?.FilePath == target.FilePath)
            {
                if (FilteredPapers.Count > 0)
                {
                    await SelectPaperAsync(FilteredPapers[0]);
                }
                else
                {
                    SelectedPaper = null;
                    CurrentContent = string.Empty;
                    ReadDocument = null;
                    HasUnsavedChanges = false;
                    StatusMessage = "No papers";
                }
            }

            _dialogService.ShowToast("Paper Deleted", $"Deleted '{target.Title}.md'", ToastType.Info);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Delete Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    public void OpenFolderInExplorer()
    {
        _storageService.OpenFolderInExplorer();
    }

    [RelayCommand]
    public async Task ChangeFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Local Papers Storage Folder",
            InitialDirectory = CurrentDirectory
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            try
            {
                _storageService.SetPapersDirectory(dialog.FolderName);
                CurrentDirectory = _storageService.GetPapersDirectory();
                await ReloadPapersAsync();
                _dialogService.ShowToast("Papers Location Updated", $"Storage folder changed to: {CurrentDirectory}", ToastType.Success);
            }
            catch (Exception ex)
            {
                _dialogService.ShowToast("Folder Error", $"Failed to set folder: {ex.Message}. Reverted to default.", ToastType.Error);
            }
        }
    }

    [RelayCommand]
    public void SetEditMode()
    {
        IsEditMode = true;
    }

    [RelayCommand]
    public void SetReadMode()
    {
        IsEditMode = false;
    }

    // ── Markdown Formatting Helpers ─────────────────────────────────────

    [RelayCommand]
    public void InsertHeading(string? levelStr)
    {
        var level = int.TryParse(levelStr, out var l) ? l : 1;
        var prefix = new string('#', level) + " ";
        ApplyFormat(prefix, string.Empty, "Heading");
    }

    [RelayCommand]
    public void InsertBold() => ApplyFormat("**", "**", "bold text");

    [RelayCommand]
    public void InsertItalic() => ApplyFormat("*", "*", "italic text");

    [RelayCommand]
    public void InsertStrikethrough() => ApplyFormat("~~", "~~", "strikethrough");

    [RelayCommand]
    public void InsertCodeBlock() => ApplyFormat("```csharp\n", "\n```\n", "// Code block");

    [RelayCommand]
    public void InsertInlineCode() => ApplyFormat("`", "`", "code");

    [RelayCommand]
    public void InsertQuote() => ApplyFormat("> ", string.Empty, "Quote text");

    [RelayCommand]
    public void InsertBulletList() => ApplyFormat("- ", string.Empty, "List item");

    [RelayCommand]
    public void InsertNumberedList() => ApplyFormat("1. ", string.Empty, "First item");

    [RelayCommand]
    public void InsertChecklist() => ApplyFormat("- [ ] ", string.Empty, "Todo task");

    [RelayCommand]
    public void InsertLink() => ApplyFormat("[", "](https://example.com)", "Link text");

    [RelayCommand]
    public void InsertTable()
    {
        var tableTemplate = "\n| Column 1 | Column 2 | Column 3 |\n| :--- | :---: | ---: |\n| Value 1 | Value 2 | Value 3 |\n| Data A | Data B | Data C |\n\n";
        ApplyFormat(tableTemplate, string.Empty, string.Empty);
    }

    [RelayCommand]
    public void InsertHorizontalRule() => ApplyFormat("\n---\n\n", string.Empty, string.Empty);

    [RelayCommand]
    public void InsertImageFromPc()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose Image from PC",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
            var path = dialog.FileName.Replace("\\", "/");
            var imgTag = $"\n![{fileName}](file:///{path})\n";
            ApplyFormat(imgTag, string.Empty, string.Empty);
            _dialogService.ShowToast("Image Inserted", $"Photo path added for '{Path.GetFileName(dialog.FileName)}'", ToastType.Success);
        }
    }

    private void ApplyFormat(string prefix, string suffix, string placeholder)
    {
        // Fire event to View for selection-aware insertion
        FormattingRequested?.Invoke(prefix, suffix, placeholder);

        if (!IsEditMode)
        {
            // If in Read Mode, switch to Edit Mode so user can see what was added or edit it
            IsEditMode = true;
        }
    }
}
