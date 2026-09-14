using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;
using OctalPulse.FloatWindows;
using OctalPulse.Infrastructure;
using OctalPulse.Services;
using OctalPulse.ViewModels;

namespace OctalPulse;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keep the app alive when MainWindow is hidden
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var apiBaseUrl = context.Configuration["OctalPulse:ApiBaseUrl"]
                    ?? Environment.GetEnvironmentVariable("OCTALPULSE_API_URL")
                    ?? "https://octalpulse.runasp.net";

                // Infrastructure services (EF SQLite, DPAPI, ApiClient, SignalR, Services)
                services.AddInfrastructureServices(apiBaseUrl);

                // UI Services
                services.AddSingleton<ThemeService>();
                services.AddSingleton<WpfNavigationService>();
                services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<WpfNavigationService>());
                services.AddSingleton<WpfDialogService>();
                services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<WpfDialogService>());

                // Background-mode services
                services.AddSingleton<TrayService>();
                services.AddSingleton<GlobalHotkeyService>();
                services.AddSingleton<FloatWindowPositionStore>();
                services.AddSingleton<FloatWindowService>();
                services.AddSingleton<RealtimeNotificationService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<RegisterViewModel>();
                services.AddTransient<VerifyEmailViewModel>();
                services.AddTransient<ForgotPasswordViewModel>();
                services.AddSingleton<ShellViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<ProjectsViewModel>();
                services.AddTransient<ProjectDetailViewModel>();
                services.AddTransient<TrackDetailViewModel>();
                services.AddTransient<MajorTaskDetailViewModel>();
                services.AddTransient<CalendarViewModel>();
                services.AddTransient<TasksViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddSingleton<StopwatchViewModel>();
                services.AddSingleton<ProfileViewModel>();
                services.AddTransient<MediaViewModel>();

                // Float ViewModels
                services.AddSingleton<MajorTasksFloatViewModel>();
                services.AddSingleton<MinorTasksFloatViewModel>();
                services.AddSingleton<CalendarFloatViewModel>();
                services.AddSingleton<MediaFloatViewModel>();
                // StopwatchViewModel already singleton above — shared with float window

                // Windows
                services.AddSingleton<MainWindow>();

                // Float Windows (singleton — one instance, show/hide as needed)
                services.AddSingleton<MajorTasksFloatWindow>();
                services.AddSingleton<MinorTasksFloatWindow>();
                services.AddSingleton<StopwatchFloatWindow>();
                services.AddSingleton<CalendarFloatWindow>();
                services.AddSingleton<MediaFloatWindow>();
            })
            .Build();

        await _host.StartAsync();
        Services = _host.Services;

        // Apply the saved theme (defaults to light on first run)
        var themeService = Services.GetRequiredService<ThemeService>();
        var localCache = Services.GetRequiredService<ILocalCacheService>();
        var preferences = await localCache.GetPreferencesAsync();
        themeService.SetTheme(string.IsNullOrWhiteSpace(preferences.Theme) ? "Light" : preferences.Theme);

        // Load float window positions
        var positionStore = Services.GetRequiredService<FloatWindowPositionStore>();
        await positionStore.LoadAsync();

        // Initialize real-time SignalR notifications & group sync
        var notificationService = Services.GetRequiredService<RealtimeNotificationService>();
        notificationService.Initialize();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            // Stop the SignalR connection first while the UI thread is still free,
            // so connection callbacks don't block (or deadlock with) container disposal.
            var realtime = _host.Services.GetService<ISignalRRealtimeService>();
            if (realtime != null)
            {
                var stop = realtime.DisconnectAsync();
                await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(3)));
            }

            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
