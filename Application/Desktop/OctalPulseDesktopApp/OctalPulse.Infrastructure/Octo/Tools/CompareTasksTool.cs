using System.Text.Json;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Octo.Tools;

public class CompareTasksTool : IOctoTool
{
    private readonly ITaskService _taskService;
    private readonly IProjectService _projectService;

    public string Name => "compare_tasks";
    public string Description => "Compares two or more tasks side-by-side across priority, status, order, due dates, progress, and subtask completion.";

    public GeminiFunctionDeclaration Declaration => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new GeminiSchema
        {
            Type = "OBJECT",
            Properties = new Dictionary<string, GeminiSchemaProperty>
            {
                ["taskIds"] = new()
                {
                    Type = "ARRAY",
                    Description = "List of task UUIDs to compare (minimum 2, maximum 5).",
                    Items = new GeminiSchemaProperty { Type = "STRING" }
                },
                ["taskType"] = new()
                {
                    Type = "STRING",
                    Description = "Type of tasks being compared: 'major' or 'minor' (default: 'major').",
                    Enum = new List<string> { "major", "minor" }
                },
                ["trackId"] = new()
                {
                    Type = "STRING",
                    Description = "Optional track ID to locate major tasks."
                }
            },
            Required = new List<string> { "taskIds" }
        }
    };

    public CompareTasksTool(ITaskService taskService, IProjectService projectService)
    {
        _taskService = taskService;
        _projectService = projectService;
    }

    public async Task<string> ExecuteAsync(JsonElement arguments, OctoScope scope, CancellationToken cancellationToken = default)
    {
        var taskIds = new List<Guid>();
        if (arguments.TryGetProperty("taskIds", out var idsProp) && idsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in idsProp.EnumerateArray())
            {
                if (Guid.TryParse(item.GetString(), out var g))
                {
                    taskIds.Add(g);
                }
            }
        }
        else if (scope.SelectedTaskIds.Count >= 2)
        {
            taskIds.AddRange(scope.SelectedTaskIds);
        }

        if (taskIds.Count < 2)
        {
            return JsonSerializer.Serialize(new { error = "Please provide at least two task IDs to compare." });
        }

        var taskType = "major";
        if (arguments.TryGetProperty("taskType", out var typeProp) && !string.IsNullOrWhiteSpace(typeProp.GetString()))
        {
            taskType = typeProp.GetString()!.ToLowerInvariant();
        }

        try
        {
            var comparisonList = new List<object>();

            if (taskType == "minor")
            {
                if (!scope.MajorTaskId.HasValue)
                {
                    return JsonSerializer.Serialize(new { error = "Comparing minor tasks requires a major task in scope." });
                }

                var minorResp = await _taskService.GetMinorTasksByMajorTaskAsync(scope.MajorTaskId.Value, cancellationToken);
                var minors = minorResp.MinorTasks.Where(m => taskIds.Contains(m.Id)).ToList();

                foreach (var m in minors)
                {
                    comparisonList.Add(new
                    {
                        id = m.Id,
                        title = m.Title,
                        state = m.State.ToString(),
                        order = m.Order,
                        jobType = m.JobType?.ToString(),
                        target = m.Target,
                        notes = m.Notes,
                        workTimeSeconds = m.WorkTimeSeconds
                    });
                }
            }
            else
            {
                // Major Tasks
                Guid trackId = Guid.Empty;
                if (arguments.TryGetProperty("trackId", out var tProp) && Guid.TryParse(tProp.GetString(), out var parsedTrackId))
                {
                    trackId = parsedTrackId;
                }
                else if (scope.TrackId.HasValue)
                {
                    trackId = scope.TrackId.Value;
                }

                List<MajorTaskItem> foundTasks = new();

                if (trackId != Guid.Empty)
                {
                    var majorResp = await _taskService.GetMajorTasksByTrackAsync(trackId, cancellationToken);
                    foundTasks = majorResp.MajorTasks.Where(t => taskIds.Contains(t.Id)).ToList();
                }
                else if (scope.ProjectId.HasValue)
                {
                    var tracksResp = await _projectService.GetTracksByProjectAsync(scope.ProjectId.Value, cancellationToken);
                    foreach (var tr in tracksResp.Tracks)
                    {
                        var mTasks = await _taskService.GetMajorTasksByTrackAsync(tr.Id, cancellationToken);
                        foundTasks.AddRange(mTasks.MajorTasks.Where(t => taskIds.Contains(t.Id)));
                    }
                }

                foreach (var t in foundTasks)
                {
                    var subtasksResp = await _taskService.GetMinorTasksByMajorTaskAsync(t.Id, cancellationToken);
                    var subtasks = subtasksResp.MinorTasks.Where(m => !m.IsDeleted).ToList();

                    comparisonList.Add(new
                    {
                        id = t.Id,
                        title = t.Title,
                        description = t.Description,
                        state = t.State.ToString(),
                        priority = t.Priority.ToString(),
                        order = t.Order,
                        progress = t.Progress,
                        dueDate = t.DueDate?.ToString("yyyy-MM-dd"),
                        subtaskTotal = subtasks.Count,
                        subtasksCompleted = subtasks.Count(m => m.State == Domain.Enums.MinorTaskState.Done),
                        subtasksPending = subtasks.Count(m => m.State != Domain.Enums.MinorTaskState.Done)
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                tasksCompared = comparisonList.Count,
                items = comparisonList
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to compare tasks: {ex.Message}" });
        }
    }
}
