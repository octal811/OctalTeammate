using OctalPulse.Application.Contracts;

namespace OctalPulse.ViewModels;

public class MajorTaskRowViewModel
{
    public MajorTaskItem Item { get; }
    public bool IsOwner { get; }

    public MajorTaskRowViewModel(MajorTaskItem item, bool isOwner)
    {
        Item = item;
        IsOwner = isOwner;
    }
}