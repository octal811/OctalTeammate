using MediatR;

namespace OctalPulse.Application.Features.Query.MinorTask.GetMinorTasksByMajorTask;

public record GetMinorTasksByMajorTaskQuery(
    Guid MajorTaskId,
    Guid UserId) : IRequest<GetMinorTasksByMajorTaskResponse>;
