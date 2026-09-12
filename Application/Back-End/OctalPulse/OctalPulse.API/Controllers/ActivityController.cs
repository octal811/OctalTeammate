using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.Application.Features.Query.Activity.GetMonthlyActivity;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ActivityController : ControllerBase
{
    private readonly ISender _sender;

    public ActivityController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("monthly/{year:int}/{month:int}")]
    public async Task<ActionResult<GetMonthlyActivityResponse>> GetMonthlyActivity(
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _sender.Send(
            new GetMonthlyActivityQuery(userId, year, month),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("monthly/{userId:guid}/{year:int}/{month:int}")]
    public async Task<ActionResult<GetMonthlyActivityResponse>> GetMonthlyActivityForUser(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMonthlyActivityQuery(userId, year, month),
            cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
    }
}