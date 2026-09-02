using FluentValidation;

namespace OctalPulse.Application.Features.Command.Track.RequestTrackJoin;

public class RequestTrackJoinCommandValidator : AbstractValidator<RequestTrackJoinCommand>
{
    public RequestTrackJoinCommandValidator()
    {
        RuleFor(x => x.TrackId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
