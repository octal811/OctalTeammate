using System.Text.Json;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Infrastructure.Octo.Tools;

public class AnalyzeTaskDependenciesTool : IOctoTool
{
    private readonly ITaskService _taskService;

    public string Name => "analyze_task_dependencies";
    public string Description => "Analyzes execution order, prerequisites, blockers, and dependent successor tasks based on sequential track hierarchy and status progression.";

    public GeminiFunctionDeclaration Declaration => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new GeminiSchema
        {
            Type = "OBJECT",
            Properties = new Dictionary<string, GeminiSchemaProperty>
            {
                ["taskId"] = new()
                {
                    Type = "STRING",
                    Description = "The UUID of the task to analyze."
                },
                ["taskType"] = new()
                {
                    Type = "STRING",
                    Description = "Type of task: 'major' or 'minor' (default: 'major').",
                    Enum = new List<string> { "major", "minor" }
                },
                ["trackId"] = new()
                {
                    Type = "STRING",
                    Description = "The track UUID (required for major tasks if not in scope)."
                },
                ["majorTaskId"] = new()
                {
                    Type = "STRING",
                    Description = "The major task UUID (required for minor tasks if not in scope)."
                }
            },
            Required = new List<string> { "taskId" }
        }
    };

    public AnalyzeTaskDependenciesTool(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public async Task<string> ExecuteAsync(JsonElement arguments, OctoScope scope, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("taskId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var taskId))
        {
            return JsonSerializer.Serialize(new { error = "Invalid or missing 'taskId' parameter." });
        }

        var taskType = "major";
        if (arguments.TryGetProperty("taskType", out var typeProp) && !string.IsNullOrWhiteSpace(typeProp.GetString()))
        {
            taskType = typeProp.GetString()!.ToLowerInvariant();
        }

        try
        {
            if (taskType == "minor")
            {
                Guid majorTaskId = Guid.Empty;
                if (arguments.TryGetProperty("majorTaskId", out var mProp) && Guid.TryParse(mProp.GetString(), out var parsedMajorId))
                {
                    majorTaskId = parsedMajorId;
                }
                else if (scope.MajorTaskId.HasValue)
                {
                    majorTaskId = scope.MajorTaskId.Value;
                }

                if (majorTaskId == Guid.Empty)
                {
                    return JsonSerializer.Serialize(new { error = "Cannot analyze minor task without a majorTaskId in scope or arguments." });
                }

                var minorResp = await _taskService.GetMinorTasksByMajorTaskAsync(majorTaskId, cancellationToken);
                var allMinors = minorResp.MinorTasks.Where(m => !m.IsDeleted).OrderBy(m => m.Order).ToList();
                var target = allMinors.FirstOrDefault(m => m.Id == taskId);

                if (target == null)
                {
                    return JsonSerializer.Serialize(new { error = $"Minor task {taskId} not found in major task {majorTaskId}." });
                }

                var preceding = allMinors.Where(m => m.Order < target.Order).ToList();
                var incompletePreceding = preceding.Where(m => m.State != MinorTaskState.Done).ToList();
                var successors = allMinors.Where(m => m.Order > target.Order).ToList();

                var isActionable = target.State is MinorTaskState.Todo or MinorTaskState.InProgress && incompletePreceding.Count == 0;

                return JsonSerializer.Serialize(new
                {
                    taskType = "minor",
                    id = target.Id,
                    title = target.Title,
                    order = target.Order,
                    state = target.State.ToString(),
                    isActionable = isActionable,
                    precedingTasksCount = preceding.Count,
                    incompletePrecedingBlockers = incompletePreceding.Select(p => new
                    {
                        id = p.Id,
                        title = p.Title,
                        order = p.Order,
                        state = p.State.ToString()
                    }).ToList(),
                    subsequentDependentTasksCount = successors.Count,
                    subsequentTasks = successors.Take(3).Select(s => new
                    {
                        id = s.Id,
                        title = s.Title,
                        order = s.Order,
                        state = s.State.ToString()
                    }).ToList(),
                    summary = isActionable
                        ? "Task has no blocking predecessors and is ready to work on."
                        : incompletePreceding.Count > 0
                            ? $"Task is preceded by {incompletePreceding.Count} incomplete task(s) in this checklist."
                            : $"Task is currently {target.State}."
                });
            }
            else
            {
                // Major Task
                Guid trackId = Guid.Empty;
                if (arguments.TryGetProperty("trackId", out var tProp) && Guid.TryParse(tProp.GetString(), out var parsedTrackId))
                {
                    trackId = parsedTrackId;
                }
                else if (scope.TrackId.HasValue)
                {
                    trackId = scope.TrackId.Value;
                }

                if (trackId == Guid.Empty)
                {
                    return JsonSerializer.Serialize(new { error = "Cannot analyze major task without a trackId in scope or arguments." });
                }

                var majorResp = await _taskService.GetMajorTasksByTrackAsync(trackId, cancellationToken);
                var allMajor = majorResp.MajorTasks.OrderBy(t => t.Order).ToList();
                var target = allMajor.FirstOrDefault(t => t.Id == taskId);

                if (target == null)
                {
                    return JsonSerializer.Serialize(new { error = $"Major task {taskId} not found in track {trackId}." });
                }

                var preceding = allMajor.Where(t => t.Order < target.Order).ToList();
                var incompletePreceding = preceding.Where(t => t.State != MajorTaskState.Done).ToList();
                var successors = allMajor.Where(t => t.Order > target.Order).ToList();

                // Child minor tasks
                var minorResp = await _taskService.GetMinorTasksByMajorTaskAsync(target.Id, cancellationToken);
                var childMinors = minorResp.MinorTasks.Where(m => !m.IsDeleted).ToList();
                var childIncomplete = childMinors.Where(m => m.State != MinorTaskState.Done).ToList();

                var isActionable = target.State is MajorTaskState.Todo or MajorTaskState.InProgress && incompletePreceding.Count == 0;

                return JsonSerializer.Serialize(new
                {
                    taskType = "major",
                    id = target.Id,
                    title = target.Title,
                    order = target.Order,
                    priority = target.Priority.ToString(),
                    state = target.State.ToString(),
                    progress = target.Progress,
                    isActionable = isActionable,
                    precedingTasksCount = preceding.Count,
                    incompletePrecedingBlockers = incompletePreceding.Select(p => new
                    {
                        id = p.Id,
                        title = p.Title,
                        order = p.Order,
                        state = p.State.ToString(),
                        priority = p.Priority.ToString()
                    }).ToList(),
                    subsequentDependentTasksCount = successors.Count,
                    subsequentTasks = successors.Take(3).Select(s => new
                    {
                        id = s.Id,
                        title = s.Title,
                        order = s.Order,
                        state = s.State.ToString(),
                        priority = s.Priority.ToString()
                    }).ToList(),
                    childMinorTasksTotal = childMinors.Count,
                    childMinorTasksPending = childIncomplete.Count,
                    summary = isActionable
                        ? "Task is actionable: no preceding major tasks in the track are blocking it."
                        : incompletePreceding.Count > 0
                            ? $"Task is preceded by {incompletePreceding.Count} unfinished major task(s) with lower order in the track sequence."
                            : $"Task is currently {target.State}."
                });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Dependency analysis failed: {ex.Message}" });
        }
    }
}
