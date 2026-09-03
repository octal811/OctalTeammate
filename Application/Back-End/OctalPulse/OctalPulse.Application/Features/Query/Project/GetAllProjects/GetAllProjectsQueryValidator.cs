using FluentValidation;

namespace OctalPulse.Application.Features.Query.Project.GetAllProjects;

public class GetAllProjectsQueryValidator : AbstractValidator<GetAllProjectsQuery>
{
    public GetAllProjectsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);
    }
}