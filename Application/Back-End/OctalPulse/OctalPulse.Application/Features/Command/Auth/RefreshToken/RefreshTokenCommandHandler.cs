using MediatR;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Command.Auth.Common;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Features.Command.Auth.RefreshToken;

public class RefreshTokenCommandHandler : AuthCommandHandlerBase, IRequestHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    private readonly UserManager<User> _userManager;

    public RefreshTokenCommandHandler(
        UserManager<User> userManager,
        IJwtService jwtService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
        : base(jwtService, refreshTokenRepository, unitOfWork)
    {
        _userManager = userManager;
    }

    public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = JwtService.HashRefreshToken(request.RefreshToken);

        var stored = await RefreshTokenRepository.FirstOrDefaultAsync(
            rt => rt.TokenHash == hash && rt.RevokedAt == null,
            cancellationToken);

        if (stored is null)
            throw new UnauthorizedException("Invalid refresh token.");

        if (stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token has expired.");

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || user.IsDeleted)
            throw new UnauthorizedException("User no longer exists.");

        stored.RevokedAt = DateTime.UtcNow;
        stored.ModifiedDate = DateTime.UtcNow;
        RefreshTokenRepository.Update(stored);

        var result = await IssueTokensAsync(user, cancellationToken);
        return new RefreshTokenResponse(
            result.AccessToken,
            result.RefreshToken,
            result.AccessTokenExpiresAt,
            result.RefreshTokenExpiresAt,
            result.UserId,
            result.Email,
            result.Name);
    }
}