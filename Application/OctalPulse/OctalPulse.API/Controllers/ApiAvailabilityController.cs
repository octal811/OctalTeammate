using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.Application.Features.Command.Admin.Availability;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class ApiAvailabilityController : ControllerBase
{
    private readonly IApiAvailabilityService _service;

    public ApiAvailabilityController(IApiAvailabilityService service)
    {
        _service = service;
    }

    [HttpPost("enable")]
    public async Task<IActionResult> Enable(
        [FromBody] AvailabilityCommand command,
        CancellationToken cancellationToken)
    {
        var adminId = GetAdminId();
        await _service.EnableAsync(command.Key, adminId, cancellationToken);
        return Ok(new AvailabilityResponse(command.Key, true));
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable(
        [FromBody] AvailabilityCommand command,
        CancellationToken cancellationToken)
    {
        var adminId = GetAdminId();
        await _service.DisableAsync(command.Key, adminId, cancellationToken);
        return Ok(new AvailabilityResponse(command.Key, false));
    }

    [HttpGet("status")]
    public async Task<ActionResult<ApiAvailabilityStatus>> GetStatus(
        [FromBody] AvailabilityCommand command,
        CancellationToken cancellationToken)
    {
        var status = await _service.GetStatusAsync(command.Key, cancellationToken);
        return Ok(status);
    }

    private Guid? GetAdminId()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }
}

public record AvailabilityResponse(string Key, bool IsEnabled);
