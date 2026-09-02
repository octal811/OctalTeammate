using FluentValidation;

namespace OctalPulse.Application.Features.Query.Track.GetTracksByProject;

public class GetTracksByProjectQueryValidator : AbstractValidator<GetTracksByProjectQuery>
{
    public GetTracksByProjectQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}