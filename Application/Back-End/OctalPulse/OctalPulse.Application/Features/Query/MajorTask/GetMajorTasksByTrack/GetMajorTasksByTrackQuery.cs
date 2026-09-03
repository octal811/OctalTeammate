using MediatR;

namespace OctalPulse.Application.Features.Query.MajorTask.GetMajorTasksByTrack;

public record GetMajorTasksByTrackQuery(
    Guid TrackId,
    Guid UserId) : IRequest<GetMajorTasksByTrackResponse>;
