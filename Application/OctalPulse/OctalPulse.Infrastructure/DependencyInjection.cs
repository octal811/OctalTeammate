using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Infrastructure.Persistence;
using OctalPulse.Infrastructure.Repositories;
using OctalPulse.Infrastructure.Services;

namespace OctalPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.AddSingleton<EmailTemplateRenderer>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<INotificationService, NotificationService>();

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<IJwtService, JwtService>();

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddSingleton<IUserOperationLock, UserOperationLock>();
        services.AddScoped<IApiAvailabilityService, ApiAvailabilityService>();
        services.AddScoped<IProgressCalculator, ProgressCalculator>();

        services.AddScoped(typeof(IGenericRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
        services.AddScoped<IUserProjectRoleRepository, UserProjectRoleRepository>();
        services.AddScoped<ITrackRepository, TrackRepository>();
        services.AddScoped<ITrackMemberRepository, TrackMemberRepository>();
        services.AddScoped<IMajorTaskRepository, MajorTaskRepository>();
        services.AddScoped<IMinorTaskRepository, MinorTaskRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IApiAvailabilityRepository, ApiAvailabilityRepository>();

        return services;
    }
}
