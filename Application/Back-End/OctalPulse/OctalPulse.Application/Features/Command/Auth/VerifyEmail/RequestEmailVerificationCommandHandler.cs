using MediatR;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Features.Command.Auth.VerifyEmail;

public class RequestEmailVerificationCommandHandler : IRequestHandler<RequestEmailVerificationCommand, RequestEmailVerificationResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly IOtpService _otpService;
    private readonly INotificationService _notificationService;

    public RequestEmailVerificationCommandHandler(
        UserManager<User> userManager,
        IOtpService otpService,
        INotificationService notificationService)
    {
        _userManager = userManager;
        _otpService = otpService;
        _notificationService = notificationService;
    }

    public async Task<RequestEmailVerificationResponse> Handle(RequestEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new ValidationException("No account found with this email.");

        if (user.EmailConfirmed)
            throw new ValidationException("This email is already verified.");

        var code = await _otpService.IssueOtpAsync(request.Email, OtpPurpose.EmailVerification, cancellationToken: cancellationToken);
        await _notificationService.SendEmailVerificationAsync(request.Email, code, cancellationToken);

        return new RequestEmailVerificationResponse("Verification code sent to your email.");
    }
}