using FluentValidation;

namespace OctalPulse.Application.Features.Command.Auth.VerifyEmail;

public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Otp)
            .NotEmpty()
            .Length(8)
            .Matches("^[0-9]+$").WithMessage("OTP must be a numeric code.");
    }
}