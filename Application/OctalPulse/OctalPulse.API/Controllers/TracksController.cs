using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.Track.CreateTrack;
using OctalPulse.Application.Features.Command.Track.DeleteTrack;
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
        var result = await _sender.Send(command, cancellationToken);
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
        await _sender.Send(new DeleteTrackCommand(id), cancellationToken);
        return NoContent();
    }
}