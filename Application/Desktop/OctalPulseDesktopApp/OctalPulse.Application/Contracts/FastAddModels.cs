using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Contracts;

public enum FastAddTaskType
{
    Major,
    Minor
}

public enum FastAddExecutionStatus
{
    Pending,
    InProgress,
    Success,
    Failed,
    Skipped
}

public enum FastAddScope
{
    MajorTasks,
    MinorTasks
}

public class FastAddMajorTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Details { get; set; }
    public string? ExternalLink { get; set; }
    public string? State { get; set; }
    public string? Priority { get; set; }
    public string? DueDate { get; set; }
    public List<FastAddMinorTaskDto> MinorTasks { get; set; } = new();

    // Resolved properties after validation
    public MajorTaskState ResolvedState { get; set; } = MajorTaskState.Todo;
    public Priority ResolvedPriority { get; set; } = OctalPulse.Domain.Enums.Priority.Medium;
    public DateTime? ResolvedDueDate { get; set; }
}

public class FastAddMinorTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Target { get; set; }
    public string? JobType { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }
    public string? ExternalLink { get; set; }

    // Resolved properties after validation
    public MinorTaskState ResolvedState { get; set; } = MinorTaskState.Todo;
    public MinorTaskJobType? ResolvedJobType { get; set; }
    public Guid? ParentMajorTaskId { get; set; }
}

public class FastAddValidationResult
{
    public bool IsValid { get; set; }
    public FastAddScope DetectedScope { get; set; } = FastAddScope.MajorTasks;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<FastAddMajorTaskDto> MajorTasks { get; set; } = new();
    public List<FastAddMinorTaskDto> MinorTasks { get; set; } = new();

    public int TotalTasksCount => DetectedScope == FastAddScope.MajorTasks
        ? MajorTasks.Count + MajorTasks.Sum(m => m.MinorTasks.Count)
        : MinorTasks.Count;
}
