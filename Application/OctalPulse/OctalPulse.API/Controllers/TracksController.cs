using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.Track.CreateTrack;
using OctalPulse.Application.Features.Command.Track.DeleteTrack;
using OctalPulse.Application.Features.Command.Track.RequestTrackJoin;
using OctalPulse.Application.Features.Command.Track.ReviewTrackJoin;
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpdateTrackResponse>> Update(
        Guid id,
        [FromBody] UpdateTrackCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [UserOperationLock("tracks:delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteTrackCommand(id, GetUserId()), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/join-request")]
    public async Task<ActionResult<RequestTrackJoinResponse>> RequestJoin(
        Guid id,
        [FromBody] RequestTrackJoinCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { TrackId = id, UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/join-requests/{userId:guid}/approve")]
    public async Task<ActionResult<ReviewTrackJoinResponse>> ApproveJoin(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReviewTrackJoinCommand(id, GetUserId(), userId, true),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/join-requests/{userId:guid}/reject")]
    public async Task<ActionResult<ReviewTrackJoinResponse>> RejectJoin(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReviewTrackJoinCommand(id, GetUserId(), userId, false),
            cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
