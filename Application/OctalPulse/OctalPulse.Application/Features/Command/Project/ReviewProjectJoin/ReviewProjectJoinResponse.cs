namespace OctalPulse.Application.Features.Command.Project.ReviewProjectJoin;

public record ReviewProjectJoinResponse(
    Guid ProjectId,
    Guid UserId,
    string Status);
