using FluentValidation;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.UserProfile.SetUserImage;

public class SetUserImageCommandValidator : AbstractValidator<SetUserImageCommand>
{
    public SetUserImageCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.ImageType)
            .IsInEnum().WithMessage("A valid image type is required.");

        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Image URL is required.")
            .MaximumLength(500).WithMessage("Image URL must not exceed 500 characters.");
    }
}