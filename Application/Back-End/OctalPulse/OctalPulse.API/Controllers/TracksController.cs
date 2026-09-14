using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.Track.CreateTrack;
using OctalPulse.Application.Features.Command.Track.DeleteTrack;
using OctalPulse.Application.Features.Command.Track.LeaveTrack;
using OctalPulse.Application.Features.Command.Track.RequestTrackJoin;
using OctalPulse.Application.Features.Command.Track.UpdateTrack;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TracksController : ControllerBase
{
    private readonly ISender _sender;

    public TracksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [UserOperationLock("tracks:create")]
    public async Task<ActionResult<CreateTrackResponse>> Create(
        [FromBody] CreateTrackCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { CreatedByUserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<UpdateTrackResponse>> Update(
        [FromBody] UpdateTrackCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    [UserOperationLock("tracks:delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteTrackCommand command,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return NoContent();
    }

    [HttpPost("join")]
    public async Task<ActionResult<RequestTrackJoinResponse>> Join(
        [FromBody] RequestTrackJoinCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("leave")]
    public async Task<ActionResult<LeaveTrackResponse>> Leave(
        [FromBody] LeaveTrackCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
