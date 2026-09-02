using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.MinorTask.CreateMinorTask;
using OctalPulse.Application.Features.Command.MinorTask.DeleteMinorTask;
using OctalPulse.Application.Features.Command.MinorTask.UpdateMinorTask;
using OctalPulse.Application.Features.Query.MinorTask.GetMinorTasksByMajorTask;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MinorTasksController : ControllerBase
{
    private readonly ISender _sender;

    public MinorTasksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("major/{majorTaskId:guid}")]
    public async Task<ActionResult<GetMinorTasksByMajorTaskResponse>> GetByMajorTask(
        Guid majorTaskId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMinorTasksByMajorTaskQuery(majorTaskId, GetUserId()),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [UserOperationLock("minortasks:create")]
    public async Task<ActionResult<CreateMinorTaskResponse>> Create(
        [FromBody] CreateMinorTaskCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpdateMinorTaskResponse>> Update(
        Guid id,
        [FromBody] UpdateMinorTaskCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { Id = id, UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [UserOperationLock("minortasks:delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteMinorTaskCommand(id, GetUserId()), cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
