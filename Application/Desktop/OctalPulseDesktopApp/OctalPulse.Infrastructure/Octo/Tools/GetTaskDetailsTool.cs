using System.Text.Json;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Octo.Tools;

public class GetTaskDetailsTool : IOctoTool
{
    private readonly ITaskService _taskService;
    private readonly IProjectService _projectService;

    public string Name => "get_task_details";
    public string Description => "Retrieves detailed information about a specific task (major or minor), including description, subtasks, criteria, work time, and status.";

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
                    Description = "The UUID of the task to inspect."
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
                    Description = "Optional track UUID if known (helps find major task faster)."
                },
                ["majorTaskId"] = new()
                {
                    Type = "STRING",
                    Description = "Optional major task UUID if inspecting a minor task."
                }
            },
            Required = new List<string> { "taskId" }
        }
    };

    public GetTaskDetailsTool(ITaskService taskService, IProjectService projectService)
    {
        _taskService = taskService;
        _projectService = projectService;
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
                Guid parentMajorTaskId = Guid.Empty;
                if (arguments.TryGetProperty("majorTaskId", out var mProp) && Guid.TryParse(mProp.GetString(), out var parsedMajorId))
                {
                    parentMajorTaskId = parsedMajorId;
                }
                else if (scope.MajorTaskId.HasValue)
                {
                    parentMajorTaskId = scope.MajorTaskId.Value;
                }

                if (parentMajorTaskId != Guid.Empty)
                {
                    var minorResp = await _taskService.GetMinorTasksByMajorTaskAsync(parentMajorTaskId, cancellationToken);
                    var minorTask = minorResp.MinorTasks.FirstOrDefault(m => m.Id == taskId);
                    if (minorTask != null)
                    {
                        return JsonSerializer.Serialize(new
                        {
                            taskType = "minor",
                            id = minorTask.Id,
                            majorTaskId = minorTask.MajorTaskId,
                            title = minorTask.Title,
                            description = minorTask.Description,
                            target = minorTask.Target,
                            state = minorTask.State.ToString(),
                            jobType = minorTask.JobType?.ToString(),
                            notes = minorTask.Notes,
                            link = minorTask.Link,
                            order = minorTask.Order,
                            workTimeSeconds = minorTask.WorkTimeSeconds,
                            createdDate = minorTask.CreatedDate.ToString("yyyy-MM-dd HH:mm")
                        });
                    }
                }

                return JsonSerializer.Serialize(new { error = $"Could not locate minor task {taskId}. Please specify majorTaskId." });
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

                MajorTaskItem? majorTask = null;

                if (trackId != Guid.Empty)
                {
                    var majorTasksResp = await _taskService.GetMajorTasksByTrackAsync(trackId, cancellationToken);
                    majorTask = majorTasksResp.MajorTasks.FirstOrDefault(t => t.Id == taskId);
                }
                else if (scope.ProjectId.HasValue)
                {
                    // Search tracks in project
                    var tracksResp = await _projectService.GetTracksByProjectAsync(scope.ProjectId.Value, cancellationToken);
                    foreach (var tr in tracksResp.Tracks)
                    {
                        var mTasks = await _taskService.GetMajorTasksByTrackAsync(tr.Id, cancellationToken);
                        majorTask = mTasks.MajorTasks.FirstOrDefault(t => t.Id == taskId);
                        if (majorTask != null)
                        {
                            trackId = tr.Id;
                            break;
                        }
                    }
                }

                if (majorTask == null)
                {
                    return JsonSerializer.Serialize(new { error = $"Could not locate major task {taskId}. Please ensure you are authorized and specify trackId." });
                }

                // Also fetch subtasks
                var minorTasksResp = await _taskService.GetMinorTasksByMajorTaskAsync(majorTask.Id, cancellationToken);
                var activeMinors = minorTasksResp.MinorTasks.Where(m => !m.IsDeleted).ToList();

                return JsonSerializer.Serialize(new
                {
                    taskType = "major",
                    id = majorTask.Id,
                    trackId = majorTask.TrackId,
                    title = majorTask.Title,
                    description = majorTask.Description,
                    details = majorTask.Details,
                    link = majorTask.Link,
                    state = majorTask.State.ToString(),
                    priority = majorTask.Priority.ToString(),
                    order = majorTask.Order,
                    progress = majorTask.Progress,
                    dueDate = majorTask.DueDate?.ToString("yyyy-MM-dd"),
                    assignedUserId = majorTask.AssignedUserId,
                    createdDate = majorTask.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                    minorTasksCount = activeMinors.Count,
                    minorTasksCompleted = activeMinors.Count(m => m.State == Domain.Enums.MinorTaskState.Done),
                    minorTasks = activeMinors.Select(m => new
                    {
                        id = m.Id,
                        title = m.Title,
                        state = m.State.ToString(),
                        order = m.Order,
                        jobType = m.JobType?.ToString()
                    }).ToList()
                });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error fetching task details: {ex.Message}" });
        }
    }
}
