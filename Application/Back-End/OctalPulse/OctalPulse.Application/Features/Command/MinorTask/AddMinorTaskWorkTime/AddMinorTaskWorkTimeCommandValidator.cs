using FluentValidation;

namespace OctalPulse.Application.Features.Command.MinorTask.AddMinorTaskWorkTime;

public class AddMinorTaskWorkTimeCommandValidator : AbstractValidator<AddMinorTaskWorkTimeCommand>
{
    public AddMinorTaskWorkTimeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.WorkTimeSeconds)
            .GreaterThanOrEqualTo(0);
    }
}