using FluentValidation;

namespace OctalPulse.Application.Features.Command.Track.ReviewTrackJoin;

public class ReviewTrackJoinCommandValidator : AbstractValidator<ReviewTrackJoinCommand>
{
    public ReviewTrackJoinCommandValidator()
    {
        RuleFor(x => x.TrackId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.TargetUserId)
            .NotEmpty();
    }
}
