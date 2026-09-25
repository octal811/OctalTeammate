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

        var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse", "startup_debug.log");
        void Log(string msg)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
            }
            catch { }
        }

        Log("1. OnStartup initiated.");

        try
        {
            // Keep the app alive when MainWindow is hidden
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            Log("2. Configuring Host...");
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
                    services.AddSingleton<FastAddDialogService>();
                    services.AddSingleton<IFastAddDialogService>(sp => sp.GetRequiredService<FastAddDialogService>());

                    // Background-mode services
                    services.AddSingleton<TrayService>();
                    services.AddSingleton<GlobalHotkeyService>();
                    services.AddSingleton<FloatWindowPositionStore>();
                    services.AddSingleton<FloatWindowService>();
                    services.AddSingleton<RealtimeNotificationService>();
                    services.AddSingleton<AppUpdateCoordinator>();

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
                    services.AddTransient<FastAddViewModel>();
                    services.AddTransient<OctoViewModel>();

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

            Log("3. Starting Host...");
            await _host.StartAsync();
            Services = _host.Services;
            Log("4. Host started successfully.");

            // Check if an update merge was interrupted in a previous run
            Log("5. Checking for interrupted update...");
            var updateCoordinator = Services.GetRequiredService<AppUpdateCoordinator>();
            if (updateCoordinator.CheckAndHandleInterruptedUpdate())
            {
                Log("5a. Interrupted update detected and handled. Exiting OnStartup.");
                return;
            }

            // 1. Show MainWindow immediately so the UI is responsive and visible to the user
            Log("6. Creating MainWindow...");
            var mainWindow = Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            Log("7. Showing MainWindow...");
            mainWindow.Show();
            Log("8. MainWindow is now visible!");

            // 2. Apply theme & preferences asynchronously in background
            Log("9. Applying theme...");
            _ = Task.Run(async () =>
            {
                try
                {
                    var themeService = Services.GetRequiredService<ThemeService>();
                    var localCache = Services.GetRequiredService<ILocalCacheService>();
                    var preferences = await localCache.GetPreferencesAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(preferences?.Theme))
                    {
                        await Dispatcher.InvokeAsync(() => themeService.SetTheme(preferences.Theme));
                    }
                }
                catch (Exception ex)
                {
                    Log($"Theme application warning: {ex.Message}");
                }
            });

            // 3. Load float window positions asynchronously
            Log("10. Loading float window positions...");
            _ = Task.Run(async () =>
            {
                try
                {
                    var positionStore = Services.GetRequiredService<FloatWindowPositionStore>();
                    await positionStore.LoadAsync().ConfigureAwait(false);
                }
                catch { }
            });

            // 4. Initialize real-time SignalR notifications & group sync
            Log("11. Initializing notifications...");
            try
            {
                var notificationService = Services.GetRequiredService<RealtimeNotificationService>();
                notificationService.Initialize();
            }
            catch (Exception ex)
            {
                Log($"Notification init warning: {ex.Message}");
            }

            // 5. Non-blocking background check for remote updates
            updateCoordinator.StartBackgroundUpdateCheck();
        }
        catch (Exception ex)
        {
            Log($"FATAL STARTUP EXCEPTION: {ex}");
            MessageBox.Show($"Startup Error:\n\n{ex.Message}\n\n{ex.StackTrace}", "OctalPulse Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
