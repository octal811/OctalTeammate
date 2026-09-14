using FluentValidation;

namespace OctalPulse.Application.Features.Command.Track.LeaveTrack;

public class LeaveTrackCommandValidator : AbstractValidator<LeaveTrackCommand>
{
    public LeaveTrackCommandValidator()
    {
        RuleFor(x => x.TrackId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}