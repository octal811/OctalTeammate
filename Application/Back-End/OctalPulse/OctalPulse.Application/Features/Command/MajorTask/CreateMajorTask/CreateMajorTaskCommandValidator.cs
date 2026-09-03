using FluentValidation;

namespace OctalPulse.Application.Features.Command.MajorTask.CreateMajorTask;

public class CreateMajorTaskCommandValidator : AbstractValidator<CreateMajorTaskCommand>
{
    public CreateMajorTaskCommandValidator()
    {
        RuleFor(x => x.TrackId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.Details)
            .MaximumLength(4000);

        RuleFor(x => x.Link)
            .MaximumLength(500);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0);
    }
}
