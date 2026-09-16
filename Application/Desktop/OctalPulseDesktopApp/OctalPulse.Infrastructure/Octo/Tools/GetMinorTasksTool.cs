using System.Text.Json;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Infrastructure.Octo.Tools;

public class GetMinorTasksTool : IOctoTool
{
    private readonly ITaskService _taskService;

    public string Name => "get_minor_tasks";
    public string Description => "Retrieves minor tasks (subtasks / checklist items) under a major task, paginated at max 10 tasks per page. Returns order, state, jobType, target criteria, notes, and workTime. If majorTaskId is omitted, the currently scoped major task is used.";

    public GeminiFunctionDeclaration Declaration => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new GeminiSchema
        {
            Type = "OBJECT",
            Properties = new Dictionary<string, GeminiSchemaProperty>
            {
                ["majorTaskId"] = new()
                {
                    Type = "STRING",
                    Description = "The UUID of the parent major task. Optional if already scoped to a major task."
                },
                ["pageNumber"] = new()
                {
                    Type = "INTEGER",
                    Description = "Page number to retrieve (1-based, default: 1)."
                },
                ["pageSize"] = new()
                {
                    Type = "INTEGER",
                    Description = "Items per page (max: 10, default: 10)."
                },
                ["statusFilter"] = new()
                {
                    Type = "STRING",
                    Description = "Optional status filter (e.g. 'Todo', 'InProgress', 'Done', 'Canceled', 'Failed')."
                }
            }
        }
    };

    public GetMinorTasksTool(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public async Task<string> ExecuteAsync(JsonElement arguments, OctoScope scope, CancellationToken cancellationToken = default)
    {
        Guid targetMajorTaskId = Guid.Empty;

        if (arguments.TryGetProperty("majorTaskId", out var majorProp) && Guid.TryParse(majorProp.GetString(), out var parsedId))
        {
            targetMajorTaskId = parsedId;
        }
        else if (scope.MajorTaskId.HasValue)
        {
            targetMajorTaskId = scope.MajorTaskId.Value;
        }

        if (targetMajorTaskId == Guid.Empty)
        {
            return JsonSerializer.Serialize(new { error = "No majorTaskId provided or found in the current scope. Please specify a majorTaskId." });
        }

        var pageNumber = 1;
        var pageSize = 10;

        if (arguments.TryGetProperty("pageNumber", out var pageProp) && pageProp.TryGetInt32(out var p))
            pageNumber = Math.Max(1, p);

        if (arguments.TryGetProperty("pageSize", out var sizeProp) && sizeProp.TryGetInt32(out var s))
            pageSize = Math.Clamp(s, 1, 10);

        string? statusFilter = null;
        if (arguments.TryGetProperty("statusFilter", out var statusProp) && !string.IsNullOrWhiteSpace(statusProp.GetString()))
        {
            statusFilter = statusProp.GetString()!.Trim();
        }

        try
        {
            var result = await _taskService.GetMinorTasksByMajorTaskAsync(targetMajorTaskId, cancellationToken);
            var query = result.MinorTasks.Where(m => !m.IsDeleted).AsEnumerable();

            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<MinorTaskState>(statusFilter, true, out var parsedState))
            {
                query = query.Where(m => m.State == parsedState);
            }

            var totalMatching = query.Count();
            var paged = query
                .OrderBy(m => m.Order)
                .ThenBy(m => m.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    id = m.Id,
                    majorTaskId = m.MajorTaskId,
                    title = m.Title,
                    description = m.Description,
                    target = m.Target,
                    state = m.State.ToString(),
                    jobType = m.JobType?.ToString(),
                    notes = m.Notes,
                    order = m.Order,
                    workTimeSeconds = m.WorkTimeSeconds,
                    isDone = m.State == MinorTaskState.Done,
                    isActionable = m.State is MinorTaskState.Todo or MinorTaskState.InProgress
                })
                .ToList();

            return JsonSerializer.Serialize(new
            {
                majorTaskId = targetMajorTaskId,
                minorTasks = paged,
                totalCount = totalMatching,
                pageNumber = pageNumber,
                pageSize = pageSize,
                hasMorePages = (pageNumber * pageSize) < totalMatching
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to retrieve minor tasks for major task {targetMajorTaskId}: {ex.Message}" });
        }
    }
}
