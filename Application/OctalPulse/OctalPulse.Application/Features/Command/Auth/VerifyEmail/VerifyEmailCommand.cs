using MediatR;

namespace OctalPulse.Application.Features.Command.Auth.VerifyEmail;

public record VerifyEmailCommand(string Email, string Otp) : IRequest<Unit>;