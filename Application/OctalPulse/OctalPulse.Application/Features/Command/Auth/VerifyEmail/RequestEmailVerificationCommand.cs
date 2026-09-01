using MediatR;

namespace OctalPulse.Application.Features.Command.Auth.VerifyEmail;

public record RequestEmailVerificationCommand(string Email) : IRequest<Unit>;