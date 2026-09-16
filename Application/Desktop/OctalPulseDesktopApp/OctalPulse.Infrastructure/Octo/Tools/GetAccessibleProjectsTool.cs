using System.Text.Json;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Octo.Tools;

public class GetAccessibleProjectsTool : IOctoTool
{
    private readonly IProjectService _projectService;

    public string Name => "get_accessible_projects";
    public string Description => "Retrieves a paginated list of projects accessible to the authenticated user. Limited to 10 projects per request.";

    public GeminiFunctionDeclaration Declaration => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new GeminiSchema
        {
            Type = "OBJECT",
            Properties = new Dictionary<string, GeminiSchemaProperty>
            {
                ["pageNumber"] = new()
                {
                    Type = "INTEGER",
                    Description = "Page number to retrieve (1-based, default: 1)."
                },
                ["pageSize"] = new()
                {
                    Type = "INTEGER",
                    Description = "Number of items per page (maximum 10, default: 10)."
                }
            }
        }
    };

    public GetAccessibleProjectsTool(IProjectService _projectService)
    {
        this._projectService = _projectService;
    }

    public async Task<string> ExecuteAsync(JsonElement arguments, OctoScope scope, CancellationToken cancellationToken = default)
    {
        var pageNumber = 1;
        var pageSize = 10;

        if (arguments.TryGetProperty("pageNumber", out var pageProp) && pageProp.TryGetInt32(out var p))
            pageNumber = Math.Max(1, p);

        if (arguments.TryGetProperty("pageSize", out var sizeProp) && sizeProp.TryGetInt32(out var s))
            pageSize = Math.Clamp(s, 1, 10);

        try
        {
            var result = await _projectService.GetAllProjectsAsync(pageNumber, pageSize, cancellationToken);
            var items = result.Items.Select(x => new
            {
                id = x.Id,
                title = x.Title,
                description = x.Description,
                progress = x.Progress,
                status = x.Status.ToString()
            }).ToList();

            var responseObj = new
            {
                projects = items,
                totalCount = result.TotalCount,
                pageNumber = result.PageNumber,
                pageSize = result.PageSize,
                hasMorePages = (result.PageNumber * result.PageSize) < result.TotalCount
            };

            return JsonSerializer.Serialize(responseObj);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to retrieve projects: {ex.Message}" });
        }
    }
}
