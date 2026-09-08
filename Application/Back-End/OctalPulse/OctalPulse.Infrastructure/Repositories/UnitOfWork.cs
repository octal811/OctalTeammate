using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IUserRepository? _users;
    private IProjectRepository? _projects;
    private IProjectMemberRepository? _projectMembers;
    private IUserProjectRoleRepository? _userProjectRoles;
    private ITrackRepository? _tracks;
    private ITrackMemberRepository? _trackMembers;
    private IMajorTaskRepository? _majorTasks;
    private IMinorTaskRepository? _minorTasks;
    private IEventRepository? _events;
    private IPostRepository? _posts;
    private IPostReactionRepository? _postReactions;
    private IPostCommentRepository? _postComments;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IProjectRepository Projects => _projects ??= new ProjectRepository(_context);
    public IProjectMemberRepository ProjectMembers => _projectMembers ??= new ProjectMemberRepository(_context);
    public IUserProjectRoleRepository UserProjectRoles => _userProjectRoles ??= new UserProjectRoleRepository(_context);
    public ITrackRepository Tracks => _tracks ??= new TrackRepository(_context);
    public ITrackMemberRepository TrackMembers => _trackMembers ??= new TrackMemberRepository(_context);
    public IMajorTaskRepository MajorTasks => _majorTasks ??= new MajorTaskRepository(_context);
    public IMinorTaskRepository MinorTasks => _minorTasks ??= new MinorTaskRepository(_context);
    public IEventRepository Events => _events ??= new EventRepository(_context);
    public IPostRepository Posts => _posts ??= new PostRepository(_context);
    public IPostReactionRepository PostReactions => _postReactions ??= new PostReactionRepository(_context);
    public IPostCommentRepository PostComments => _postComments ??= new PostCommentRepository(_context);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CompleteAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
