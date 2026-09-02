using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.Project.CreateProject;
using OctalPulse.Application.Features.Command.Project.DeleteProject;
using OctalPulse.Application.Features.Command.Project.UpdateProject;
using OctalPulse.Application.Features.Query.Project.GetAllProjects;
using OctalPulse.Application.Features.Query.Project.GetProjectById;
using OctalPulse.Application.Features.Query.Track.GetTracksByProject;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly ISender _sender;

    public ProjectsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [UserOperationLock("projects:create")]
    public async Task<ActionResult<CreateProjectResponse>> Create(
        [FromBody] CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { CreatedByUserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("list")]
    public async Task<ActionResult<GetAllProjectsResponse>> GetAll(
        [FromBody] GetAllProjectsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetProjectByIdResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetProjectByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/tracks")]
    public async Task<ActionResult<GetTracksByProjectResponse>> GetTracks(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTracksByProjectQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpdateProjectResponse>> Update(
        Guid id,
        [FromBody] UpdateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [UserOperationLock("projects:delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteProjectCommand(id), cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
    }
}