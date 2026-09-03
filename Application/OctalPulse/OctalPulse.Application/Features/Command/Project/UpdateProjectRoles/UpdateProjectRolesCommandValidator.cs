using FluentValidation;

namespace OctalPulse.Application.Features.Command.Project.UpdateProjectRoles;

public class UpdateProjectRolesCommandValidator : AbstractValidator<UpdateProjectRolesCommand>
{
    public UpdateProjectRolesCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Roles)
            .NotNull()
            .WithMessage("Roles are required.");

        RuleFor(x => x.Roles)
            .Must(r => r.Count > 0)
            .WithMessage("At least one role is required.");

        RuleForEach(x => x.Roles)
            .IsInEnum()
            .WithMessage("Invalid project role.");
    }
}
