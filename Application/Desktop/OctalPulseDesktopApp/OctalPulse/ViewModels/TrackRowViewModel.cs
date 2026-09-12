using OctalPulse.Application.Contracts;
using OctalPulse.Domain.Enums;

namespace OctalPulse.ViewModels;

public record PendingTrackJoinViewModel(Guid TrackId, string TrackName, TrackMemberItem Member);

public class TrackRowViewModel
{
    public TrackSummaryItem Item { get; }
    public bool IsOwner { get; }

    public bool IsJoinPending => !IsOwner
        && Item.CurrentUserMembership == MembershipStatus.Pending;

    public bool IsMember => IsOwner
        || Item.CurrentUserMembership == MembershipStatus.Approved;

    public bool CanRequestJoin => !IsOwner
        && !IsMember
        && !IsJoinPending;

    public bool HasPendingJoins => IsOwner
        && Item.PendingMembers is { Count: > 0 };

    public TrackRowViewModel(TrackSummaryItem item, bool isOwner)
    {
        Item = item;
        IsOwner = isOwner;
    }
}