using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Application.Features.Command.Auth.Revoke;

public class RevokeRefreshTokenCommandHandler : IRequestHandler<RevokeRefreshTokenCommand, Unit>
{
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeRefreshTokenCommandHandler(
        IJwtService jwtService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _jwtService = jwtService;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(RevokeRefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = _jwtService.HashRefreshToken(request.RefreshToken);

        var stored = await _refreshTokenRepository.FirstOrDefaultAsync(
            rt => rt.TokenHash == hash && rt.RevokedAt == null,
            cancellationToken);

        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            stored.ModifiedDate = DateTime.UtcNow;
            _refreshTokenRepository.Update(stored);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}