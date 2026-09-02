using MediatR;

namespace OctalPulse.Application.Features.Command.Auth.ResetPassword;

public record ResetPasswordCommand(string Email, string Otp, string NewPassword) : IRequest<ResetPasswordResponse>;