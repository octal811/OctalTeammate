using MediatR;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Features.Command.Auth.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly UserManager<User> _userManager;
    private readonly IOtpService _otpService;

    public ResetPasswordCommandHandler(UserManager<User> userManager, IOtpService otpService)
    {
        _userManager = userManager;
        _otpService = otpService;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var validation = await _otpService.ValidateOtpAsync(
            request.Email,
            OtpPurpose.PasswordReset,
            request.Otp,
            cancellationToken);

        if (validation != OtpValidationResult.Valid)
            throw MapValidationFailure(validation);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new ValidationException("No account found with this email.");

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Unit.Value;
    }

    private static ValidationException MapValidationFailure(OtpValidationResult result)
    {
        return result switch
        {
            OtpValidationResult.Expired => new("The OTP has expired. Please request a new one."),
            OtpValidationResult.TooManyAttempts => new("Too many invalid attempts. Please request a new OTP."),
            _ => new("The OTP you entered is incorrect.")
        };
    }
}