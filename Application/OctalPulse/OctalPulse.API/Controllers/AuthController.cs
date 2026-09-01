using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.Domain.Enums;
using OctalPulse.Application.Features.Command.Auth;
using OctalPulse.Application.Features.Command.Auth.Login;
using OctalPulse.Application.Features.Command.Auth.RefreshToken;
using OctalPulse.Application.Features.Command.Auth.Register;
using OctalPulse.Application.Features.Command.Auth.Revoke;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(
            request.Email,
            request.Name,
            request.MainRole,
            request.Password);

        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RefreshRequest request, CancellationToken cancellationToken)
    {
        var command = new RevokeRefreshTokenCommand(request.RefreshToken);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }
}

public record RegisterRequest(string Email, string Name, UserRole MainRole, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);