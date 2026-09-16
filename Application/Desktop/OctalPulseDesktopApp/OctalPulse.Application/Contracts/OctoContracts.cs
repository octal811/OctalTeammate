namespace OctalPulse.Application.Contracts;

public enum OctoScopeLevel
{
    AllAccessible,
    Project,
    Track,
    MajorTask,
    SelectedTasks
}

public class OctoScope
{
    public Guid? ProjectId { get; set; }
    public string? ProjectTitle { get; set; }

    public Guid? TrackId { get; set; }
    public string? TrackTitle { get; set; }

    public Guid? MajorTaskId { get; set; }
    public string? MajorTaskTitle { get; set; }

    public List<Guid> SelectedTaskIds { get; set; } = new();
    public List<string> SelectedTaskTitles { get; set; } = new();

    public OctoScopeLevel Level
    {
        get
        {
            if (SelectedTaskIds.Count > 0) return OctoScopeLevel.SelectedTasks;
            if (MajorTaskId.HasValue) return OctoScopeLevel.MajorTask;
            if (TrackId.HasValue) return OctoScopeLevel.Track;
            if (ProjectId.HasValue) return OctoScopeLevel.Project;
            return OctoScopeLevel.AllAccessible;
        }
    }

    public string DisplayText
    {
        get
        {
            if (SelectedTaskIds.Count > 0)
            {
                return $"Selected Tasks ({SelectedTaskIds.Count})";
            }

            if (MajorTaskId.HasValue)
            {
                var track = !string.IsNullOrEmpty(TrackTitle) ? $"{TrackTitle} › " : "";
                var proj = !string.IsNullOrEmpty(ProjectTitle) ? $"{ProjectTitle} › " : "";
                return $"{proj}{track}{MajorTaskTitle}";
            }

            if (TrackId.HasValue)
            {
                var proj = !string.IsNullOrEmpty(ProjectTitle) ? $"{ProjectTitle} › " : "";
                return $"{proj}{TrackTitle}";
            }

            if (ProjectId.HasValue)
            {
                return ProjectTitle ?? "Project";
            }

            return "All Accessible Workspaces";
        }
    }

    public OctoScope Clone()
    {
        return new OctoScope
        {
            ProjectId = ProjectId,
            ProjectTitle = ProjectTitle,
            TrackId = TrackId,
            TrackTitle = TrackTitle,
            MajorTaskId = MajorTaskId,
            MajorTaskTitle = MajorTaskTitle,
            SelectedTaskIds = new List<Guid>(SelectedTaskIds),
            SelectedTaskTitles = new List<string>(SelectedTaskTitles)
        };
    }
}

public class OctoMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Role { get; set; } = "user"; // "user", "assistant", "system", "tool"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? StatusText { get; set; }
    public string? ToolCallsSummary { get; set; }
    public bool IsError { get; set; }

    public static OctoMessage ForUser(string content) => new()
    {
        Role = "user",
        Content = content,
        Timestamp = DateTime.UtcNow
    };

    public static OctoMessage ForAssistant(string content, string? toolSummary = null) => new()
    {
        Role = "assistant",
        Content = content,
        ToolCallsSummary = toolSummary,
        Timestamp = DateTime.UtcNow
    };

    public static OctoMessage ForError(string errorMessage) => new()
    {
        Role = "assistant",
        Content = errorMessage,
        IsError = true,
        Timestamp = DateTime.UtcNow
    };
}

public class OctoAgentResult
{
    public bool Success { get; set; }
    public string ResponseText { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int ExecutedToolCount { get; set; }
}
