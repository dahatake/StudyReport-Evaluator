using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.Launch;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;

namespace StudyReportEvaluator.App.Composition;

public sealed class ServiceRegistration
{
    public ServiceRegistration()
        : this(new WorkflowNavigator(), LaunchStartupState.Empty)
    {
    }

    public ServiceRegistration(WorkflowNavigator workflowNavigator)
        : this(workflowNavigator, LaunchStartupState.Empty)
    {
    }

    public ServiceRegistration(
        WorkflowNavigator workflowNavigator,
        LaunchStartupState startup)
    {
        WorkflowNavigator = workflowNavigator
            ?? throw new ArgumentNullException(nameof(workflowNavigator));
        Startup = startup ?? throw new ArgumentNullException(nameof(startup));
    }

    public WorkflowNavigator WorkflowNavigator { get; }

    public LaunchStartupState Startup { get; }

    public static ServiceRegistration FromStartup(LaunchStartupState startup) =>
        new(new WorkflowNavigator(), startup);

    public MainWindowViewModel CreateMainWindowViewModel()
    {
        InputViewModel input = new();
        if (Startup.Options?.InputPath is string inputPath)
        {
            input.ApplyLaunchInput(inputPath);
        }

        return new MainWindowViewModel(
            WorkflowNavigator,
            input,
            new QuantificationDesignViewModel(
                initialDefinition: null,
                availableColumnNames: null,
                importedPrompts: Startup.Prompts),
            new ExecutionViewModel(),
            new ResultsOutputViewModel());
    }

    public MainWindow CreateMainWindow() =>
        new(CreateMainWindowViewModel());
}