using OctalPulse.Application.Contracts;
using OctalPulse.Domain.Enums;

namespace OctalPulse.ViewModels;

public class TrackRowViewModel
{
    public TrackSummaryItem Item { get; }
    public bool IsOwner { get; }

    public bool IsMember => IsOwner
        || Item.CurrentUserMembership == MembershipStatus.Approved;

    public bool CanJoin => !IsOwner
        && !IsMember;

    public bool CanLeave => IsMember
        && !IsOwner;

    public TrackRowViewModel(TrackSummaryItem item, bool isOwner)
    {
        Item = item;
        IsOwner = isOwner;
    }
}