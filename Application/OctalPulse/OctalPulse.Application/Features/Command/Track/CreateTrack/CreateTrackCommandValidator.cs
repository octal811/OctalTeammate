using FluentValidation;

namespace OctalPulse.Application.Features.Command.Track.CreateTrack;

public class CreateTrackCommandValidator : AbstractValidator<CreateTrackCommand>
{
    public CreateTrackCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(1000);
    }
}