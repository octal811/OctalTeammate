namespace OctalPulse.Application.Interface.Repositories;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IProjectRepository Projects { get; }
    IProjectMemberRepository ProjectMembers { get; }
    IUserProjectRoleRepository UserProjectRoles { get; }
    ITrackRepository Tracks { get; }
    ITrackMemberRepository TrackMembers { get; }
    IMajorTaskRepository MajorTasks { get; }
    IMinorTaskRepository MinorTasks { get; }
    IEventRepository Events { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> CompleteAsync(CancellationToken cancellationToken = default);
}
