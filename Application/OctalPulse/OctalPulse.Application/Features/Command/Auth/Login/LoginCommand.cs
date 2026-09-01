using MediatR;

namespace OctalPulse.Application.Features.Command.Auth.Login;

public record LoginCommand(
    string Email,
    string Password) : IRequest<AuthResponse>;