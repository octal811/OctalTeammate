using MediatR;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Command.Auth.Common;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Auth.Register;

public class RegisterCommandHandler : AuthCommandHandlerBase, IRequestHandler<RegisterCommand, RegisterResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly IOtpService _otpService;
    private readonly INotificationService _notificationService;

    public RegisterCommandHandler(
        UserManager<User> userManager,
        IJwtService jwtService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        INotificationService notificationService)
        : base(jwtService, refreshTokenRepository, unitOfWork)
    {
        _userManager = userManager;
        _otpService = otpService;
        _notificationService = notificationService;
    }

    public async Task<RegisterResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            throw new ValidationException("Email is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            Name = request.Name,
            MainRole = request.MainRole,
            Rank = UserRank.Member,
            EmailConfirmed = false,
            IsDeleted = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        // Automatically issue and dispatch email verification OTP via MailKit
        var otp = await _otpService.IssueOtpAsync(request.Email, OtpPurpose.EmailVerification, cancellationToken: cancellationToken);
        await _notificationService.SendEmailVerificationAsync(request.Email, otp, cancellationToken);

        var tokens = await IssueTokensAsync(user, cancellationToken);
        return new RegisterResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAt,
            tokens.RefreshTokenExpiresAt,
            tokens.UserId,
            tokens.Email,
            tokens.Name);
    }
}