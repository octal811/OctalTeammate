using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.Event.CreateEvent;
using OctalPulse.Application.Features.Command.Event.DeleteEvent;
using OctalPulse.Application.Features.Command.Event.UpdateEvent;
using OctalPulse.Application.Features.Query.Event.GetEventsByMonth;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly ISender _sender;

    public EventsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("month")]
    public async Task<ActionResult<GetEventsByMonthResponse>> GetByMonth(
        [FromBody] GetEventsByMonthQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            query with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [UserOperationLock("events:create")]
    public async Task<ActionResult<CreateEventResponse>> Create(
        [FromBody] CreateEventCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { CreatedByUserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<UpdateEventResponse>> Update(
        [FromBody] UpdateEventCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    [UserOperationLock("events:delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteEventCommand command,
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
