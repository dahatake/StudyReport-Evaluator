using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;

namespace StudyReportEvaluator.App.Composition;

public sealed class ServiceRegistration
{
    public ServiceRegistration()
        : this(new WorkflowNavigator())
    {
    }

    public ServiceRegistration(WorkflowNavigator workflowNavigator)
    {
        WorkflowNavigator = workflowNavigator
            ?? throw new ArgumentNullException(nameof(workflowNavigator));
    }

    public WorkflowNavigator WorkflowNavigator { get; }

    public MainWindowViewModel CreateMainWindowViewModel() =>
        new(
            WorkflowNavigator,
            new InputViewModel(),
            new QuantificationDesignViewModel(),
            new ExecutionViewModel(),
            new ResultsOutputViewModel());

    public MainWindow CreateMainWindow() =>
        new(CreateMainWindowViewModel());
}