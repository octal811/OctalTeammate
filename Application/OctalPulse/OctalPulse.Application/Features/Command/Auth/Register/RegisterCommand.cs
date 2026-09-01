using MediatR;

namespace OctalPulse.Application.Features.Command.Auth.Register;

public record RegisterCommand(
    string Email,
    string Name,
    Domain.Enums.UserRole MainRole,
    string Password) : IRequest<AuthResponse>;