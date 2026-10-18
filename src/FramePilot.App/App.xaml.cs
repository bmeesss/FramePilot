using System.Windows;
using FramePilot.App.Services;
using FramePilot.App.ViewModels;
using FramePilot.App.Views;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Infrastructure;
using FramePilot.Core.Services;
using FramePilot.Core.Sessions;
using FramePilot.Windows;

namespace FramePilot.App;

/// <summary>
/// Composition root of FramePilot. All services are created here and injected
/// into the view models.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var registry = BuildServiceRegistry();

        var mainViewModel = registry.Resolve<MainViewModel>();
        var window = new MainWindow(mainViewModel);

        MainWindow = window;
        window.Show();
    }

    private static ServiceRegistry BuildServiceRegistry()
    {
        var registry = new ServiceRegistry();

        registry.RegisterSingleton<IAppLog>(new InMemoryAppLog());
        registry.RegisterSingleton<IClock>(new SystemClock());
        registry.RegisterSingleton<IAppPaths>(_ => new AppPaths());
        registry.RegisterSingleton<IDialogService>(_ => new WpfDialogService());
        registry.RegisterSingleton<IFileDialogService>(_ => new WpfFileDialogService());
        registry.RegisterSingleton<SettingsService>(r => new SettingsService(r.Resolve<IAppPaths>().SettingsPath));

        registry.RegisterSingleton<ISystemMetricsSource>(_ => new WindowsSystemMetricsSource());
        registry.RegisterSingleton<IPowerPlanService>(_ => new WindowsPowerPlanService());
        registry.RegisterSingleton<IProcessController>(_ => new WindowsProcessController());
        registry.RegisterSingleton<IGameProcessLauncher>(_ => new WindowsGameProcessLauncher());
        registry.RegisterSingleton<ISessionJournalStore>(r => new FileSessionJournalStore(r.Resolve<IAppPaths>().SessionJournalPath));
        registry.RegisterSingleton<IPresentMonService>(r =>
        {
            var settings = r.Resolve<SettingsService>();
            var path = settings.LoadAsync().GetAwaiter().GetResult().PresentMonPath;
            return new WindowsPresentMonService(path, AppContext.BaseDirectory);
        });

        registry.RegisterSingleton<SystemMonitor>(r => new SystemMonitor(
            r.Resolve<ISystemMetricsSource>(),
            r.Resolve<IClock>()));

        registry.RegisterSingleton<GameSessionController>(r => new GameSessionController(
            r.Resolve<IPowerPlanService>(),
            r.Resolve<ISessionJournalStore>(),
            r.Resolve<IGameProcessLauncher>(),
            r.Resolve<IAppLog>()));

        registry.RegisterSingleton<SessionRecoveryService>(r => new SessionRecoveryService(
            r.Resolve<IPowerPlanService>(),
            r.Resolve<ISessionJournalStore>(),
            r.Resolve<IAppLog>()));

        registry.RegisterSingleton<DashboardViewModel>();
        registry.RegisterSingleton<GameSessionViewModel>();
        registry.RegisterSingleton<BenchmarkViewModel>();
        registry.RegisterSingleton<ProcessesViewModel>();
        registry.RegisterSingleton<SettingsViewModel>();
        registry.RegisterSingleton<MainViewModel>();

        return registry;
    }
}
