namespace OctalPulse.Application.Features.Command.Project.RequestProjectJoin;

public record RequestProjectJoinResponse(
    Guid ProjectId,
    string Status,
    string Message);
