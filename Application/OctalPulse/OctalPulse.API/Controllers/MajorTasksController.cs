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

    [HttpGet("track/{trackId:guid}")]
    public async Task<ActionResult<GetMajorTasksByTrackResponse>> GetByTrack(
        Guid trackId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMajorTasksByTrackQuery(trackId, GetUserId()),
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpdateMajorTaskResponse>> Update(
        Guid id,
        [FromBody] UpdateMajorTaskCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { Id = id, UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [UserOperationLock("majortasks:delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteMajorTaskCommand(id, GetUserId()), cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
