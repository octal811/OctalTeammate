using FluentValidation;

namespace OctalPulse.Application.Features.Query.MinorTask.GetMinorTasksByMajorTask;

public class GetMinorTasksByMajorTaskQueryValidator : AbstractValidator<GetMinorTasksByMajorTaskQuery>
{
    public GetMinorTasksByMajorTaskQueryValidator()
    {
        RuleFor(x => x.MajorTaskId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
