using MediatR;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Features.Command.Auth.VerifyEmail;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, VerifyEmailResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly IOtpService _otpService;

    public VerifyEmailCommandHandler(UserManager<User> userManager, IOtpService otpService)
    {
        _userManager = userManager;
        _otpService = otpService;
    }

    public async Task<VerifyEmailResponse> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var validation = await _otpService.ValidateOtpAsync(
            request.Email,
            OtpPurpose.EmailVerification,
            request.Otp,
            cancellationToken);

        if (validation != OtpValidationResult.Valid)
            throw MapValidationFailure(validation);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new ValidationException("No account found with this email.");

        if (user.EmailConfirmed)
            throw new ValidationException("This email is already verified.");

        user.EmailConfirmed = true;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return new VerifyEmailResponse("Email verified successfully.");
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