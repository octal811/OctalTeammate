using FluentValidation;

namespace OctalPulse.Application.Features.Command.Project.RequestProjectJoin;

public class RequestProjectJoinCommandValidator : AbstractValidator<RequestProjectJoinCommand>
{
    public RequestProjectJoinCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
