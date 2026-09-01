using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost("{key}/enable")]
    public async Task<IActionResult> Enable(string key, CancellationToken cancellationToken)
    {
        var adminId = GetAdminId();
        await _service.EnableAsync(key, adminId, cancellationToken);
        return Ok(new AvailabilityResponse(key, true));
    }

    [HttpPost("{key}/disable")]
    public async Task<IActionResult> Disable(string key, CancellationToken cancellationToken)
    {
        var adminId = GetAdminId();
        await _service.DisableAsync(key, adminId, cancellationToken);
        return Ok(new AvailabilityResponse(key, false));
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<ApiAvailabilityStatus>> GetStatus(string key, CancellationToken cancellationToken)
    {
        var status = await _service.GetStatusAsync(key, cancellationToken);
        return Ok(status);
    }

    private Guid? GetAdminId()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }
}

public record AvailabilityResponse(string Key, bool IsEnabled);