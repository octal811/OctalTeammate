using System.Text.Json;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Infrastructure.Octo.Tools;

public class GetMajorTasksTool : IOctoTool
{
    private readonly ITaskService _taskService;

    public string Name => "get_major_tasks";
    public string Description => "Retrieves major tasks belonging to a track, paginated at max 10 tasks per page. Returns order, state, priority, progress, and due dates. If trackId is omitted, the currently scoped track is used.";

    public GeminiFunctionDeclaration Declaration => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new GeminiSchema
        {
            Type = "OBJECT",
            Properties = new Dictionary<string, GeminiSchemaProperty>
            {
                ["trackId"] = new()
                {
                    Type = "STRING",
                    Description = "The UUID of the track. Optional if already scoped to a track."
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
                    Description = "Optional status filter (e.g. 'Todo', 'InProgress', 'Review', 'Done', 'OnHold')."
                }
            }
        }
    };

    public GetMajorTasksTool(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public async Task<string> ExecuteAsync(JsonElement arguments, OctoScope scope, CancellationToken cancellationToken = default)
    {
        Guid targetTrackId = Guid.Empty;

        if (arguments.TryGetProperty("trackId", out var trackProp) && Guid.TryParse(trackProp.GetString(), out var parsedId))
        {
            targetTrackId = parsedId;
        }
        else if (scope.TrackId.HasValue)
        {
            targetTrackId = scope.TrackId.Value;
        }

        if (targetTrackId == Guid.Empty)
        {
            return JsonSerializer.Serialize(new { error = "No track ID provided or found in the current scope. Please specify a trackId." });
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
            var result = await _taskService.GetMajorTasksByTrackAsync(targetTrackId, cancellationToken);
            var query = result.MajorTasks.AsEnumerable();

            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<MajorTaskState>(statusFilter, true, out var parsedState))
            {
                query = query.Where(t => t.State == parsedState);
            }

            var totalMatching = query.Count();
            var paged = query
                .OrderBy(t => t.Order)
                .ThenByDescending(t => t.Priority)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    id = t.Id,
                    trackId = t.TrackId,
                    title = t.Title,
                    description = t.Description,
                    state = t.State.ToString(),
                    priority = t.Priority.ToString(),
                    order = t.Order,
                    progress = t.Progress,
                    dueDate = t.DueDate?.ToString("yyyy-MM-dd"),
                    assignedUserId = t.AssignedUserId,
                    isCompleted = t.State == MajorTaskState.Done,
                    isActionable = t.State is MajorTaskState.Todo or MajorTaskState.InProgress
                })
                .ToList();

            return JsonSerializer.Serialize(new
            {
                trackId = targetTrackId,
                tasks = paged,
                totalCount = totalMatching,
                pageNumber = pageNumber,
                pageSize = pageSize,
                hasMorePages = (pageNumber * pageSize) < totalMatching
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to retrieve major tasks for track {targetTrackId}: {ex.Message}" });
        }
    }
}
