using FluentValidation;

namespace OctalPulse.Application.Features.Command.Project.ReviewProjectJoin;

public class ReviewProjectJoinCommandValidator : AbstractValidator<ReviewProjectJoinCommand>
{
    public ReviewProjectJoinCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.TargetUserId)
            .NotEmpty();
    }
}
