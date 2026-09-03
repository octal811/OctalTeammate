using FluentValidation;

namespace OctalPulse.Application.Features.Command.Track.DeleteTrack;

public class DeleteTrackCommandValidator : AbstractValidator<DeleteTrackCommand>
{
    public DeleteTrackCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}