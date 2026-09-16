namespace OctalPulse.Services;

public interface IFastAddDialogService
{
    Task<bool> OpenForTrackAsync(Guid? preferredTrackId = null);
    Task<bool> OpenForMajorTaskAsync(Guid majorTaskId, string majorTaskTitle);
}
