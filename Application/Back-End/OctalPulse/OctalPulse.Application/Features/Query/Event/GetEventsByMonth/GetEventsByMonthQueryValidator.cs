using FluentValidation;

namespace OctalPulse.Application.Features.Query.Event.GetEventsByMonth;

public class GetEventsByMonthQueryValidator : AbstractValidator<GetEventsByMonthQuery>
{
    public GetEventsByMonthQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100);

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12);
    }
}
