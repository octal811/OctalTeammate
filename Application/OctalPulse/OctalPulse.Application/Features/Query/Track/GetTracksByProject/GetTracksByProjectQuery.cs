using MediatR;

namespace OctalPulse.Application.Features.Query.Track.GetTracksByProject;

public record GetTracksByProjectQuery(Guid ProjectId, Guid UserId) : IRequest<GetTracksByProjectResponse>;