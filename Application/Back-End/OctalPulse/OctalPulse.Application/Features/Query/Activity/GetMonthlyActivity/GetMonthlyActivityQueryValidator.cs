using FluentValidation;

namespace OctalPulse.Application.Features.Query.Activity.GetMonthlyActivity;

public class GetMonthlyActivityQueryValidator : AbstractValidator<GetMonthlyActivityQuery>
{
    public GetMonthlyActivityQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, DateTime.UtcNow.Year + 1);

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12);
    }
}