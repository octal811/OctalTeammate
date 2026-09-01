using MediatR;

namespace OctalPulse.Application.Features.Command.Auth.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;