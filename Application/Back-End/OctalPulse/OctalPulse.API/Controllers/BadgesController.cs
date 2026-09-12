using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.Application.Features.Query.Badges.GetMyBadges;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BadgesController : ControllerBase
{
    private readonly ISender _sender;

    public BadgesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<GetMyBadgesResponse>> GetMyBadges(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _sender.Send(new GetMyBadgesQuery(userId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<GetMyBadgesResponse>> GetBadges(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyBadgesQuery(userId), cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
    }
}