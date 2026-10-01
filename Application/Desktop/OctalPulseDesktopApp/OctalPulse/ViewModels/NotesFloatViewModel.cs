using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public class NoteColorChoice
{
    public string Hex { get; set; } = "#FFFFFF";
    public string Name { get; set; } = "White";
    public string BorderHex { get; set; } = "#E2E8F0";
}

public partial class NotesFloatViewModel : ObservableObject
{
    private readonly INotesStorageService _storageService;
    private readonly IProjectService? _projectService;
    private readonly IDialogService _dialogService;
    private readonly ThemeService _themeService;

    private readonly List<NoteItemViewModel> _allNotes = new();

    // ── Filter & Search ──────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedTypeFilter = "All";
    [ObservableProperty] private string _selectedProjectFilter = "All Projects";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<NoteItemViewModel> FilteredNotes { get; } = new();
    public ObservableCollection<string> AvailableProjects { get; } = new() { "All Projects" };

    public IReadOnlyList<string> FilterTypeOptions { get; } = new[]
    {
        "All", "Task", "Reminder", "Research", "Pending", "Completed"
    };

    // ── Counters ─────────────────────────────────────────────────────────
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _completedCount;

    // ── Create / Edit Composer State ─────────────────────────────────────
    [ObservableProperty] private bool _isComposerOpen;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private Guid? _editingNoteId;

    [ObservableProperty] private string _titleInput = string.Empty;
    [ObservableProperty] private string _descriptionInput = string.Empty;
    [ObservableProperty] private NoteType _typeInput = NoteType.Task;
    [ObservableProperty] private bool _hasDurationDate;
    [ObservableProperty] private DateTime? _durationDateInput = DateTime.Today.AddDays(1);
    [ObservableProperty] private string _selectedCardColor = "#FFFFFF";
    [ObservableProperty] private string _selectedProjectInput = "Personal (No Project)";
    [ObservableProperty] private string _newLinkInput = string.Empty;

    public ObservableCollection<string> LinksInput { get; } = new();
    public ObservableCollection<string> ComposerProjects { get; } = new() { "Personal (No Project)" };

    // 10 Curated Colors (White + 9 distinct modern colors)
    public IReadOnlyList<NoteColorChoice> ColorPalette { get; } = new List<NoteColorChoice>
    {
        new() { Hex = "#FFFFFF", Name = "Pure White", BorderHex = "#E2E8F0" },
        new() { Hex = "#FEF08A", Name = "Sunlight Yellow", BorderHex = "#EAB308" },
        new() { Hex = "#A7F3D0", Name = "Mint Emerald", BorderHex = "#10B981" },
        new() { Hex = "#BAE6FD", Name = "Sky Ice", BorderHex = "#0284C7" },
        new() { Hex = "#DDD6FE", Name = "Soft Lilac", BorderHex = "#8B5CF6" },
        new() { Hex = "#FECDD3", Name = "Coral Rose", BorderHex = "#F43F5E" },
        new() { Hex = "#FED7AA", Name = "Warm Peach", BorderHex = "#F97316" },
        new() { Hex = "#99F6E4", Name = "Bright Aqua", BorderHex = "#14B8A6" },
        new() { Hex = "#E2E8F0", Name = "Slate Silver", BorderHex = "#94A3B8" },
        new() { Hex = "#334155", Name = "Obsidian Navy", BorderHex = "#64748B" }
    };

    public string EmergencyPathDisplay => _storageService.EmergencyStoragePath;

    public NotesFloatViewModel(
        INotesStorageService storageService,
        IDialogService dialogService,
        ThemeService themeService,
        IProjectService? projectService = null)
    {
        _storageService = storageService;
        _dialogService = dialogService;
        _themeService = themeService;
        _projectService = projectService;
    }

    public async Task InitializeAsync()
    {
        await LoadNotesAsync();
        _ = LoadProjectsAsync();
    }

    [RelayCommand]
    public async Task LoadNotesAsync()
    {
        IsBusy = true;
        try
        {
            var notes = await _storageService.LoadNotesAsync();
            _allNotes.Clear();
            foreach (var n in notes.OrderByDescending(x => x.CreatedAt))
            {
                _allNotes.Add(new NoteItemViewModel(n));
            }
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Notes Error", $"Failed to load notes: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadProjectsAsync()
    {
        if (_projectService == null) return;
        try
        {
            var res = await _projectService.GetAllProjectsAsync(1, 50);
            if (res?.Items != null)
            {
                foreach (var p in res.Items)
                {
                    if (!string.IsNullOrWhiteSpace(p.Title))
                    {
                        if (!AvailableProjects.Contains(p.Title))
                            AvailableProjects.Add(p.Title);
                        if (!ComposerProjects.Contains(p.Title))
                            ComposerProjects.Add(p.Title);
                    }
                }
            }
        }
        catch
        {
            // Offline mode or no API connection — safe to ignore
        }
    }

    // ── Search & Filter Logic ────────────────────────────────────────────

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedTypeFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedProjectFilterChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var query = SearchText.Trim().ToLowerInvariant();
        var typeFilter = SelectedTypeFilter;
        var projFilter = SelectedProjectFilter;

        var filtered = _allNotes.AsEnumerable();

        // 1. Search Query
        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(n =>
                n.Title.ToLowerInvariant().Contains(query) ||
                (n.Description?.ToLowerInvariant().Contains(query) == true) ||
                (n.RelatedToProject?.ToLowerInvariant().Contains(query) == true) ||
                n.ListOfLinks.Any(l => l.ToLowerInvariant().Contains(query)));
        }

        // 2. Type / Status Filter
        if (typeFilter == "Task")
            filtered = filtered.Where(n => n.Type == NoteType.Task);
        else if (typeFilter == "Reminder")
            filtered = filtered.Where(n => n.Type == NoteType.Reminder);
        else if (typeFilter == "Research")
            filtered = filtered.Where(n => n.Type == NoteType.Research);
        else if (typeFilter == "Pending")
            filtered = filtered.Where(n => !n.Checked);
        else if (typeFilter == "Completed")
            filtered = filtered.Where(n => n.Checked);

        // 3. Project Filter
        if (projFilter != "All Projects" && !string.IsNullOrEmpty(projFilter))
        {
            filtered = filtered.Where(n => string.Equals(n.RelatedToProject, projFilter, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        if (!FilteredNotes.SequenceEqual(list))
        {
            FilteredNotes.Clear();
            foreach (var item in list)
            {
                FilteredNotes.Add(item);
            }
        }

        TotalCount = _allNotes.Count;
        ActiveCount = _allNotes.Count(n => !n.Checked);
        CompletedCount = _allNotes.Count(n => n.Checked);
    }

    // ── Composer Commands ────────────────────────────────────────────────

    [RelayCommand]
    private void OpenCreateComposer()
    {
        IsEditing = false;
        EditingNoteId = null;
        TitleInput = string.Empty;
        DescriptionInput = string.Empty;
        TypeInput = NoteType.Task;
        HasDurationDate = false;
        DurationDateInput = DateTime.Today.AddDays(1);
        SelectedCardColor = "#FFFFFF";
        SelectedProjectInput = "Personal (No Project)";
        LinksInput.Clear();
        NewLinkInput = string.Empty;

        IsComposerOpen = true;
    }

    [RelayCommand]
    private void OpenEditComposer(NoteItemViewModel note)
    {
        IsEditing = true;
        EditingNoteId = note.Id;
        TitleInput = note.Title;
        DescriptionInput = note.Description ?? string.Empty;
        TypeInput = note.Type;
        HasDurationDate = note.DurationDate.HasValue;
        DurationDateInput = note.DurationDate ?? DateTime.Today.AddDays(1);
        SelectedCardColor = note.CardColor;
        SelectedProjectInput = string.IsNullOrWhiteSpace(note.RelatedToProject) ? "Personal (No Project)" : note.RelatedToProject;
        LinksInput.Clear();
        foreach (var l in note.ListOfLinks)
            LinksInput.Add(l);
        NewLinkInput = string.Empty;

        IsComposerOpen = true;
    }

    [RelayCommand]
    private void CancelComposer()
    {
        IsComposerOpen = false;
    }

    [RelayCommand]
    private void SelectCardColor(string hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
            SelectedCardColor = hex;
    }

    [RelayCommand]
    private void SelectType(NoteType type)
    {
        TypeInput = type;
    }

    [RelayCommand]
    private void AddLink()
    {
        if (string.IsNullOrWhiteSpace(NewLinkInput)) return;
        var link = NewLinkInput.Trim();
        if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            link = "https://" + link;
        }

        if (!LinksInput.Contains(link))
            LinksInput.Add(link);

        NewLinkInput = string.Empty;
    }

    [RelayCommand]
    private void RemoveLink(string link)
    {
        LinksInput.Remove(link);
    }

    [RelayCommand]
    private async Task SaveNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(TitleInput))
        {
            _dialogService.ShowToast("Validation Error", "Note title is required.", ToastType.Warning);
            return;
        }

        try
        {
            var projectTag = SelectedProjectInput == "Personal (No Project)" ? null : SelectedProjectInput;

            if (IsEditing && EditingNoteId.HasValue)
            {
                var existing = _allNotes.FirstOrDefault(n => n.Id == EditingNoteId.Value);
                if (existing != null)
                {
                    existing.Title = TitleInput.Trim();
                    existing.Description = string.IsNullOrWhiteSpace(DescriptionInput) ? null : DescriptionInput.Trim();
                    existing.Type = TypeInput;
                    existing.DurationDate = HasDurationDate ? DurationDateInput : null;
                    existing.CardColor = SelectedCardColor;
                    existing.RelatedToProject = projectTag;
                    existing.ListOfLinks.Clear();
                    foreach (var l in LinksInput)
                        existing.ListOfLinks.Add(l);

                    existing.SyncToModel();
                    await _storageService.AddOrUpdateNoteAsync(existing.Model);
                    _dialogService.ShowToast("Note Updated", $"\"{existing.Title}\" has been saved and emergency backed up.", ToastType.Success);
                }
            }
            else
            {
                var note = new UserNote
                {
                    Id = Guid.NewGuid(),
                    Title = TitleInput.Trim(),
                    Description = string.IsNullOrWhiteSpace(DescriptionInput) ? null : DescriptionInput.Trim(),
                    Type = TypeInput,
                    DurationDate = HasDurationDate ? DurationDateInput : null,
                    CardColor = SelectedCardColor,
                    CreatedAt = DateTime.UtcNow,
                    RelatedToProject = projectTag,
                    Checked = false,
                    ListOfLinks = new List<string>(LinksInput)
                };

                await _storageService.AddOrUpdateNoteAsync(note);
                _allNotes.Insert(0, new NoteItemViewModel(note));
                _dialogService.ShowToast("Note Created", "Note saved offline with emergency mirror backup.", ToastType.Success);
            }

            IsComposerOpen = false;
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Save Error", $"Could not save note: {ex.Message}", ToastType.Error);
        }
    }

    [RelayCommand]
    private void SelectFilter(string filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            SelectedTypeFilter = filter;
        }
    }

    // ── Item Actions ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleCheckedAsync(NoteItemViewModel item)
    {
        if (item == null) return;
        try
        {
            item.Checked = !item.Checked;
            item.SyncToModel();
            await _storageService.AddOrUpdateNoteAsync(item.Model);
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(NoteItemViewModel item)
    {
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Delete Note",
            $"Are you sure you want to permanently delete \"{item.Title}\"?",
            "Delete",
            "Cancel");

        if (!confirm) return;

        try
        {
            await _storageService.DeleteNoteAsync(item.Id);
            _allNotes.Remove(item);
            ApplyFilters();
            _dialogService.ShowToast("Note Deleted", "The note has been removed from all storage files.", ToastType.Info);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Delete Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    private void OpenExternalLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Link Error", $"Could not open link: {ex.Message}", ToastType.Error);
        }
    }

    [RelayCommand]
    private void OpenEmergencyFolder()
    {
        try
        {
            var folder = Path.GetDirectoryName(_storageService.EmergencyStoragePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Folder Error", ex.Message, ToastType.Error);
        }
    }

    [RelayCommand]
    private async Task RestoreFromEmergencyAsync()
    {
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Restore Emergency Notes",
            "Do you want to reload notes from your emergency backup folder in Documents?",
            "Restore",
            "Cancel");

        if (!confirm) return;

        try
        {
            var restored = await _storageService.RestoreFromEmergencyBackupAsync();
            _allNotes.Clear();
            foreach (var n in restored.OrderByDescending(x => x.CreatedAt))
            {
                _allNotes.Add(new NoteItemViewModel(n));
            }
            ApplyFilters();
            _dialogService.ShowToast("Restored", $"Successfully restored {restored.Count} notes from Emergency Backup!", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Restore Error", ex.Message, ToastType.Error);
        }
    }
}
