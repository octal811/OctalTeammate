using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.MajorTask.CreateMajorTask;
using OctalPulse.Application.Features.Command.MajorTask.DeleteMajorTask;
using OctalPulse.Application.Features.Command.MajorTask.UpdateMajorTask;
using OctalPulse.Application.Features.Query.MajorTask.GetMajorTasksByTrack;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MajorTasksController : ControllerBase
{
    private readonly ISender _sender;

    public MajorTasksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("track")]
    public async Task<ActionResult<GetMajorTasksByTrackResponse>> GetByTrack(
        [FromBody] GetMajorTasksByTrackQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            query with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [UserOperationLock("majortasks:create")]
    public async Task<ActionResult<CreateMajorTaskResponse>> Create(
        [FromBody] CreateMajorTaskCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<UpdateMajorTaskResponse>> Update(
        [FromBody] UpdateMajorTaskCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    [UserOperationLock("majortasks:delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteMajorTaskCommand command,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
