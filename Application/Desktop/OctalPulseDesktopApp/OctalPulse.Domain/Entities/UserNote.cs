using System;
using System.Collections.Generic;

namespace OctalPulse.Domain.Entities;

public enum NoteType
{
    Reminder,
    Task,
    Research
}

public class UserNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> ListOfLinks { get; set; } = new();
    public NoteType Type { get; set; } = NoteType.Task;
    public DateTime? DurationDate { get; set; }
    public string CardColor { get; set; } = "#FFFFFF";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RelatedToProject { get; set; }
    public bool Checked { get; set; }
}
