using FluentValidation;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.RequestProjectJoin;

public class RequestProjectJoinCommandValidator : AbstractValidator<RequestProjectJoinCommand>
{
    public RequestProjectJoinCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Roles)
            .NotNull()
            .WithMessage("Roles are required.");

        RuleForEach(x => x.Roles)
            .IsInEnum()
            .WithMessage("Invalid project role.");
    }
}
