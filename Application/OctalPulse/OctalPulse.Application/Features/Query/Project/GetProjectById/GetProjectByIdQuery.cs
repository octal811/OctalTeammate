using MediatR;

namespace OctalPulse.Application.Features.Query.Project.GetProjectById;

public record GetProjectByIdQuery(Guid ProjectId, Guid UserId) : IRequest<GetProjectByIdResponse>;