using FluentValidation;

namespace OctalPulse.Application.Features.Command.Track.UpdateTrack;

public class UpdateTrackCommandValidator : AbstractValidator<UpdateTrackCommand>
{
    public UpdateTrackCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(1000);
    }
}