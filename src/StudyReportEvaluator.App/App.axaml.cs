using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StudyReportEvaluator.App.Composition;
using StudyReportEvaluator.App.Launch;
using StudyReportEvaluator.App.Views;

namespace StudyReportEvaluator.App;

public sealed partial class App : Application
{
    public App()
        : this(new ServiceRegistration(), null)
    {
    }

    public App(ServiceRegistration services)
        : this(services, null)
    {
    }

    public App(
        ServiceRegistration services,
        LaunchOptionsException? startupError)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        StartupError = startupError;
    }

    public ServiceRegistration Services { get; }

    public LaunchOptionsException? StartupError { get; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public MainWindow CreateMainWindow() => Services.CreateMainWindow();

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = StartupError is null
                ? CreateMainWindow()
                : new StartupErrorWindow(StartupError);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
