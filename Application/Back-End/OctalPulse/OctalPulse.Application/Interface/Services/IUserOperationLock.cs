namespace OctalPulse.Application.Interface.Services;

public interface IUserOperationLock
{
    IDisposable? TryAcquire(Guid userId, string operation);
}