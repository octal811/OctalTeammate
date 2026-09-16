using System.Text.Json;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Octo.Tools;

public class GetTracksTool : IOctoTool
{
    private readonly IProjectService _projectService;

    public string Name => "get_tracks";
    public string Description => "Retrieves the list of tracks (functional areas / deliverables) inside a project. If projectId is omitted, the currently scoped project is used.";

    public GeminiFunctionDeclaration Declaration => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new GeminiSchema
        {
            Type = "OBJECT",
            Properties = new Dictionary<string, GeminiSchemaProperty>
            {
                ["projectId"] = new()
                {
                    Type = "STRING",
                    Description = "The UUID of the project whose tracks should be fetched. Optional if scoped to a project."
                }
            }
        }
    };

    public GetTracksTool(IProjectService projectService)
    {
        _projectService = projectService;
    }

    public async Task<string> ExecuteAsync(JsonElement arguments, OctoScope scope, CancellationToken cancellationToken = default)
    {
        Guid targetProjectId = Guid.Empty;

        if (arguments.TryGetProperty("projectId", out var projProp) && Guid.TryParse(projProp.GetString(), out var parsedId))
        {
            targetProjectId = parsedId;
        }
        else if (scope.ProjectId.HasValue)
        {
            targetProjectId = scope.ProjectId.Value;
        }

        if (targetProjectId == Guid.Empty)
        {
            return JsonSerializer.Serialize(new { error = "No project ID provided or found in the current scope. Please specify a projectId." });
        }

        try
        {
            var result = await _projectService.GetTracksByProjectAsync(targetProjectId, cancellationToken);
            var tracks = result.Tracks.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                description = t.Description,
                progress = t.Progress,
                leadUserId = t.TrackLeadUserId,
                memberCount = t.Members?.Count ?? 0
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                projectId = targetProjectId,
                tracks = tracks,
                count = tracks.Count
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to fetch tracks for project {targetProjectId}: {ex.Message}" });
        }
    }
}
