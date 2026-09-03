using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.Project.CreateProject;
using OctalPulse.Application.Features.Command.Project.DeleteProject;
using OctalPulse.Application.Features.Command.Project.RequestProjectJoin;
using OctalPulse.Application.Features.Command.Project.ReviewProjectJoin;
using OctalPulse.Application.Features.Command.Project.UpdateProject;
using OctalPulse.Application.Features.Command.Project.UpdateProjectRoles;
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
        var result = await _sender.Send(
            query with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<GetProjectByIdResponse>> GetById(
        [FromBody] GetProjectByIdQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            query with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("tracks")]
    public async Task<ActionResult<GetTracksByProjectResponse>> GetTracks(
        [FromBody] GetTracksByProjectQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            query with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<UpdateProjectResponse>> Update(
        [FromBody] UpdateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    [UserOperationLock("projects:delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteProjectCommand command,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return NoContent();
    }

    [HttpPost("join")]
    public async Task<ActionResult<RequestProjectJoinResponse>> RequestJoin(
        [FromBody] RequestProjectJoinCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("approve-join")]
    public async Task<ActionResult<ReviewProjectJoinResponse>> ApproveJoin(
        [FromBody] ReviewProjectJoinCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId(), Approve = true },
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("reject-join")]
    public async Task<ActionResult<ReviewProjectJoinResponse>> RejectJoin(
        [FromBody] ReviewProjectJoinCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId(), Approve = false },
            cancellationToken);
        return Ok(result);
    }

    [HttpPut("roles")]
    public async Task<ActionResult<UpdateProjectRolesResponse>> UpdateRoles(
        [FromBody] UpdateProjectRolesCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
    }
}
