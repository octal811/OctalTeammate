using OctalPulse.Application.Contracts;

namespace OctalPulse.ViewModels;

public class TrackRowViewModel
{
    public TrackSummaryItem Item { get; }
    public bool IsOwner { get; }

    public TrackRowViewModel(TrackSummaryItem item, bool isOwner)
    {
        Item = item;
        IsOwner = isOwner;
    }
}