using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Domain.Enums;using OctalPulse.Application.Features.Command.Auth;
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
    [ApiAvailability("UserLogin")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [UserOperationLock("refresh")]
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

    [HttpPost("verify-email/request")]
    [UserOperationLock("verify-email:request")]
    public async Task<IActionResult> RequestEmailVerification(EmailRequest request, CancellationToken cancellationToken)
    {
        await _sender.Send(new RequestEmailVerificationCommand(request.Email), cancellationToken);
        return Ok(new { message = "Verification code sent to your email." });
    }

    [HttpPost("verify-email")]
    [UserOperationLock("verify-email:confirm")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await _sender.Send(new VerifyEmailCommand(request.Email, request.Otp), cancellationToken);
        return Ok(new { message = "Email verified successfully." });
    }

    [HttpPost("password-reset/request")]
    [UserOperationLock("password-reset:request")]
    public async Task<IActionResult> RequestPasswordReset(EmailRequest request, CancellationToken cancellationToken)
    {
        await _sender.Send(new RequestPasswordResetCommand(request.Email), cancellationToken);
        return Ok(new { message = "If an account exists, a reset code has been sent to your email." });
    }

    [HttpPost("password-reset")]
    [UserOperationLock("password-reset:confirm")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _sender.Send(new ResetPasswordCommand(request.Email, request.Otp, request.NewPassword), cancellationToken);
        return Ok(new { message = "Password has been reset. You can now log in with your new password." });
    }
}

public record RegisterRequest(string Email, string Name, UserRole MainRole, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record EmailRequest(string Email);

public record VerifyEmailRequest(string Email, string Otp);

public record ResetPasswordRequest(string Email, string Otp, string NewPassword);