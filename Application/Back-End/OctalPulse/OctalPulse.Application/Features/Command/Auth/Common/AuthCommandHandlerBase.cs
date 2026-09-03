using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using RefreshTokenEntity = global::OctalPulse.Domain.Entities.RefreshToken;

namespace OctalPulse.Application.Features.Command.Auth.Common;

public abstract class AuthCommandHandlerBase
{
    protected readonly IJwtService JwtService;
    protected readonly IRefreshTokenRepository RefreshTokenRepository;
    protected readonly IUnitOfWork UnitOfWork;

    protected AuthCommandHandlerBase(
        IJwtService jwtService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        JwtService = jwtService;
        RefreshTokenRepository = refreshTokenRepository;
        UnitOfWork = unitOfWork;
    }

    protected async Task<TokenIssueResult> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var access = JwtService.GenerateAccessToken(user);
        var refresh = JwtService.GenerateRefreshToken();

        await RefreshTokenRepository.AddAsync(
            new RefreshTokenEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = refresh.Hash,
                ExpiresAt = refresh.ExpiresAtUtc,
                CreatedDate = DateTime.UtcNow
            },
            cancellationToken);

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return new TokenIssueResult(
            access.Token,
            refresh.Token,
            access.ExpiresAtUtc,
            refresh.ExpiresAtUtc,
            user.Id,
            user.Email ?? string.Empty,
            user.Name);
    }
}