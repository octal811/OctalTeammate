using MediatR;

namespace OctalPulse.Application.Features.Command.Auth.Revoke;

public record RevokeRefreshTokenCommand(string RefreshToken) : IRequest<Unit>;