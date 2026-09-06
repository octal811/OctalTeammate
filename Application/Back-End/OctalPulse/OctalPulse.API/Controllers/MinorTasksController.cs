using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.MinorTask.AddMinorTaskWorkTime;
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

    [HttpGet("major")]
    public async Task<ActionResult<GetMinorTasksByMajorTaskResponse>> GetByMajorTask(
        [FromBody] GetMinorTasksByMajorTaskQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            query with { UserId = GetUserId() },
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

    [HttpPut]
    public async Task<ActionResult<UpdateMinorTaskResponse>> Update(
        [FromBody] UpdateMinorTaskCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("worktime")]
    public async Task<ActionResult<AddMinorTaskWorkTimeResponse>> AddWorkTime(
        [FromBody] AddMinorTaskWorkTimeCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command with { UserId = GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    [UserOperationLock("minortasks:delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteMinorTaskCommand command,
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
