using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StudyReportEvaluator.App.Composition;
using StudyReportEvaluator.App.Views;

namespace StudyReportEvaluator.App;

public sealed partial class App : Application
{
    public App()
        : this(new ServiceRegistration())
    {
    }

    public App(ServiceRegistration services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public ServiceRegistration Services { get; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public MainWindow CreateMainWindow() => Services.CreateMainWindow();

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
