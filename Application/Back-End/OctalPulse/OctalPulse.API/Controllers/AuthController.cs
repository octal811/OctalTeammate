using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Features.Command.Auth.Login;
using OctalPulse.Application.Features.Command.Auth.RefreshToken;
using OctalPulse.Application.Features.Command.Auth.Register;
using OctalPulse.Application.Features.Command.Auth.ResetPassword;
using OctalPulse.Application.Features.Command.Auth.Revoke;
using OctalPulse.Application.Features.Command.Auth.VerifyEmail;

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
    [ApiAvailability("UserRegistration")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("login")]
    [ApiAvailability("UserLogin")]
    public async Task<ActionResult<LoginResponse>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [UserOperationLock("refresh")]
    public async Task<ActionResult<RefreshTokenResponse>> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RevokeRefreshTokenCommand command, CancellationToken cancellationToken)
    {
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("verify-email/request")]
    [UserOperationLock("verify-email:request")]
    public async Task<ActionResult<RequestEmailVerificationResponse>> RequestEmailVerification(
        RequestEmailVerificationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("verify-email")]
    [UserOperationLock("verify-email:confirm")]
    public async Task<ActionResult<VerifyEmailResponse>> VerifyEmail(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("password-reset/request")]
    [UserOperationLock("password-reset:request")]
    public async Task<ActionResult<RequestPasswordResetResponse>> RequestPasswordReset(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("password-reset")]
    [UserOperationLock("password-reset:confirm")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }
}