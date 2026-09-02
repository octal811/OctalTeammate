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

    [HttpGet("project/{projectId:guid}/month")]
    public async Task<ActionResult<GetEventsByMonthResponse>> GetByMonth(
        Guid projectId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetEventsByMonthQuery(projectId, year, month, GetUserId()),
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpdateEventResponse>> Update(
        Guid id,
        [FromBody] UpdateEventCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { Id = id, UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [UserOperationLock("events:delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteEventCommand(id, GetUserId()), cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
