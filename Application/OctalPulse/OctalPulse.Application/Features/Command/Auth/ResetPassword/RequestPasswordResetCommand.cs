using MediatR;

namespace OctalPulse.Application.Features.Command.Auth.ResetPassword;

public record RequestPasswordResetCommand(string Email) : IRequest<RequestPasswordResetResponse>;