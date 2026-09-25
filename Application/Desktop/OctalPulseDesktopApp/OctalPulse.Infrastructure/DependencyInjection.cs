using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;
using OctalPulse.Infrastructure.Persistence;
using OctalPulse.Infrastructure.Realtime;
using OctalPulse.Infrastructure.Security;
using OctalPulse.Infrastructure.Services;
using OctalPulse.Infrastructure.Session;

namespace OctalPulse.Infrastructure;

public static class DependencyInjection
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, string apiBaseUrl = "https://localhost:7001")
    {
        ApiConfiguration.BaseUrl = apiBaseUrl.TrimEnd('/');

        // Security & Session
        services.AddSingleton<ISecureStorageService, WindowsDpapiSecureStorageService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<UserSession>();
        services.AddSingleton<IUserSession>(sp => sp.GetRequiredService<UserSession>());

        // Persistence (SQLite)
        services.AddDbContextFactory<LocalAppDbContext>();
        services.AddSingleton<ILocalCacheService, LocalCacheService>();

        // HTTP & API
        services.AddTransient<AuthDelegatingHandler>();

        services.AddHttpClient<ApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/'));
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<AuthDelegatingHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (message.RequestUri?.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) == true)
                    return true;
                return errors == System.Net.Security.SslPolicyErrors.None;
            }
        });

        services.AddHttpClient<IGitHubService, GitHubService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddHttpClient<IUpdateCheckService, UpdateCheckService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITrackService, TrackService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IApiAvailabilityAdminService, ApiAvailabilityAdminService>();
        services.AddScoped<IBadgeService, BadgeService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IFastAddParserService, FastAddParserService>();

        // Gemini & Octo AI Supporter
        services.AddHttpClient<IGeminiClient, OctalPulse.Infrastructure.Gemini.GeminiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<IOctoTool, OctalPulse.Infrastructure.Octo.Tools.GetAccessibleProjectsTool>();
        services.AddScoped<IOctoTool, OctalPulse.Infrastructure.Octo.Tools.GetTracksTool>();
        services.AddScoped<IOctoTool, OctalPulse.Infrastructure.Octo.Tools.GetMajorTasksTool>();
        services.AddScoped<IOctoTool, OctalPulse.Infrastructure.Octo.Tools.GetMinorTasksTool>();
        services.AddScoped<IOctoTool, OctalPulse.Infrastructure.Octo.Tools.GetTaskDetailsTool>();
        services.AddScoped<IOctoTool, OctalPulse.Infrastructure.Octo.Tools.CompareTasksTool>();
        services.AddScoped<IOctoTool, OctalPulse.Infrastructure.Octo.Tools.AnalyzeTaskDependenciesTool>();
        services.AddScoped<IOctoToolRegistry, OctalPulse.Infrastructure.Octo.OctoToolRegistry>();
        services.AddScoped<IOctoAgent, OctalPulse.Infrastructure.Octo.OctoAgent>();

        // SignalR Realtime Service
        services.AddSingleton<ISignalRRealtimeService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SignalRRealtimeService>>();
            var tokenService = sp.GetRequiredService<ITokenService>();
            return new SignalRRealtimeService(apiBaseUrl, tokenService, logger);
        });

        return services;
    }
}
