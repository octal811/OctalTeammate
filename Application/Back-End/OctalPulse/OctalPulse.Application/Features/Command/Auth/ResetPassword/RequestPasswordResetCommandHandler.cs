using MediatR;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Features.Command.Auth.ResetPassword;

public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, RequestPasswordResetResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly IOtpService _otpService;
    private readonly INotificationService _notificationService;

    public RequestPasswordResetCommandHandler(
        UserManager<User> userManager,
        IOtpService otpService,
        INotificationService notificationService)
    {
        _userManager = userManager;
        _otpService = otpService;
        _notificationService = notificationService;
    }

    public async Task<RequestPasswordResetResponse> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        // Do not reveal whether an account exists. Always behave the same,
        // but only actually send the email when an account is found.
        if (user is null)
            return new RequestPasswordResetResponse("If an account exists, a reset code has been sent to your email.");

        if (await _otpService.HasActiveOtpAsync(request.Email, OtpPurpose.PasswordReset, cancellationToken))
        {
            throw new ValidationException(
                "A reset request is already pending. Please check your email, or wait for the code to expire before trying again.");
        }

        var code = await _otpService.IssueOtpAsync(request.Email, OtpPurpose.PasswordReset, cancellationToken: cancellationToken);
        await _notificationService.SendPasswordResetAsync(request.Email, code, cancellationToken);

        return new RequestPasswordResetResponse("If an account exists, a reset code has been sent to your email.");
    }
}