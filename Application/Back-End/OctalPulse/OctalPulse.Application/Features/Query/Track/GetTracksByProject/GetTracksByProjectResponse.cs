using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.Track.GetTracksByProject;

public record GetTracksByProjectResponse(IReadOnlyList<TrackSummaryItem> Tracks);

public record TrackSummaryItem(
    Guid Id,
    string Name,
    string? Description,
    int Progress,
    Guid? TrackLeadUserId,
    IReadOnlyList<TrackMemberItem> Members);

public record TrackMemberItem(
    Guid UserId,
    string Name,
    string? ProfilePictureUrl);