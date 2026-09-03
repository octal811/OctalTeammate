using FluentValidation;

namespace OctalPulse.Application.Features.Command.MinorTask.UpdateMinorTask;

public class UpdateMinorTaskCommandValidator : AbstractValidator<UpdateMinorTaskCommand>
{
    public UpdateMinorTaskCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.Target)
            .MaximumLength(1000);

        RuleFor(x => x.Notes)
            .MaximumLength(2000);

        RuleFor(x => x.Link)
            .MaximumLength(500);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0);
    }
}
