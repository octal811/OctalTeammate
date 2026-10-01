using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OctalPulse.Domain.Entities;

namespace OctalPulse.ViewModels;

public partial class NoteItemViewModel : ObservableObject
{
    public UserNote Model { get; }

    public Guid Id => Model.Id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private NoteType _type = NoteType.Task;

    [ObservableProperty]
    private DateTime? _durationDate;

    [ObservableProperty]
    private string _cardColor = "#FFFFFF";

    [ObservableProperty]
    private string? _relatedToProject;

    [ObservableProperty]
    private bool _checked;

    public ObservableCollection<string> ListOfLinks { get; } = new();

    public DateTime CreatedAt => Model.CreatedAt;

    public string FormattedCreatedAt => CreatedAt.ToLocalTime().ToString("MMM d, yyyy • h:mm tt");
    public string? FormattedDuration => DurationDate.HasValue ? $"Due {DurationDate.Value.ToLocalTime():MMM d, yyyy}" : null;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasLinks => ListOfLinks.Count > 0;
    public bool HasProject => !string.IsNullOrWhiteSpace(RelatedToProject);
    public bool HasDuration => DurationDate.HasValue;

    public string TypeIcon => Type switch
    {
        NoteType.Reminder => "⏰",
        NoteType.Task => "📋",
        NoteType.Research => "🔬",
        _ => "📝"
    };

    public NoteItemViewModel(UserNote model)
    {
        Model = model;
        _title = model.Title;
        _description = model.Description;
        _type = model.Type;
        _durationDate = model.DurationDate;
        _cardColor = string.IsNullOrWhiteSpace(model.CardColor) ? "#FFFFFF" : model.CardColor;
        _relatedToProject = model.RelatedToProject;
        _checked = model.Checked;

        if (model.ListOfLinks != null)
        {
            foreach (var link in model.ListOfLinks)
            {
                if (!string.IsNullOrWhiteSpace(link))
                    ListOfLinks.Add(link.Trim());
            }
        }
    }

    public void SyncToModel()
    {
        Model.Title = Title;
        Model.Description = Description;
        Model.Type = Type;
        Model.DurationDate = DurationDate;
        Model.CardColor = CardColor;
        Model.RelatedToProject = RelatedToProject;
        Model.Checked = Checked;
        Model.ListOfLinks = new List<string>(ListOfLinks);
    }
}
