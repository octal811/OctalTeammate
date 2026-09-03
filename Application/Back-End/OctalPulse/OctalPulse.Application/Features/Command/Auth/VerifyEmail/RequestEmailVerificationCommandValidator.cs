using FluentValidation;

namespace OctalPulse.Application.Features.Command.Auth.VerifyEmail;

public class RequestEmailVerificationCommandValidator : AbstractValidator<RequestEmailVerificationCommand>
{
    public RequestEmailVerificationCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
    }
}