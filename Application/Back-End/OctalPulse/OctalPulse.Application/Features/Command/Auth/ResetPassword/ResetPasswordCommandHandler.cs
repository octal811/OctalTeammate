using MediatR;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Features.Command.Auth.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly IOtpService _otpService;

    public ResetPasswordCommandHandler(UserManager<User> userManager, IOtpService otpService)
    {
        _userManager = userManager;
        _otpService = otpService;
    }

    public async Task<ResetPasswordResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new ValidationException("No account found with this email.");

        ValidatePasswordOrThrow(request.NewPassword);

        var validation = await _otpService.ValidateOtpAsync(
            request.Email,
            OtpPurpose.PasswordReset,
            request.Otp,
            cancellationToken);

        if (validation != OtpValidationResult.Valid)
            throw MapValidationFailure(validation);

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return new ResetPasswordResponse("Password has been reset. You can now log in with your new password.");
    }

    private static void ValidatePasswordOrThrow(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ValidationException("Password is required.");

        var errors = new List<string>();

        if (password.Length < 8)
            errors.Add("Password must be at least 8 characters long.");
        if (!password.Any(char.IsUpper))
            errors.Add("Password must contain an uppercase letter.");
        if (!password.Any(char.IsLower))
            errors.Add("Password must contain a lowercase letter.");
        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain a digit.");
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            errors.Add("Password must contain at least one non-alphanumeric character.");

        if (errors.Count > 0)
            throw new ValidationException(string.Join("; ", errors));
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