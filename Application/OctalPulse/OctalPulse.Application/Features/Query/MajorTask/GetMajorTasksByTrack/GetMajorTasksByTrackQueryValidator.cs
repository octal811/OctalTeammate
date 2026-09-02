using FluentValidation;

namespace OctalPulse.Application.Features.Query.MajorTask.GetMajorTasksByTrack;

public class GetMajorTasksByTrackQueryValidator : AbstractValidator<GetMajorTasksByTrackQuery>
{
    public GetMajorTasksByTrackQueryValidator()
    {
        RuleFor(x => x.TrackId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
